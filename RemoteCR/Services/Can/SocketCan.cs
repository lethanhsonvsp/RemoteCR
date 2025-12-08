using System;
using System.Runtime.InteropServices;

namespace RemoteCR.Services.Can;

public class SocketCan
{
    private const int AF_CAN = 29;
    private const int SOCK_RAW = 3;
    private const int CAN_RAW = 1;

    private int _socket;

    public event Action<CanFrame>? OnFrameReceived;

    public struct CanFrame
    {
        public uint Id;
        public byte Dlc;
        public byte[] Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct can_frame
    {
        public uint can_id;
        public byte can_dlc;
        public byte __pad1;
        public byte __pad2;
        public byte __pad3;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct sockaddr_can
    {
        public ushort can_family;
        public int can_ifindex;
        public uint rx;
        public uint tx;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ifreq
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] ifr_name;

        public int ifr_ifindex;

        public ifreq(string name)
        {
            ifr_name = new byte[16];
            var bytes = System.Text.Encoding.ASCII.GetBytes(name);
            Array.Copy(bytes, ifr_name, bytes.Length);
            ifr_ifindex = 0;
        }
    }

    // ----------- libc syscalls ---------------
    [DllImport("libc", SetLastError = true)]
    static extern int socket(int domain, int type, int protocol);

    [DllImport("libc", SetLastError = true)]
    static extern int bind(int sockfd, ref sockaddr_can addr, int addrlen);

    [DllImport("libc", SetLastError = true)]
    static extern int ioctl(int fd, uint request, ref ifreq ifr);

    [DllImport("libc", SetLastError = true)]
    static extern int read(int fd, byte[] buffer, int count);

    const uint SIOCGIFINDEX = 0x8933;

    public SocketCan(string iface)
    {
        // 1) Create raw CAN socket using native syscall
        _socket = socket(AF_CAN, SOCK_RAW, CAN_RAW);
        if (_socket < 0)
            throw new Exception("socket(AF_CAN) failed. PF_CAN not enabled in .NET build?");

        // 2) Get interface index
        var ifr = new ifreq(iface);
        if (ioctl(_socket, SIOCGIFINDEX, ref ifr) < 0)
            throw new Exception($"Interface '{iface}' not found.");

        // 3) Bind to CAN interface
        var addr = new sockaddr_can
        {
            can_family = AF_CAN,
            can_ifindex = ifr.ifr_ifindex
        };

        if (bind(_socket, ref addr, Marshal.SizeOf<sockaddr_can>()) < 0)
            throw new Exception($"Bind to {iface} failed.");
    }

    public void StartReading()
    {
        int size = Marshal.SizeOf<can_frame>();
        byte[] buffer = new byte[size];

        while (true)
        {
            int n = read(_socket, buffer, size);
            if (n <= 0) continue;

            var frame = ByteArrayToStruct<can_frame>(buffer);

            OnFrameReceived?.Invoke(new CanFrame
            {
                Id = frame.can_id & 0x1FFFFFFF,
                Dlc = frame.can_dlc,
                Data = frame.data
            });
        }
    }

    private static T ByteArrayToStruct<T>(byte[] bytes)
    {
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        T obj = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject())!;
        handle.Free();
        return obj;
    }
}
