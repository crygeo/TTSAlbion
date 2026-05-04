using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace LibNetWork.PortResolution;

/// <summary>
/// Inspects OS-level TCP/UDP connections to find ports owned by a given process.
/// Uses managed IPGlobalProperties first; falls back to netstat parsing for UDP.
/// Isolated here so AlbionPortResolver stays thin and testable with a mock inspector.
/// </summary>
public sealed class ProcessNetworkInspector
{
    /// <summary>
    /// Returns all local ports (TCP + UDP) currently open by any process with the given PID set.
    /// </summary>
    public IReadOnlySet<int> GetPortsForProcessIds(IReadOnlySet<int> pids)
    {
        if (pids.Count == 0) return new HashSet<int>();

        var ports = new HashSet<int>();

        // TCP active connections
        try
        {
            var props = IPGlobalProperties.GetIPGlobalProperties();

            foreach (var conn in props.GetActiveTcpConnections())
            {
                // TcpConnectionInformation doesn't expose PID — we need GetExtendedTcpTable
                // so we fall through to the P/Invoke path below.
            }
        }
        catch { /* ignore — P/Invoke path covers it */ }

        // P/Invoke path: GetExtendedTcpTable gives us PID per connection
        CollectTcpPortsViaPInvoke(pids, ports);
        CollectUdpPortsViaPInvoke(pids, ports);

        return ports;
    }

    private static void CollectTcpPortsViaPInvoke(IReadOnlySet<int> pids, HashSet<int> result)
    {
        int bufferSize = 0;
        _ = NativeMethods.GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2 /*AF_INET*/, 5 /*TCP_TABLE_OWNER_PID_ALL*/, 0);

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            if (NativeMethods.GetExtendedTcpTable(buffer, ref bufferSize, true, 2, 5, 0) != 0)
                return;

            int rowCount = Marshal.ReadInt32(buffer);
            int offset = 4;
            int rowSize = Marshal.SizeOf<NativeMethods.MibTcpRowOwnerPid>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MibTcpRowOwnerPid>(buffer + offset);
                if (pids.Contains((int)row.dwOwningPid))
                {
                    int port = (int)(ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                    result.Add(port);
                }
                offset += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void CollectUdpPortsViaPInvoke(IReadOnlySet<int> pids, HashSet<int> result)
    {
        int bufferSize = 0;
        _ = NativeMethods.GetExtendedUdpTable(IntPtr.Zero, ref bufferSize, true, 2 /*AF_INET*/, 1 /*UDP_TABLE_OWNER_PID*/, 0);

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            if (NativeMethods.GetExtendedUdpTable(buffer, ref bufferSize, true, 2, 1, 0) != 0)
                return;

            int rowCount = Marshal.ReadInt32(buffer);
            int offset = 4;
            int rowSize = Marshal.SizeOf<NativeMethods.MibUdpRowOwnerPid>();

            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<NativeMethods.MibUdpRowOwnerPid>(buffer + offset);
                if (pids.Contains((int)row.dwOwningPid))
                {
                    int port = (int)(ushort)IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                    result.Add(port);
                }
                offset += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    
}