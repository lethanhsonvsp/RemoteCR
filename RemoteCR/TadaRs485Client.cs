using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;

namespace RemoteCR;

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
public class TadaRs485Client : IDisposable
{
    private SerialPort _port;
    private readonly string _portName;
    private readonly int _baud;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private readonly int _readTimeoutMs;
    private readonly int _writeTimeoutMs;

    private readonly Lock _lock = new();
    private bool _faulted = false;
    private DateTime _lastRetry = DateTime.MinValue;
    private int _retryDelayMs = 1000; // backoff min = 1s
    private DateTime _lastDataTime = DateTime.MinValue;
    private readonly int _dataTimeoutMs = 10_000; // 10 giây
    private int _consecutiveFails = 0;
    private readonly int _maxFails = 3; // sau 3 lần fail liên tiếp thì coi như lost

    public TadaRs485Client(
        string portName,
        int baud = 19200,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        int readTimeoutMs = 2000,
        int writeTimeoutMs = 1000)
    {
        _portName = portName;
        _baud = baud;
        _parity = parity;
        _dataBits = dataBits;
        _stopBits = stopBits;
        _readTimeoutMs = readTimeoutMs;
        _writeTimeoutMs = writeTimeoutMs;
        _lastDataTime = DateTime.Now;
        _consecutiveFails = 0;
        _port = null!;
        EnsureConnected();
    }

    private void CheckDataTimeout()
    {
        if (_lastDataTime != DateTime.MinValue &&
            (DateTime.Now - _lastDataTime).TotalMilliseconds > _dataTimeoutMs)
        {
            Log("[BMU] Communication lost (data timeout).");
            _faulted = true;
            _lastDataTime = DateTime.MinValue; // reset để tránh spam log
        }
    }

    private void EnsureConnected()
    {
        lock (_lock)
        {
            if (_port != null && _port.IsOpen && !_faulted) return;

            if ((DateTime.Now - _lastRetry).TotalMilliseconds < _retryDelayMs)
                return;
            _lastRetry = DateTime.Now;

            try
            {
                _port?.Dispose();
                _port = new SerialPort(_portName, _baud, _parity, _dataBits, _stopBits)
                {
                    ReadTimeout = _readTimeoutMs,
                    WriteTimeout = _writeTimeoutMs
                };
                _port.Open();
                _faulted = false;
                _retryDelayMs = 1000; // reset backoff sau khi thành công
                Log($"[BMU] Connected {_port.PortName} @ {_baud}bps");
            }
            catch (Exception ex)
            {
                Log($"[BMU] Connect failed: {ex.Message}");
                _faulted = true;
                // exponential backoff up to 30s
                _retryDelayMs = Math.Min(_retryDelayMs * 2, 30_000);
            }
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
            if (i < len - 1) sb.Append('-');
        }
        return sb.ToString();
    }

    public void RequestStatus(byte address = 0x60,
        DataKind1 kind1 = DataKind1.Voltage | DataKind1.Current | DataKind1.SOC | DataKind1.Status |
                          DataKind1.ChargeTime | DataKind1.DischargeTime | DataKind1.Temp,
        DataKind2 kind2 = DataKind2.SOH | DataKind2.RemainCapacity | DataKind2.RemainEnergy)
    {
        EnsureConnected();
        if (_port == null || !_port.IsOpen || _faulted) return;

        try
        {
            byte kind1Byte = (byte)kind1;
            byte kind2Byte = (byte)kind2;

            byte[] frame =
            [
                0xAF, 0xFA,
                address,
                0x05,
                0x01,
                address,
                kind1Byte, kind2Byte,
                0x00,
                0xAF, 0xA0
            ];

            frame[8] = Checksum(frame, 2, 6);
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();
            _port.Write(frame, 0, frame.Length);
            Thread.Sleep(50);
        }
        catch (Exception ex)
        {
            Log($"[BMU] Write error: {ex.Message}");
            _faulted = true;
        }
    }

