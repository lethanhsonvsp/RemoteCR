using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace RemoteCR.Services.Modbus
{
    public class ModbusBackgroundService : BackgroundService
    {
        private readonly ModbusRtuClient _mb;
        private readonly byte _slave = 0x01;
        public event Action<DeviceState>? OnStateChanged;

        public ModbusBackgroundService()
        {
            _mb = new ModbusRtuClient("COM8");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            DeviceState? last = null;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // ✅ ĐỌC 4 WORD
                    var regs = _mb.ReadHoldingRegisters(_slave, 0x0001, 4);
                    var b = ToBytes(regs);

                    byte d0 = b[0]; // Word0 H
                    byte d1 = b[1]; // Word0 L
                    byte d2 = b[2]; // Word1 H
                    byte d3 = b[3]; // Word1 L
                    byte d6 = b[6]; // Word3 H (Joystick FB)
                    byte d7 = b[7]; // Word3 L (Joystick LR)

                    var state = new DeviceState
                    {
                        // ===== System =====
                        Heartbeat = (d0 >> 4) & 0x0F,
                        LostLink = (d0 & 0b0000_0100) != 0,
                        RemoteReady = (d0 & 0b0000_0100) == 0,
                        EStop = (d0 & 0b0000_0001) != 0,

                        // ===== Buttons =====
                        Enable = (d1 & 0b1000_0000) != 0,
                        ModeSelect = (d1 & 0b0100_0000) != 0,

                        LiftUp = (d1 & 0b0000_0001) != 0,
                        LiftDown = (d1 & 0b0000_0010) != 0,
                        RotateLeft = (d1 & 0b0000_0100) != 0,
                        RotateRight = (d1 & 0b0000_1000) != 0,

                        // ===== Speed =====
                        Speed = Math.Clamp((int)d3, 0, 100) / 100f,
                        // ===== Mode =====
                        Mode = DecodeMode(d2)
                    };

                    // ===== Joystick ANALOG =====
                    state.Linear = (d6 - 127f) / 127f;
                    state.Angular = (d7 - 127f) / 127f;

                    // ===== Safety =====
                    if (!state.RemoteReady || state.EStop || !state.Enable)
                    {
                        state.Linear = 0;
                        state.Angular = 0;
                    }

                    state.Action = BuildActionString(state);

                    if (!state.Equals(last))
                    {
                        OnStateChanged?.Invoke(state);
                        last = state;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Modbus] Error: {ex.Message}");
                }

                await Task.Delay(200, stoppingToken);
            }
        }

        private static byte[] ToBytes(ushort[] regs)
        {
            if (regs.Length < 4) throw new ArgumentException("Need 4 registers");

            return new[]
            {
                (byte)(regs[0] >> 8), (byte)regs[0],
                (byte)(regs[1] >> 8), (byte)regs[1],
                (byte)(regs[2] >> 8), (byte)regs[2],
                (byte)(regs[3] >> 8), (byte)regs[3],
            };
        }

        private static string DecodeMode(byte d2)
        {
            return (d2 & 0x0F) switch
            {
                0x00 => "Default",
                0x01 => "Maintenance",
                0x02 => "Override",
                _ => "Unknown"
            };
        }

        private static string BuildActionString(DeviceState s)
        {
            if (!s.RemoteReady) return "Remote Not Ready";
            if (s.EStop) return "E-STOP";
            if (!s.Enable) return "Disabled";

            if (Math.Abs(s.Linear) > 0.05)
                return s.Linear > 0 ? "Forward" : "Backward";

            if (Math.Abs(s.Angular) > 0.05)
                return s.Angular > 0 ? "Right" : "Left";

            if (s.LiftUp) return "Lift Up";
            if (s.LiftDown) return "Lift Down";
            if (s.RotateLeft) return "Rotate Left";
            if (s.RotateRight) return "Rotate Right";

            return "Idle";
        }
    }
}
public class DeviceState
{
    // ===== System =====
    public int Heartbeat { get; set; }
    public bool RemoteReady { get; set; }
    public bool LostLink { get; set; }
    public bool EStop { get; set; }
    public bool Enable { get; set; }
    public string Mode { get; set; } = "Unknown";

    // ===== Buttons =====
    public bool LiftUp { get; set; }
    public bool LiftDown { get; set; }
    public bool RotateLeft { get; set; }
    public bool RotateRight { get; set; }
    public bool ModeSelect { get; set; }

    // ===== Analog Joystick =====
    public float Linear { get; set; }    // -1 → +1
    public float Angular { get; set; }   // -1 → +1

    // ===== Speed =====
    public float Speed { get; set; }     // 0 → 1

    public string Action { get; set; } = "Idle";
}
