using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RemoteCR.Services.Modbus;

public class TadaService : IDisposable
{
    private readonly TadaRs485Client _client;
    private readonly System.Threading.Timer _timer;
    private bool _inLoop = false;

    public DateTime StartTime { get; private set; }
    public int SuccessCount { get; private set; } = 0;
    public int ErrorCount { get; private set; } = 0;

    public Dictionary<string, double> LastData { get; private set; } = [];
    public List<string> LastAlarms { get; private set; } = [];
    public const string PortName = "COM4";

    public ConcurrentDictionary<string, int> ErrorStats { get; } = new();

    public int ChargeCount { get; private set; } = 0;
    public int DischargeCount { get; private set; } = 0;
    public bool BatteryEnabled { get; private set; } = true; // giả định true, có thể gán từ config
    public bool IsCharging { get; private set; } = false;
    public string LastComError { get; private set; } = string.Empty;
    public int FrameMismatchCount { get; private set; } = 0;

    public TadaService()
    {
        _client = new TadaRs485Client(PortName);
        StartTime = DateTime.Now;
        _timer = new System.Threading.Timer(Loop, null, 0, 500);
    }

    private void Loop(object? state)
    {
        if (_inLoop) return;
        _inLoop = true;

        try
        {
            if (!BatteryEnabled)
            {
                // nếu pin tắt thì vẫn cố đọc thông tin cổng nhưng không tăng counters
                var frame = _client.ReadResponse();
                // không làm gì nhiều nếu pin tắt
            }
            else
            {
                _client.RequestStatus(0x60,
                    DataKind1.Voltage | DataKind1.Current | DataKind1.SOC | DataKind1.Status |
                    DataKind1.ChargeTime | DataKind1.DischargeTime | DataKind1.Temp,
                    DataKind2.SOH | DataKind2.RemainCapacity | DataKind2.RemainEnergy);

                var data = _client.ReadResponse();
                if (data != null)
                {
                    SuccessCount++;
                    LastData = data;

                    // decode status for alarms and charging flag
                    if (data.TryGetValue("Status", out double value))
                        LastAlarms = DecodeStatus((int)value);

                    // determine charging by Current sign or Status bit
                    if (data.TryGetValue("Current", out double curr))
                    {
                        bool charging = curr > 0; // tùy vào chiều đo của bộ BMU: dùng >0 là ví dụ
                        if (charging && !IsCharging) ChargeCount++;
                        if (!charging && IsCharging) DischargeCount++;
                        IsCharging = charging;
                    }
                }
                else
                {
                    ErrorCount++;
                    AddError("Timeout/No response");
                    LastComError = "Timeout/No response";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorCount++;
            LastComError = ex.Message;
            if (ex.Message.Contains("closed", StringComparison.OrdinalIgnoreCase))
                AddError("Lost connection");
            else if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                AddError("Timeout");
            else if (ex.Message.Contains("frame", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("invalid frame", StringComparison.OrdinalIgnoreCase))
            {
                AddError("Frame mismatch");
                FrameMismatchCount++;
            }
            else
                AddError("Other error");
        }
        finally
        {
            _inLoop = false;
        }
    }

    private void AddError(string type)
    {
        ErrorStats.AddOrUpdate(type, 1, (_, v) => v + 1);
    }

    private static List<string> DecodeStatus(int status)
    {
        var alarms = new List<string>();
        if ((status & (1 << 0)) != 0) alarms.Add("Battery Over Voltage");
        if ((status & (1 << 1)) != 0) alarms.Add("Battery Low Voltage");
        if ((status & (1 << 2)) != 0) alarms.Add("Charge Over Current");
        if ((status & (1 << 3)) != 0) alarms.Add("Discharge Over Current");
        if ((status & (1 << 4)) != 0) alarms.Add("High Temperature");
        if ((status & (1 << 5)) != 0) alarms.Add("Low Temperature");
        if ((status & (1 << 6)) != 0) alarms.Add("BMU Error");
        return alarms;
    }

    public TimeSpan Uptime => DateTime.Now - StartTime;

    public async Task TryReconnectAsync()
    {
        LastComError = string.Empty;
        await Task.Run(() => _client.ForceReconnect());
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}

