namespace RemoteCR
{
    public static class ProtocolHelpers
    {
        // Giải mã dữ liệu đọc về theo mapping trong tài liệu (2 thanh ghi từ 0x0001) → 4 bytes
        // regs[0] = Word0 (H), regs[1] = Word1 (H?) — lưu ý: mỗi "Word" ở Modbus là 16-bit big-endian.
        // Ở tài liệu: Data0..Data3 là 4 byte theo thứ tự [Word0_H][Word0_L][Word1_H][Word1_L]
        public static (byte data0, byte data1, byte data2, byte data3) ToBytes(ushort[] regs)
        {
            if (regs == null || regs.Length < 2) throw new ArgumentException("Need 2 registers");
            byte data0 = (byte)(regs[0] >> 8);
            byte data1 = (byte)(regs[0] & 0xFF);
            byte data2 = (byte)(regs[1] >> 8);
            byte data3 = (byte)(regs[1] & 0xFF);
            return (data0, data1, data2, data3);
        }

        // Ví dụ bóc bit cờ trong Data0 (Word0_H)
        public static bool Bit(byte b, int bit) => (b >> bit & 0x01) == 1;

        // Ví dụ diễn giải joystick (Data2 = Word1_H, Data3 = Word1_L)
        public static (int vertical, int horizontal) ParseJoystick(byte data2, byte data3)
        {
            // Theo tài liệu: 0..126 là xuống/trái, 127 là giữa, 128..254 là lên/phải
            int v = data2 - 127; // âm: xuống; dương: lên
            int h = data3 - 127; // âm: trái; dương: phải
            return (v, h);
        }
    }

}
