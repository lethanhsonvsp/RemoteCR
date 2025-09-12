using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace RemoteCR
{
    public class ModbusBackgroundService : BackgroundService
    {
        private readonly ModbusRtuClient _mb;
        private readonly byte _slave = 0x01;
        public event Action<DeviceState>? OnStateChanged;

        public ModbusBackgroundService()
        {
            _mb = new ModbusRtuClient("COM4"); // ⚠ chỉnh COM port
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            (byte oldD0, byte oldD1, byte oldD2, byte oldD3) = (0, 0, 0, 0);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var regs = _mb.ReadHoldingRegisters(_slave, 0x0001, 3);
                    var (d0, d1, d2, d3) = ProtocolHelpers.ToBytes(regs);

                    if (d0 != oldD0 || d1 != oldD1 || d2 != oldD2 || d3 != oldD3)
                    {
                        var state = new DeviceState
                        {
                            Heartbeat = (d0 & 0b1000_0000) != 0,
                            LostLink = (d0 & 0b0000_0100) != 0,
                            Locked = (d0 & 0b0000_0010) != 0,
                            EStop = (d0 & 0b0000_0001) != 0,

                            LiftUp = (d1 & 0b0000_0001) != 0,
                            LiftDown = (d1 & 0b0000_0010) != 0,
                            RotateLeft = (d1 & 0b0000_0100) != 0,
                            RotateRight = (d1 & 0b0000_1000) != 0,

                            Forward = (d2 & 0b0001_0000) != 0,
                            Backward = (d2 & 0b0010_0000) != 0,
                            Left = (d2 & 0b0100_0000) != 0,
                            Right = (d2 & 0b1000_0000) != 0,

                            ModeSelect = d1 == 0x40,
                            Enable = d1 == 0x80,   // hoặc tùy bạn map theo d2 nibble
                            Speed = d3 <= 100 ? d3 : 0,
                            Mode = DecodeMode(d2)

                        };

                        // Tạo chuỗi Action để debug/log
                        state.Action = BuildActionString(state);

                        OnStateChanged?.Invoke(state);
                        (oldD0, oldD1, oldD2, oldD3) = (d0, d1, d2, d3);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi đọc Modbus: {ex.Message}");
                }

                await Task.Delay(200, stoppingToken);
            }
        }
        private string DecodeMode(byte d2) => d2 switch
        {
            0x00 => "Default",
            0x01 => "Maintenance",
            0x02 => "Override",
            _ => "Unknown"
        };
        private string BuildActionString(DeviceState s)
        {
            if (s.LiftUp) return "Lift Up";
            if (s.LiftDown) return "Lift Down";
            if (s.RotateLeft) return "Rotate Left";
            if (s.RotateRight) return "Rotate Right";
            if (s.Forward) return "Forward";
            if (s.Backward) return "Backward";
            if (s.Left) return "Left";
            if (s.Right) return "Right";
            if (s.ModeSelect) return "Mode Select";
            if (s.Enable) return "Enable";
            if (s.Speed > 0) return $"Speed {s.Speed}";
            return "None";
        }
    }
    public class DeviceState
    {
        public bool Heartbeat { get; set; }
        public bool LostLink { get; set; }
        public bool Locked { get; set; }
        public bool EStop { get; set; }
        public string Action { get; set; } = "None";

        // Binary mapping
        public bool LiftUp { get; set; }
        public bool LiftDown { get; set; }
        public bool RotateLeft { get; set; }
        public bool RotateRight { get; set; }
        public bool Forward { get; set; }
        public bool Backward { get; set; }
        public bool Left { get; set; }
        public bool Right { get; set; }
        public bool ModeSelect { get; set; }
        public bool Enable { get; set; }

        public int Speed { get; set; }
        public string Mode { get; set; } = "Unknown";

    }
}
