using System;
using System.IO.Ports;

namespace RemoteCR.Services.Modbus;

public class ModbusRtuClient : IDisposable
{
    private SerialPort? _port;

    private readonly string _portName;
    private readonly int _baud;
    private readonly Parity _parity;
    private readonly int _dataBits;
    private readonly StopBits _stopBits;
    private readonly int _readTimeoutMs;
    private readonly int _writeTimeoutMs;

    private readonly object _sync = new();
    private bool _faulted = false;
    private DateTime _lastRetry = DateTime.MinValue;
    private int _retryDelayMs = 1000;

    public bool IsFaulted => _faulted;

    public ModbusRtuClient(string portName,
        int baud = 9600,
        Parity parity = Parity.None,
        int dataBits = 8,
        StopBits stopBits = StopBits.One,
        int readTimeoutMs = 500,
        int writeTimeoutMs = 500)
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

    // -------------------------
    // AUTO RECONNECT (giống TadaRs485Client)
    // -------------------------
    private void EnsureConnected()
    {
        lock (_sync)
        {
            if (_port != null && _port.IsOpen && !_faulted)
                return;

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
                _retryDelayMs = 1000; // reset backoff
                Console.WriteLine($"[Modbus] Connected {_portName}@{_baud}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Modbus] Open port failed: {ex.Message}");
                _faulted = true;

                // exponential backoff giống Tada
                _retryDelayMs = Math.Min(_retryDelayMs * 2, 30000);
            }
        }
    }

    public void ForceReconnect()
    {
        lock (_sync)
        {
            try
            {
                if (_port != null)
                {
                    try { if (_port.IsOpen) _port.Close(); } catch { }
                    _port.Dispose();
                    _port = null;
                }
            }
            catch { }

            _faulted = false;
            _lastRetry = DateTime.MinValue;
            _retryDelayMs = 1000;

            EnsureConnected();
        }
    }

    private void Fail()
    {
        _faulted = true;
    }

    // -------------------------
    // CRC
    // -------------------------
    private static ushort Crc16(byte[] data, int len)
    {
        ushort crc = 0xFFFF;
        for (int i = 0; i < len; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                    bool lsb = (crc & 0x0001) != 0;
                crc >>= 1;
                if (lsb) crc ^= 0xA001;
            }
        }
        return crc;
    }

    // -------------------------
    // TX/RX WITH RECONNECT
    // -------------------------
    private byte[] TxRx(byte[] req, int respLen)
    {
        EnsureConnected();
        if (_port == null || !_port.IsOpen || _faulted)
            throw new Exception("Modbus port not available");

        try
        {
            // Build frame
            ushort crc = Crc16(req, req.Length);
            byte[] frame = new byte[req.Length + 2];
            Array.Copy(req, frame, req.Length);
            frame[^2] = (byte)(crc & 0xFF);       // CRC Lo
            frame[^1] = (byte)(crc >> 8 & 0xFF);// CRC Hi

            _port.DiscardInBuffer();
            _port.Write(frame, 0, frame.Length);

            // Read response
            byte[] buf = new byte[respLen];
            int got = 0;

            while (got < respLen)
            {
                int b = _port.ReadByte();  // may throw TimeoutException
                buf[got++] = (byte)b;
            }

            // Verify CRC
            if (got < 3) throw new Exception("Response too short");
                ushort rxCrc = (ushort)(buf[got - 2] | buf[got - 1] << 8);
                ushort calc = Crc16(buf, got - 2);
            if (rxCrc != calc) throw new Exception("CRC mismatch");

            return buf;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Modbus] IO error: " + ex.Message);
            Fail();          // 🔥 đánh dấu lỗi (giống Tada)
            EnsureConnected(); // thử reconnect
            throw;
        }
    }

        /// <summary>
        /// Read Holding Registers (FC 0x03)
        /// </summary>
    public ushort[] ReadHoldingRegisters(byte slave, ushort startAddr, ushort quantity)
    {
        byte[] pdu =
        [
            slave, 0x03,
                (byte)(startAddr >> 8), (byte)(startAddr & 0xFF),
                (byte)(quantity >> 8), (byte)(quantity & 0xFF),
        ];

            // Expected response: [slave][0x03][byteCount][data...][CRClo][CRChi]
        int byteCount = quantity * 2;
        int respLen = 3 + byteCount + 2;
        var resp = TxRx(pdu, respLen);

            if (resp[0] != slave || resp[1] != 0x03) throw new Exception("Invalid response function");
            if (resp[2] != byteCount) throw new Exception("Unexpected byte count");

        ushort[] regs = new ushort[quantity];
        for (int i = 0; i < quantity; i++)
        {
            int idx = 3 + i * 2;
                regs[i] = (ushort)(resp[idx] << 8 | resp[idx + 1]); // Big-endian to ushort
        }
        return regs;
    }

    public void WriteMultipleRegisters(byte slave, ushort startAddr, ushort[] values)
    {
        int byteCount = values.Length * 2;
        byte[] pdu = new byte[7 + byteCount];

        pdu[0] = slave;
        pdu[1] = 0x10;
        pdu[2] = (byte)(startAddr >> 8);
        pdu[3] = (byte)startAddr;
        pdu[4] = (byte)(values.Length >> 8);
        pdu[5] = (byte)values.Length;
        pdu[6] = (byte)byteCount;

        for (int i = 0; i < values.Length; i++)
        {
            pdu[7 + i * 2] = (byte)(values[i] >> 8);
            pdu[7 + i * 2 + 1] = (byte)values[i];
        }

        int respLen = 8;
        TxRx(pdu, respLen);
    }

    // -------------------------
    // Dispose
    // -------------------------
    public void Dispose()
    {
        lock (_sync)
        {
            try
            {
                if (_port != null)
                {
                    if (_port.IsOpen) _port.Close();
                    _port.Dispose();
                }
            }
            catch { }
            _port = null;
        }
        GC.SuppressFinalize(this);
    }
}
