namespace RemoteCR
{
    public class BmuService : IDisposable
    {
        private readonly BmuRs485Client _client;
        private Timer _timer;
        public Dictionary<string, double> LastData { get; private set; }
        public List<string> LastAlarms { get; private set; } = new();
        public const string portName = "COM4";

        public BmuService()
        {
            _client = new BmuRs485Client(portName);
            _timer = new Timer(Loop, null, 0, 200); // Tăng lên 2500ms để tránh overlap
        }

        private void Loop(object state)
        {
            try
            {
                // Request full data (có thể giảm để test: chỉ Voltage | SOC | Temp, Kind2=0)
                _client.RequestStatus(0x60,
                    DataKind1.Voltage | DataKind1.Current | DataKind1.SOC | DataKind1.Status |
                    DataKind1.ChargeTime | DataKind1.DischargeTime | DataKind1.Temp,
                    DataKind2.SOH | DataKind2.RemainCapacity | DataKind2.RemainEnergy);

                var data = _client.ReadResponse();
                if (data == null)
                {
                    Console.WriteLine("[BmuService] No data, retrying...");
                    _client.RequestStatus(); // Retry 1 lần
                    data = _client.ReadResponse();
                }

                if (data != null)
                {
                    LastData = data;
                    if (data.ContainsKey("Status"))
                    {
                        LastAlarms = DecodeStatus((int)data["Status"]);
                    }

                    Console.WriteLine("[BmuService] OK => " + string.Join(", ", data.Select(kv => $"[{kv.Key}, {kv.Value}]")));
                    if (LastAlarms.Count > 0)
                    {
                        Console.WriteLine("[BmuService] ⚠ Cảnh báo: " + string.Join(" | ", LastAlarms));
                    }
                }
            }
            catch (TimeoutException tex)
            {
                Console.WriteLine($"[BmuService] Timeout: {tex.Message}");
            }
            catch (OperationCanceledException cex)
            {
                Console.WriteLine($"[BmuService] Canceled: {cex.Message} - Kiểm tra port!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BmuService] Error: {ex.Message}");
            }
        }

        private List<string> DecodeStatus(int status)
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

        public void Dispose()
        {
            _timer?.Dispose();
            _client?.Dispose();
        }
    }
}