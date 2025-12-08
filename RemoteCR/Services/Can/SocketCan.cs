using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace RemoteCR.Services.Can;

public class SocketCan
{
    private const int PF_CAN = 29;
    private const int SOCK_RAW = 3;
    private const int CAN_RAW = 1;

    private readonly Socket _socket;

    public event Action<CanFrame>? OnFrameReceived;

    public struct CanFrame
    {
        public uint Id;
        public byte Dlc;
        public byte[] Data;
    }

    // Native CAN struct for Linux
    [StructLayout(LayoutKind.Sequential)]
    private struct can_frame_native
    {
        public uint can_id;
        public byte can_dlc;
        public byte pad1;
        public byte pad2;
        public byte pad3;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] data;
    }

    public SocketCan(string interfaceName)
    {
        _socket = new Socket((AddressFamily)PF_CAN, SocketType.Raw, (ProtocolType)CAN_RAW);
        BindTo(interfaceName);
    }

    private void BindTo(string ifName)
    {
        var ifr = new ifreq();
        byte[] nameBytes = System.Text.Encoding.ASCII.GetBytes(ifName);
        Array.Copy(nameBytes, ifr.ifr_name, nameBytes.Length);

        int fd = GetSocketFd(_socket);

        if (ioctl(fd, SIOCGIFINDEX, ref ifr) < 0)
            throw new Exception("Cannot find CAN interface index: " + ifName);

        var addr = new sockaddr_can
        {
            can_family = PF_CAN,
            can_ifindex = ifr.ifr_ifindex
        };

        _socket.Bind(new SockAddrCan(addr));
    }

    public void StartReading()
    {
        int frameSize = Marshal.SizeOf<can_frame_native>();
        byte[] buffer = new byte[frameSize];

        while (true)
        {
            _socket.Receive(buffer);

            var native = ByteArrayToStruct<can_frame_native>(buffer);

            var frame = new CanFrame
            {
                Id = native.can_id,
                Dlc = native.can_dlc,
                Data = native.data
            };

            OnFrameReceived?.Invoke(frame);
        }
    }

    // ---------------- Native Helpers ------------------

    private static T ByteArrayToStruct<T>(byte[] bytes)
    {
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        T obj = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        handle.Free();
        return obj;
    }

    private static int GetSocketFd(Socket s)
    {
        var field = typeof(Socket).GetField("_handle",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        IntPtr handle = (IntPtr)field!.GetValue(s)!;
        return handle.ToInt32();
    }

    private const int IFNAMSIZ = 16;
    private const uint SIOCGIFINDEX = 0x8933;

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, ref ifreq ifr);

    [StructLayout(LayoutKind.Sequential)]
    struct ifreq
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IFNAMSIZ)]
        public byte[] ifr_name;
        public int ifr_ifindex;

        public ifreq()
        {
            ifr_name = new byte[IFNAMSIZ];
            ifr_ifindex = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct sockaddr_can
    {
        public ushort can_family;
        public int can_ifindex;
        public uint rx;
        public uint tx;
    }

    class SockAddrCan : System.Net.EndPoint
    {
        private readonly sockaddr_can _addr;

        public SockAddrCan(sockaddr_can addr)
        {
            _addr = addr;
        }

        public override SocketAddress Serialize()
        {
            SocketAddress sa = new SocketAddress((AddressFamily)PF_CAN, 16);

            sa[0] = (byte)(_addr.can_family & 0xFF);
            sa[1] = (byte)((_addr.can_family >> 8) & 0xFF);

            sa[4] = (byte)(_addr.can_ifindex & 0xFF);
            sa[5] = (byte)((_addr.can_ifindex >> 8) & 0xFF);
            sa[6] = (byte)((_addr.can_ifindex >> 16) & 0xFF);
            sa[7] = (byte)((_addr.can_ifindex >> 24) & 0xFF);

            return sa;
        }
    }
}