namespace RemoteCR;

public class BmuService : IDisposable
{
    private readonly BmuRs485Client _client;
    private readonly Timer _timer;
    private bool _inLoop = false;

    public DateTime StartTime { get; private set; }
    public int SuccessCount { get; private set; } = 0;
    public int ErrorCount { get; private set; } = 0;

    public Dictionary<string, double> LastData { get; private set; }
    public List<string> LastAlarms { get; private set; } = [];
    public const string portName = "COM4";

    // 👇 thống kê lỗi theo loại
    public Dictionary<string, int> ErrorStats { get; private set; } = [];

    public BmuService()
    {
        _client = new BmuRs485Client(portName);
        StartTime = DateTime.Now;
        _timer = new Timer(Loop, null, 0, 500);
        LastData = [];
    }

    private void Loop(object? state)
    {
        if (_inLoop) return;
        _inLoop = true;

        try
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
                if (data.TryGetValue("Status", out double value))
                    LastAlarms = DecodeStatus((int)value);
            }
            else
            {
                ErrorCount++;
                AddError("Timeout/No response");
            }
        }
        catch (Exception ex)
        {
            ErrorCount++;
            if (ex.Message.Contains("closed"))
                AddError("Lost connection");
            else if (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                AddError("Timeout");
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
        if (!ErrorStats.ContainsKey(type))
            ErrorStats[type] = 0;
        ErrorStats[type]++;
    }

    private static List<string> DecodeStatus(int status)
    {
        var alarms = new List<string>();
        if ((status & 1 << 0) != 0) alarms.Add("Battery Over Voltage");
        if ((status & 1 << 1) != 0) alarms.Add("Battery Low Voltage");
        if ((status & 1 << 2) != 0) alarms.Add("Charge Over Current");
        if ((status & 1 << 3) != 0) alarms.Add("Discharge Over Current");
        if ((status & 1 << 4) != 0) alarms.Add("High Temperature");
        if ((status & 1 << 5) != 0) alarms.Add("Low Temperature");
        if ((status & 1 << 6) != 0) alarms.Add("BMU Error");
        return alarms;
    }

    public TimeSpan Uptime => DateTime.Now - StartTime;

    public void Dispose()
    {
        _timer?.Dispose();
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}