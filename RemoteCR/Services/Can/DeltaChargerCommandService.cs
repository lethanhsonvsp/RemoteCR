namespace RemoteCR.Services.Can;

public class DeltaChargerCommandService
{
    private readonly SocketCan _can;
    private CancellationTokenSource? _loopCts;
    private readonly object _lockObj = new object();
    private readonly ChargerVariant _variant;

    public enum ChargerVariant
    {
        V24 = 24,
        V48 = 48
    }

    public DeltaChargerCommandService(SocketCan can, ChargerVariant variant = ChargerVariant.V24)
    {
        _can = can;
        _variant = variant;
    }

    /// <summary>
    /// Encode message ID 0x190 theo đúng spec Delta.
    /// </summary>
    public void Send190(double voltage, double current, bool powerOn, bool clearFaults = false)
    {
        // Validate input theo variant
        var (minV, maxV, maxI) = _variant switch
        {
            ChargerVariant.V24 => (12.0, 30.0, 41.7),  // Trang 8: 12-30V, 41.7A max
            ChargerVariant.V48 => (24.0, 60.0, 20.8),  // Trang 8: 24-60V, 20.8A max
            _ => throw new ArgumentException("Invalid variant")
        };

        if (voltage < minV || voltage > maxV)
        {
            throw new ArgumentException(
                $"Voltage {voltage}V ngoài range {minV}-{maxV}V cho {_variant} variant");
        }

        if (current < 0 || current > maxI)
        {
            throw new ArgumentException(
                $"Current {current}A ngoài range 0-{maxI}A cho {_variant} variant");
        }

        // ----------------------------
        // Encode Demand_V (20-bit, bit 0-19)
        // Factor 0.001 → multiply by 1000
        // ----------------------------
        int demandV = (int)(voltage * 1000);

        byte byte0 = (byte)((demandV >> 0) & 0xFF);   // bit 0-7
        byte byte1 = (byte)((demandV >> 8) & 0xFF);   // bit 8-15
        byte byte2 = (byte)((demandV >> 16) & 0x0F);  // bit 16-19 (4 bits thấp)

        // ----------------------------
        // Encode Control bits (bit 20-31)
        // ----------------------------
        // Bit 20: Demand_PowerStage1 (1=Turn ON, 0=Turn OFF)
        if (powerOn)
            byte2 |= (1 << 4); // bit 20 trong frame = bit 4 của byte2

        // Bit 21: Demand_ClearFaults
        if (clearFaults)
            byte2 |= (1 << 5); // bit 21 trong frame = bit 5 của byte2

        // Bit 22-30: Demand_PowerStage2-10 (để 0 cho standalone)

        byte byte3 = 0x00; // bit 24-31

        // ----------------------------
        // Encode Demand_I (18-bit, bit 32-49)
        // Factor 0.001 → multiply by 1000
        // ----------------------------
        int demandI = (int)(current * 1000);

        byte byte4 = (byte)((demandI >> 0) & 0xFF);   // bit 0-7 của demandI
        byte byte5 = (byte)((demandI >> 8) & 0xFF);   // bit 8-15 của demandI
        byte byte6 = (byte)((demandI >> 16) & 0x03);  // bit 16-17 của demandI

        byte byte7 = 0x00; // bit 56-63 (unused)

        byte[] data =
        {
        byte0, byte1, byte2, byte3, // Voltage + Control bits
        byte4, byte5, byte6, byte7  // Current
    };

        lock (_lockObj)
        {
            _can.Send(0x190, data);
        }

        if (Console.CursorLeft > 0) Console.WriteLine();
        Console.Write($"➡ 0x190: {BitConverter.ToString(data).Replace("-", " ")} ");
        Console.Write($"(V={voltage:F1}V I={current:F1}A {(powerOn ? "ON" : "OFF")})");
    }

    /// <summary>
    /// Gửi 0x190 một lần
    /// </summary>
    public void SendOnce(double voltage, double current, bool on)
    {
        Send190(voltage, current, on);
    }

    /// <summary>
    /// Gửi watchdog 0x190 mỗi 100ms (bắt buộc theo Delta spec trang 28)
    /// "It's suggested that the 0x190 message is sent every 100ms"
    /// </summary>
    public void StartLoop(double voltage, double current, bool on)
    {
        StopLoop();

        _loopCts = new CancellationTokenSource();
        var token = _loopCts.Token;

        Task.Run(async () =>
        {
            Console.WriteLine($"\n🔄 Bắt đầu gửi 0x190 mỗi 100ms (V={voltage}V, I={current}A, ON={on})");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    Send190(voltage, current, on);
                    await Task.Delay(100, token); // Theo spec: 100ms
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("\n⏹ Đã dừng gửi 0x190");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Lỗi gửi 0x190: {ex.Message}");
            }
        }, token);
    }

    /// <summary>
    /// Dừng gửi 0x190
    /// </summary>
    public void StopLoop()
    {
        if (_loopCts != null)
        {
            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
        }
    }

    /// <summary>
    /// Gửi lệnh Turn OFF (dừng sạc)
    /// Theo spec: set Demand_PowerStage1 = 0
    /// </summary>
    public void TurnOff()
    {
        var nominalV = _variant == ChargerVariant.V24 ? 24.0 : 48.0;
        Send190(nominalV, 0, false); // Power OFF
        Console.WriteLine("\n🔴 Đã gửi lệnh Turn OFF");
    }

    /// <summary>
    /// Clear faults và restart
    /// Theo spec trang 27:
    /// "Set this bit to clear faults and re-start charging when there's a fault.
    ///  Note: OVP and OCP excluded, which need a Turn off then Turn on command"
    /// </summary>
    public void ClearFaultsAndRestart(double voltage, double current)
    {
        Console.WriteLine("\n🔄 Clearing faults...");

        // Step 1: Clear faults
        Send190(voltage, current, false, clearFaults: true);
        Thread.Sleep(200); // Chờ charger xử lý

        // Step 2: Turn ON
        StartLoop(voltage, current, true);
        Console.WriteLine("✅ Đã clear faults và restart");
    }

    /// <summary>
    /// Clear OVP/OCP faults (cần Turn OFF → Turn ON)
    /// Theo spec: "OVP and OCP excluded, which need a Turn off then Turn on command"
    /// </summary>
    public void ClearOvpOcpAndRestart(double voltage, double current)
    {
        Console.WriteLine("\n🔄 Clearing OVP/OCP...");

        // Step 1: Turn OFF
        Send190(voltage, current, false);
        Thread.Sleep(200);

        // Step 2: Turn ON
        StartLoop(voltage, current, true);
        Console.WriteLine("✅ Đã clear OVP/OCP và restart");
    }
}