using System;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteCR.Services.Can;

public class DeltaChargerCommandService
{
    private readonly SocketCan _can;
    private CancellationTokenSource? _loopCts;

    public DeltaChargerCommandService(SocketCan can)
    {
        _can = can;
    }

    /// <summary>
    /// Encode message ID 0x190 theo đúng spec Delta.
    /// Voltage (V) → Demand_V (mV * 1000)
    /// Current (A) → Demand_I (mA * 1000)
    /// </summary>
    public void Send190(double voltage, double current, bool powerOn, bool clearFaults = false)
    {
        // ----------------------------
        // Encode Demand_V (20-bit)
        // ----------------------------
        int demandV = (int)(voltage * 1000); // 24.0V → 24000
        byte v0 = (byte)((demandV >> 0) & 0xFF);
        byte v1 = (byte)((demandV >> 8) & 0xFF);
        byte v2 = (byte)((demandV >> 16) & 0x0F); // only lower 4 bits belong to V

        // ----------------------------
        // Encode PowerStage1 bit (bit 20)
        // v2 bit4 = PowerStage1
        // bit21 = ClearFaults
        // ----------------------------
        if (powerOn)
            v2 |= 1 << 4; // bit20

        if (clearFaults)
            v2 |= 1 << 5; // bit21

        // ----------------------------
        // Encode Demand_I (18-bit) - starts at bit 32 (byte4)
        // ----------------------------
        int demandI = (int)(current * 1000); // 20A → 20000
        byte i0 = (byte)((demandI >> 0) & 0xFF);
        byte i1 = (byte)((demandI >> 8) & 0xFF);
        byte i2 = (byte)((demandI >> 16) & 0x03); // only bit0-1 valid

        byte[] data =
        {
            v0, v1, v2, // bytes 0–2: Demand_V + PowerStage bits
            0x00,       // byte3 unused
            i0, i1, i2, // bytes 4–6: Demand_I
            0x00        // byte7 unused
        };

        _can.Send(0x190, data);
        Console.WriteLine($"➡ Sent 0x190: {BitConverter.ToString(data).Replace("-", " ")}");
    }

    /// <summary>
    /// Gửi 0x190 một lần
    /// </summary>
    public void SendOnce(double voltage, double current, bool on)
    {
        Send190(voltage, current, on);
    }

    /// <summary>
    /// Gửi watchdog 0x190 mỗi 100ms (bắt buộc theo Delta)
    /// </summary>
    public void StartLoop(double voltage, double current, bool on)
    {
        StopLoop();
        _loopCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            while (!_loopCts.IsCancellationRequested)
            {
                Send190(voltage, current, on);
                Console.WriteLine($"{voltage} -- {current} -- {on}");
                await Task.Delay(100); // Delta khuyến nghị 100ms

            }
        });
    }

    /// <summary>
    /// Dừng gửi 0x190
    /// </summary>
    public void StopLoop()
    {
        _loopCts?.Cancel();
        _loopCts = null;
    }
}
