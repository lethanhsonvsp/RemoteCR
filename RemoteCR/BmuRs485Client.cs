using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace RemoteCR
{
    [Flags]
    public enum DataKind1 : byte
    {
        Voltage = 1 << 0,
        Current = 1 << 1,
        SOC = 1 << 2,
        Status = 1 << 3,
        ChargeTime = 1 << 4,
        DischargeTime = 1 << 5,
        Temp = 1 << 6
    }

    [Flags]
    public enum DataKind2 : byte
    {
        SOH = 1 << 0,
        RemainCapacity = 1 << 1,
        RemainEnergy = 1 << 2
    }

    public class BmuRs485Client : IDisposable
    {
        private readonly SerialPort _port;

        public BmuRs485Client(
            string portName,
            int baud = 19200,
            Parity parity = Parity.None,
            int dataBits = 8,
            StopBits stopBits = StopBits.One,
            int readTimeoutMs = 2000,
            int writeTimeoutMs = 1000)
        {
            _port = new SerialPort(portName, baud, parity, dataBits, stopBits)
            {
                ReadTimeout = readTimeoutMs,
                WriteTimeout = writeTimeoutMs
            };
            _port.Open();
            Console.WriteLine($"[BMU] Opened {_port.PortName} @ {baud}bps");
        }

        public void Dispose()
        {
            try
            {
                if (_port?.IsOpen == true)
                {
                    _port.Close();
                    Console.WriteLine("[BMU] Serial port closed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BMU] Error closing port: {ex.Message}");
            }
        }

        private static byte Checksum(byte[] data, int start, int len)
        {
            int sum = 0;
            for (int i = start; i < start + len; i++) sum += data[i];
            return (byte)(sum & 0xFF);
        }

        private static string ToHex(byte[] data, int len)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                sb.Append(data[i].ToString("X2"));
                if (i < len - 1) sb.Append("-");
            }
            return sb.ToString();
        }

        public void RequestStatus(byte address = 0x60,
            DataKind1 kind1 = DataKind1.Voltage | DataKind1.Current | DataKind1.SOC | DataKind1.Status |
                              DataKind1.ChargeTime | DataKind1.DischargeTime | DataKind1.Temp,
            DataKind2 kind2 = DataKind2.SOH | DataKind2.RemainCapacity | DataKind2.RemainEnergy)
        {
            if (!_port.IsOpen) return;

            byte kind1Byte = (byte)kind1;
            byte kind2Byte = (byte)kind2;

            byte[] frame = new byte[]
            {
                0xAF, 0xFA,
                address,
                0x05,
                0x01,
                address,
                kind1Byte, kind2Byte,
                0x00,
                0xAF, 0xA0
            };

            frame[8] = Checksum(frame, 2, 6);
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer(); // Thêm để clear output buffer
            _port.Write(frame, 0, frame.Length);
            Thread.Sleep(50); // Delay nhỏ để BMU response
        }

        private byte[] ReadFrame()
        {
            if (!_port.IsOpen) return Array.Empty<byte>();

            var buffer = new List<byte>();
            int expectedLen = -1;

            try
            {
                while (true)
                {
                    int bytesAvailable = _port.BytesToRead;
                    if (bytesAvailable > 0)
                    {
                        byte[] tempBuffer = new byte[bytesAvailable];
                        int bytesRead = _port.Read(tempBuffer, 0, bytesAvailable);
                        buffer.AddRange(tempBuffer.Take(bytesRead));

                        if (buffer.Count >= 4 && buffer[0] == 0xAF && buffer[1] == 0xFA && expectedLen == -1)
                        {
                            expectedLen = buffer[3] + 6; // Tổng độ dài frame (data + header/end)
                        }

                        if (expectedLen > 0 && buffer.Count >= expectedLen)
                        {
                            if (buffer[expectedLen - 2] == 0xAF && buffer[expectedLen - 1] == 0xA0)
                            {
                                Console.WriteLine($"[BMU] Full frame: {ToHex(buffer.ToArray(), buffer.Count)}");
                                return buffer.ToArray();
                            }
                            else
                            {
                                // Frame không hợp lệ, bỏ qua và tiếp tục
                                buffer.Clear();
                                expectedLen = -1;
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(2); // Chờ thêm dữ liệu
                    }
                }
            }
            catch (TimeoutException)
            {
                if (buffer.Count > 0)
                    Console.WriteLine($"[BMU] Timeout / partial frame ({buffer.Count} bytes): {ToHex(buffer.ToArray(), buffer.Count)}");
                return Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BMU] Read error: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        public Dictionary<string, double> ReadResponse()
        {
            var frame = ReadFrame();
            if (frame.Length < 9) return null;

            if (frame[0] != 0xAF || frame[1] != 0xFA) return null;
            if (frame[^2] != 0xAF || frame[^1] != 0xA0) return null;
            if (frame[4] != 0x03) return null; // Response command

            var result = new Dictionary<string, double>();
            int dataLen = frame[3] - 3; // Bỏ cmd, order, chksum
            int dataStart = 6;

            for (int i = 0; i < dataLen; i += 2)
            {
                ushort raw = (ushort)((frame[dataStart + i] << 8) | frame[dataStart + i + 1]);
                int index = i / 2;

                switch (index)
                {
                    case 0: result["Voltage"] = raw / 100.0; break;
                    case 1: result["Current"] = (short)raw / 100.0; break; // Sửa: Current có thể âm (discharge)
                    case 2: result["SOC"] = raw; break;
                    case 3: result["Status"] = raw; break;
                    case 4: result["ChargeTime"] = raw; break;
                    case 5: result["DischargeTime"] = raw; break;
                    case 6: result["Temp"] = (short)raw / 10.0; break; // Temp có thể âm
                    case 7: result["SOH"] = raw; break;
                    case 8: result["RemainCapacity"] = raw / 100.0; break;
                    case 9: result["RemainEnergy"] = raw / 10.0; break;
                }
            }

            Console.WriteLine("[BMU] Decode => " + string.Join(", ", result.Select(kv => $"[{kv.Key}, {kv.Value}]")));
            return result;
        }
    }
}

