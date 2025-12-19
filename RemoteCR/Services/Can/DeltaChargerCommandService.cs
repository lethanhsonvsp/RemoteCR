using System;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteCR.Services.Can;

public partial class DeltaChargerCommandService
{
    private readonly SocketCan _can;
    private CancellationTokenSource? _loopCts;
    private readonly object _lockObj = new();
    private readonly ChargerVariant _variant;

    // =========================================================
    // Constructor
    // =========================================================
    public DeltaChargerCommandService(
        SocketCan can,
        ChargerVariant variant = ChargerVariant.V48   // ✅ default V48
    )
    {
        _can = can;
        _variant = variant;

        Console.WriteLine($"⚡ Delta Charger Variant = {_variant}");
    }

    // =========================================================
    // Encode & Send CAN ID 0x190 (CORE)
    // =========================================================
    public void Send190(
        uint x,
        double voltage,
        double current,
        bool powerOn,
        bool clearFaults = false
    )
    {
        // ----------------------------
        // Validate theo variant
        // ----------------------------
        var (minV, maxV, maxI) = _variant switch
        {
            ChargerVariant.V24 => (12.0, 30.0, 41.7),
            ChargerVariant.V48 => (24.0, 60.0, 20.8),
            _ => throw new ArgumentException("Invalid charger variant")
        };

        if (voltage < minV || voltage > maxV)
            throw new ArgumentException(
                $"Voltage {voltage}V ngoài range {minV}-{maxV}V cho {_variant}");

        if (current < 0 || current > maxI)
            throw new ArgumentException(
                $"Current {current}A ngoài range 0-{maxI}A cho {_variant}");

        // ----------------------------
        // Demand_V (20-bit, factor 0.001)
        // ----------------------------
        int demandV = (int)(voltage * 1000);

        byte byte0 = (byte)((demandV >> 0) & 0xFF);
        byte byte1 = (byte)((demandV >> 8) & 0xFF);
        byte byte2 = (byte)((demandV >> 16) & 0x0F);

        // ----------------------------
        // Control bits
        // ----------------------------
        if (powerOn)
            byte2 |= (1 << 4);   // bit20 PowerStage1

        if (clearFaults)
            byte2 |= (1 << 5);   // bit21 ClearFaults

        byte byte3 = 0x00;

        // ----------------------------
        // Demand_I (18-bit, factor 0.001)
        // ----------------------------
        int demandI = (int)(current * 1000);

        byte byte4 = (byte)((demandI >> 0) & 0xFF);
        byte byte5 = (byte)((demandI >> 8) & 0xFF);
        byte byte6 = (byte)((demandI >> 16) & 0x03);
        byte byte7 = 0x00;

        byte[] data =
        {
            byte0, byte1, byte2, byte3,
            byte4, byte5, byte6, byte7
        };

        lock (_lockObj)
        {
            uint idcan = 0x190 +x;
            _can.Send(idcan, data);
        }

        Console.WriteLine(
            $"➡ 0x190: {BitConverter.ToString(data).Replace("-", " ")} " +
            $"(V={voltage:F1}V I={current:F1}A {(powerOn ? "ON" : "OFF")})"
        );
    }

    // =========================================================
    // Send once
    // =========================================================
    public void SendOnce(uint x, double voltage, double current, bool on)
    {
        Send190(x, voltage, current, on);
    }

    // =========================================================
    // Watchdog loop (100 ms – Delta required)
    // =========================================================
    public void StartLoop(uint x,double voltage, double current, bool on)
    {
        StopLoop();

        _loopCts = new CancellationTokenSource();
        var token = _loopCts.Token;

        Task.Run(async () =>
        {
            Console.WriteLine(
                $"🔄 Start 0x190 loop 100ms (V={voltage}V I={current}A ON={on})");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Send190(x, voltage, current, on);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 0x190 send failed: {ex.Message}");
                        break; // ❗ stop loop if invalid
                    }

                    await Task.Delay(100, token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("⏹ Stop 0x190 loop");
            }
        }, token);
    }

    // =========================================================
    // Stop watchdog loop
    // =========================================================
    public void StopLoop()
    {
        if (_loopCts != null)
        {
            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
        }
    }

    // =========================================================
    // Turn OFF charger (Delta-safe)
    // =========================================================
    //public void TurnOff()
    //{
    //    double nominalV = _variant == ChargerVariant.V48 ? 48.0 : 24.0;

    //    Send190(nominalV, 0, false);
    //    Thread.Sleep(120);
    //    Send190(nominalV, 0, false);

    //    Console.WriteLine("🔴 Charger OFF");
    //}

    // =========================================================
    // Reset faults & restart charging (CHUẨN DELTA)
    // =========================================================
    public async Task ResetFaultsAndStartAsync(
        uint x,
        double voltage,
        double current
    )
    {
        Console.WriteLine("🧹 Reset charger faults");

        double nominalV = _variant == ChargerVariant.V48 ? 48.0 : 24.0;

        // STEP 1: OFF
        Send190(x, nominalV, 0, false);
        await Task.Delay(300);

        // STEP 2: ClearFaults (OFF)
        Send190(x,voltage, current, false, clearFaults: true);
        await Task.Delay(300);

        // STEP 3: ON + ClearFaults
        Send190(x, voltage, current, true, clearFaults: true);
        await Task.Delay(300);

        // STEP 4: Normal ON + watchdog
        StartLoop(x,voltage, current, true);

        Console.WriteLine("✅ Reset sequence done");
    }
}