    private byte[] ReadFrame()
    {
        EnsureConnected();
        if (_port == null || !_port.IsOpen || _faulted) return [];

        var buffer = new List<byte>();
        int expectedLen = -1;
        var start = DateTime.Now;

        try
        {
            while ((DateTime.Now - start).TotalMilliseconds < _readTimeoutMs)
            {
                int bytesAvailable = _port.BytesToRead;
                if (bytesAvailable > 0)
                {
                    byte[] tempBuffer = new byte[bytesAvailable];
                    int bytesRead = _port.Read(tempBuffer, 0, bytesAvailable);
                    buffer.AddRange(tempBuffer.Take(bytesRead));

                    // check start marker (chuẩn AF FA hoặc bản partial 4D)
                    if (buffer.Count >= 3 && expectedLen == -1)
                    {
                        if (buffer[0] == 0xAF && buffer[1] == 0xFA)
                        {
                            expectedLen = buffer[3] + 6;
                        }
                        else if (buffer[0] == 0x4D)
                        {
                            // frame thiếu AF FA -> vẫn tính chiều dài như thường
                            expectedLen = buffer[2] + 5; // vì mất 2 byte start
                        }
                    }

                    if (expectedLen > 0 && buffer.Count >= expectedLen)
                    {
                        if (buffer[^2] == 0xAF && buffer[^1] == 0xA0)
                        {
                            if (buffer[0] == 0xAF && buffer[1] == 0xFA)
                                Log($"[BMU] Full frame (AF FA): {ToHex([.. buffer], buffer.Count)}");
                            else if (buffer[0] == 0x4D)
                                Log($"[BMU] Partial frame (4D): {ToHex([.. buffer], buffer.Count)}");

                            return [.. buffer];
                        }
                        else
                        {
                            buffer.Clear();
                            expectedLen = -1;
                        }
                    }

                }
                else
                {
                    Thread.Sleep(2);
                }
            }

            // hết thời gian chờ
            if (buffer.Count > 0)
                Log($"[BMU] Timeout / partial frame ({buffer.Count} bytes): {ToHex([.. buffer], buffer.Count)}");
            return [];
        }
        catch (Exception ex)
        {
            Log($"[BMU] Read error: {ex.Message}");
            _faulted = true;
            return [];
        }
    }

    public Dictionary<string, double>? ReadResponse()
    {
        var frame = ReadFrame();
        if (frame == null || frame.Length < 9)
        {
            _consecutiveFails++;
            if (_consecutiveFails >= _maxFails)
            {
                Log("[BMU] Communication lost (too many failed reads).");
                _faulted = true;
            }
            CheckDataTimeout();
            return null;
        }

        if (frame[0] == 0xAF && frame[1] == 0xFA)
        {
            Log("[BMU] Decode from AF FA frame");
        }
        else if (frame[0] == 0x4D)
        {
            Log("[BMU] Decode from 4D partial frame");
            var newFrame = new byte[frame.Length + 2];
            newFrame[0] = 0xAF; newFrame[1] = 0xFA;
            Array.Copy(frame, 0, newFrame, 2, frame.Length);
            frame = newFrame;
        }
        else
        {
            _consecutiveFails++;
            if (_consecutiveFails >= _maxFails)
            {
                Log("[BMU] Communication lost (invalid frame).");
                _faulted = true;
            }
            CheckDataTimeout();
            return null;
        }

        if (frame[^2] != 0xAF || frame[^1] != 0xA0)
        {
            Log("[BMU] Footer mismatch");
            throw new Exception("invalid frame: footer");
        }
        if (frame[4] != 0x03)
        {
            Log("[BMU] Command code mismatch");
            throw new Exception("invalid frame: command");
        }

        var result = new Dictionary<string, double>();
        int dataLen = frame[3] - 3;
        int dataStart = 6;

        for (int i = 0; i + 1 < dataLen; i += 2)
        {
            int idx = dataStart + i;
            if (idx + 1 >= frame.Length) break;

            ushort raw = (ushort)((frame[idx] << 8) | frame[idx + 1]);
            int index = i / 2;

            switch (index)
            {
                case 0: result["Voltage"] = raw / 100.0; break;
                case 1: result["Current"] = (short)raw / 100.0; break;
                case 2: result["SOC"] = raw; break;
                case 3: result["Status"] = raw; break;
                case 4: result["ChargeTime"] = raw; break;
                case 5: result["DischargeTime"] = raw; break;
                case 6: result["Temp"] = (short)raw / 10.0; break;
                case 7: result["SOH"] = raw; break;
                case 8: result["RemainCapacity"] = raw / 100.0; break;
                case 9: result["RemainEnergy"] = raw / 10.0; break;
            }
        }

        Log("[BMU] Decode => " + string.Join(", ", result.Select(kv => $"[{kv.Key}, {kv.Value}]")));
        _lastDataTime = DateTime.Now;  // reset watchdog
        _consecutiveFails = 0;         // reset fail counter
        return result;
    }

    public void ForceReconnect()
    {
        lock (_lock)
        {
            try
            {
                _port?.Close();
            }
            catch { }
            _faulted = false;
            _lastRetry = DateTime.MinValue;
            _retryDelayMs = 1000;
            EnsureConnected();
        }
    }

    private static void Log(string msg)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
    }

    public void Dispose()
    {
        try
        {
            if (_port != null)
            {
                if (_port.IsOpen)
                {
                    _port.Close();
                    Log("[BMU] Serial port closed.");
                }
                _port.Dispose();
                _port = null!;
            }
        }
        catch (Exception ex)
        {
            Log($"[BMU] Error in Dispose: {ex.Message}");
        }
        GC.SuppressFinalize(this);
    }
}
