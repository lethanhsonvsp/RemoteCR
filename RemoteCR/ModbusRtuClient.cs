using System.IO.Ports;

namespace RemoteCR
{
    public class ModbusRtuClient : IDisposable
    {
        private readonly SerialPort _port; //dotnet add package System.IO.Ports

        public ModbusRtuClient(string portName, int baud = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One, int readTimeoutMs = 500, int writeTimeoutMs = 500)
        {
            _port = new SerialPort(portName, baud, parity, dataBits, stopBits)
            {
                ReadTimeout = readTimeoutMs,
                WriteTimeout = writeTimeoutMs
            };
            _port.Open();
        }

        public void Dispose()
        {
            try { _port?.Close(); } catch { /* ignore */ }
            GC.SuppressFinalize(this);
        }

        // ---- CRC16 (Modbus) ----
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

        private byte[] TxRx(byte[] req, int respLen)
        {
            // Append CRC
            ushort crc = Crc16(req, req.Length);
            byte[] frame = new byte[req.Length + 2];
            Array.Copy(req, frame, req.Length);
            frame[^2] = (byte)(crc & 0xFF);       // CRC Lo
            frame[^1] = (byte)(crc >> 8 & 0xFF);// CRC Hi

            // Write
            _port.DiscardInBuffer();
            _port.Write(frame, 0, frame.Length);

            // Read expected length (blocking until timeout)
            byte[] buf = new byte[respLen];
            int got = 0;
            while (got < respLen)
            {
                int b = _port.ReadByte(); // throws on timeout
                buf[got++] = (byte)b;
            }

            // Verify CRC
            if (got < 3) throw new Exception("Response too short");
            ushort rxCrc = (ushort)(buf[got - 2] | buf[got - 1] << 8);
            ushort calc = Crc16(buf, got - 2);
            if (rxCrc != calc) throw new Exception("CRC mismatch");

            return buf;
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

        /// <summary>
        /// Write Multiple Registers (FC 0x10)
        /// </summary>
        public void WriteMultipleRegisters(byte slave, ushort startAddr, ushort[] values)
        {
            if (values == null || values.Length == 0) throw new ArgumentException("values empty");

            int byteCount = values.Length * 2;
            byte[] pdu = new byte[7 + byteCount]; // [id][0x10][addrHi][addrLo][qtyHi][qtyLo][byteCount][data...]
            pdu[0] = slave;
            pdu[1] = 0x10;
            pdu[2] = (byte)(startAddr >> 8);
            pdu[3] = (byte)(startAddr & 0xFF);
            pdu[4] = (byte)(values.Length >> 8);
            pdu[5] = (byte)(values.Length & 0xFF);
            pdu[6] = (byte)byteCount;

            for (int i = 0; i < values.Length; i++)
            {
                pdu[7 + i * 2] = (byte)(values[i] >> 8);      // Hi
                pdu[7 + i * 2 + 1] = (byte)(values[i] & 0xFF);// Lo
            }

            // Expected ACK: [slave][0x10][addrHi][addrLo][qtyHi][qtyLo][CRClo][CRChi]
            int respLen = 8;
            var resp = TxRx(pdu, respLen);

            if (resp[0] != slave || resp[1] != 0x10) throw new Exception("Invalid write response");
            // Optionally validate echoed address & quantity
            ushort echoAddr = (ushort)(resp[2] << 8 | resp[3]);
            ushort echoQty = (ushort)(resp[4] << 8 | resp[5]);
            if (echoAddr != startAddr || echoQty != values.Length) throw new Exception("Write echo mismatch");
        }
    }

}
