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
            _mb = new ModbusRtuClient("COM4"); // ⚠️ chỉnh COM
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            (byte oldD0, byte oldD1, byte oldD2, byte oldD3, byte oldD4) = (0, 0, 0, 0, 0);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var regs = _mb.ReadHoldingRegisters(_slave, 0x0001, 3); // đọc 3 word
                    var (d0, d1, d2, d3) = ProtocolHelpers.ToBytes(regs);
                    byte d4 = (byte)(regs[2] >> 8);

                    Console.WriteLine($"Data: {d0:X2} {d1:X2} {d2:X2} {d3:X2} {d4:X2}");

                    if (d0 != oldD0 || d1 != oldD1 || d2 != oldD2 || d3 != oldD3 || d4 != oldD4)
                    {
                        string action = ParseAction(d1, d2, d3);

                        var state = new DeviceState
                        {
                            Heartbeat = ProtocolHelpers.Bit(d0, 7),
                            LostLink = ProtocolHelpers.Bit(d0, 2),
                            Locked = ProtocolHelpers.Bit(d0, 1),
                            EStop = ProtocolHelpers.Bit(d0, 0),
                            Action = action
                        };

                        OnStateChanged?.Invoke(state);

                        (oldD0, oldD1, oldD2, oldD3, oldD4) = (d0, d1, d2, d3, d4);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi đọc Modbus: {ex.Message}");
                }

                await Task.Delay(200, stoppingToken);
            }
        }

        private string ParseAction(byte d1, byte d2, byte d3)
        {
            string code = $"{d1:X2}{d2:X2}{d3:X2}";
            string Mode(byte m) => m switch
            {
                0x00 => "Default",
                0x01 => "Maintenance",
                0x02 => "Override",
                _ => "Unknown" // Replace null with a default value
            };

            var d1Actions = new Dictionary<byte, string>
            {
                [0x01] = "Lift Up",
                [0x02] = "Lift Down",
                [0x04] = "Rotate Left",
                [0x08] = "Rotate Right"
            };

            var d2Moves = new Dictionary<byte, string>
            {
                [0x10] = "Forward",
                [0x20] = "Backward",
                [0x40] = "Left",
                [0x80] = "Right"
            };

            // --- Trường hợp d3 == 0 (các nút bấm cơ bản) ---
            if (d3 == 0x00)
            {
                if (d1 == 0x00 && Mode(d2) is string m0) return m0;
                if (d1 == 0x40 && Mode(d2) is string m1) return $"Mode Select {m1}";
                if (d1 == 0x80 && (d2 == 0x00 || d2 == 0x01 || d2 == 0x02)) return $"Enable {Mode(d2)}";

                if (d1Actions.TryGetValue(d1, out var act) && Mode(d2) is string m2) return $"{act} {m2}";

                if (d1 == 0x80)
                {
                    var high = (byte)(d2 & 0xF0);
                    var low = (byte)(d2 & 0x0F);
                    if (d2Moves.TryGetValue(high, out var mv) && Mode(low) is string m3) return $"{mv} {m3}";
                }
            }

            // --- Speed analog ---
            if (d1 == 0x00 && d3 <= 0x64 && Mode(d2) is string m4)
                return $"Speed {m4} {d3}";

            // --- Action + Speed (mở rộng) ---
            if (d3 <= 0x64)
            {
                // Lift / Rotate
                if (d1Actions.TryGetValue(d1, out var act) && Mode(d2) is string m5)
                    return $"{act} {m5} + Speed {d3}";

                // Mode Select
                if (d1 == 0x40 && Mode(d2) is string m6)
                    return $"Mode Select {m6} + Speed {d3}";

                // Enable
                if (d1 == 0x80 && (d2 == 0x00 || d2 == 0x01 || d2 == 0x02))
                    return $"Enable {Mode(d2)} + Speed {d3}";

                // Move
                if (d1 == 0x80)
                {
                    var high = (byte)(d2 & 0xF0);
                    var low = (byte)(d2 & 0x0F);
                    if (d2Moves.TryGetValue(high, out var mv) && Mode(low) is string m7)
                        return $"{mv} {m7} + Speed {d3}";
                }
            }

            return $"Other ({code})";
        }

    }

    public class DeviceState
    {
        public bool Heartbeat { get; set; }
        public bool LostLink { get; set; }
        public bool Locked { get; set; }
        public bool EStop { get; set; }
        public string Action { get; set; } = "None";
    }
}
