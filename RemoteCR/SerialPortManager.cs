using System.IO.Ports;

namespace RemoteCR
{
    public class SerialPortManager : IDisposable
    {
        private readonly SerialPort _port;
        private readonly object _lock = new();

        public SerialPortManager(
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
            Console.WriteLine($"[SerialPortManager] Opened {portName} @ {baud}bps");
        }

        public byte[] SendAndReceive(byte[] request, int expectedLength, int delayBeforeRead = 0)
        {
            lock (_lock) // đảm bảo chỉ một client dùng port tại một thời điểm
            {
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
                _port.Write(request, 0, request.Length);

                if (delayBeforeRead > 0) Thread.Sleep(delayBeforeRead);

                var buffer = new byte[expectedLength];
                int got = 0;
                while (got < expectedLength)
                {
                    int b = _port.ReadByte(); // throws nếu timeout
                    buffer[got++] = (byte)b;
                }
                return buffer;
            }
        }

        public int ReadByte()
        {
            lock (_lock)
            {
                return _port.ReadByte();
            }
        }

        public void Dispose()
        {
            try { _port?.Close(); } catch { }
        }
    }
}
