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

public class BmuRs485Client : IDisposable
{
    private SerialPort _port;
    private readonly string _portName;
    private readonly int _baud;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private readonly int _readTimeoutMs;
    private readonly int _writeTimeoutMs;

    private readonly object _lock = new();
    private bool _faulted = false;
    private DateTime _lastRetry = DateTime.MinValue;
    private int _retryDelayMs = 1000; // backoff min = 1s

    public BmuRs485Client(
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

        EnsureConnected();
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
            if (i < len - 1) sb.Append("-");
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
        if (_port == null || !_port.IsOpen || _faulted) return Array.Empty<byte>();

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

                    if (buffer.Count >= 4 && buffer[0] == 0xAF && buffer[1] == 0xFA && expectedLen == -1)
                    {
                        expectedLen = buffer[3] + 6;
                    }

                    if (expectedLen > 0 && buffer.Count >= expectedLen)
                    {
                        if (buffer[expectedLen - 2] == 0xAF && buffer[expectedLen - 1] == 0xA0)
                        {
                            Log($"[BMU] Full frame: {ToHex(buffer.ToArray(), buffer.Count)}");
                            return buffer.ToArray();
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
                Log($"[BMU] Timeout / partial frame ({buffer.Count} bytes): {ToHex(buffer.ToArray(), buffer.Count)}");
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log($"[BMU] Read error: {ex.Message}");
            _faulted = true;
            return Array.Empty<byte>();
        }
    }

    public Dictionary<string, double> ReadResponse()
    {
        var frame = ReadFrame();
        if (frame == null || frame.Length < 9) return null;

        if (frame[0] != 0xAF || frame[1] != 0xFA) return null;
        if (frame[^2] != 0xAF || frame[^1] != 0xA0) return null;
        if (frame[4] != 0x03) return null;

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
        return result;
    }

    private void Log(string msg)
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
                _port = null;
            }
        }
        catch (Exception ex)
        {
            Log($"[BMU] Error in Dispose: {ex.Message}");
        }
    }
}
