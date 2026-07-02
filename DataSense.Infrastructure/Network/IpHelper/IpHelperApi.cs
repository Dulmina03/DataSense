using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DataSense.Infrastructure.Network.IpHelper
{
    public class ConnectionInfo
    {
        public int LocalPort { get; set; }
        public int ProcessId { get; set; }
    }

    public static class IpHelperApi
    {
        private const int AF_INET = 2;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved = 0);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved = 0);

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] remotePort;
            public uint owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MIB_UDPROW_OWNER_PID
        {
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public uint owningPid;
        }

        public static List<ConnectionInfo> GetAllTcpConnections()
        {
            var connections = new List<ConnectionInfo>();
            int bufferSize = 0;
            // TCP_TABLE_OWNER_PID_ALL = 5
            uint ret = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, 5);
            
            if (bufferSize == 0) return connections;

            IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ret = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, AF_INET, 5);
                if (ret == 0)
                {
                    int entryCount = Marshal.ReadInt32(tcpTablePtr);
                    IntPtr rowPtr = tcpTablePtr + 4;

                    for (int i = 0; i < entryCount; i++)
                    {
                        var row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID))!;
                        int port = row.localPort[0] << 8 | row.localPort[1]; // Big Endian
                        connections.Add(new ConnectionInfo { LocalPort = port, ProcessId = (int)row.owningPid });
                        rowPtr += Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTablePtr);
            }
            return connections;
        }

        public static List<ConnectionInfo> GetAllUdpConnections()
        {
            var connections = new List<ConnectionInfo>();
            int bufferSize = 0;
            // UDP_TABLE_OWNER_PID = 1
            uint ret = GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, 1);
            
            if (bufferSize == 0) return connections;

            IntPtr udpTablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                ret = GetExtendedUdpTable(udpTablePtr, ref bufferSize, true, AF_INET, 1);
                if (ret == 0)
                {
                    int entryCount = Marshal.ReadInt32(udpTablePtr);
                    IntPtr rowPtr = udpTablePtr + 4;

                    for (int i = 0; i < entryCount; i++)
                    {
                        var row = (MIB_UDPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_UDPROW_OWNER_PID))!;
                        int port = row.localPort[0] << 8 | row.localPort[1];
                        connections.Add(new ConnectionInfo { LocalPort = port, ProcessId = (int)row.owningPid });
                        rowPtr += Marshal.SizeOf(typeof(MIB_UDPROW_OWNER_PID));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(udpTablePtr);
            }
            return connections;
        }
    }
}
