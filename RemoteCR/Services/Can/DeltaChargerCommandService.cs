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
        ChargerVariant variant = ChargerVariant.V48
    )
    {
        _can = can;
        _variant = variant;

        Console.WriteLine($"⚡ Delta Charger Variant = {_variant}");
    }

    // =========================================================
    // Encode & Send CAN ID 0x190 + x
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
        // Variant limits
        // ----------------------------
        var (minV, maxV, maxI) = _variant switch
        {
            ChargerVariant.V24 => (12.0, 30.0, 41.7),
            ChargerVariant.V48 => (24.0, 60.0, 20.8),
            _ => throw new ArgumentException("Invalid charger variant")
        };

        if (voltage < minV || voltage > maxV)
            throw new ArgumentException(
                $"Voltage {voltage}V out of range {minV}-{maxV}V");

        if (current < 0 || current > maxI)
            throw new ArgumentException(
                $"Current {current}A out of range 0-{maxI}A");

        // ----------------------------
        // Demand Voltage (20-bit, 0.001V)
        // ----------------------------
        int demandV = Math.Clamp((int)(voltage * 1000), 0, 0xFFFFF);

        byte byte0 = (byte)(demandV & 0xFF);
        byte byte1 = (byte)((demandV >> 8) & 0xFF);
        byte byte2 = (byte)((demandV >> 16) & 0x0F);

        if (powerOn)
            byte2 |= 1 << 4;   // PowerStage1 (bit20)

        if (clearFaults)
            byte2 |= 1 << 5;   // ClearFaults (bit21)

        byte byte3 = 0x00;

        // ----------------------------
        // Demand Current (18-bit, 0.001A)
        // ----------------------------
        int demandI = Math.Clamp((int)(current * 1000), 0, 0x3FFFF);

        byte byte4 = (byte)(demandI & 0xFF);
        byte byte5 = (byte)((demandI >> 8) & 0xFF);
        byte byte6 = (byte)((demandI >> 16) & 0x03);
        byte byte7 = 0x00;

        byte[] data =
        {
            byte0, byte1, byte2, byte3,
            byte4, byte5, byte6, byte7
        };

        uint canId = 0x190 + x;

        lock (_lockObj)
        {
            _can.Send(canId, data);
        }

        Console.WriteLine(
            $"➡ ID=0x{canId:X3}  {BitConverter.ToString(data).Replace("-", " ")} " +
            $"(V={voltage:F1}V I={current:F1}A {(powerOn ? "ON" : "OFF")})"
        );
    }

    // =========================================================
    // Send once
    // =========================================================
    public void SendOnce(uint x, double voltage, double current, bool on)
        => Send190(x, voltage, current, on);

    // =========================================================
    // Watchdog loop (100 ms)
    // =========================================================
    public void StartLoop(uint x, double voltage, double current, bool on)
    {
        StopLoop();

        _loopCts = new CancellationTokenSource();
        var token = _loopCts.Token;

        Task.Run(async () =>
        {
            Console.WriteLine(
                $"🔄 Start 0x{(0x190 + x):X3} loop 100ms (V={voltage} I={current} ON={on})");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    Send190(x, voltage, current, on);
                    await Task.Delay(100, token);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("⏹ Stop 0x190 loop");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Watchdog error: {ex.Message}");
            }
        }, token);
    }

    // =========================================================
    // Stop watchdog
    // =========================================================
    public void StopLoop()
    {
        if (_loopCts == null) return;

        _loopCts.Cancel();
        _loopCts.Dispose();
        _loopCts = null;
    }

    // =========================================================
    // Reset faults & restart (Delta standard sequence)
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

        // STEP 2: Clear faults (OFF)
        Send190(x, voltage, current, false, clearFaults: true);
        await Task.Delay(300);

        // STEP 3: ON + ClearFaults
        Send190(x, voltage, current, true, clearFaults: true);
        await Task.Delay(300);

        // STEP 4: Normal watchdog
        StartLoop(x, voltage, current, true);

        Console.WriteLine("✅ Reset sequence done");
    }
}
