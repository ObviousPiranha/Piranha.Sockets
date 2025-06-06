using System;

namespace Jawbone.Sockets;

public static class TcpClientV4
{
    public static ITcpClient<IpAddressV4> Connect(IpEndpoint<IpAddressV4> endpoint)
    {
        if (OperatingSystem.IsWindows())
            return Windows.WindowsTcpClientV4.Connect(endpoint);
        if (OperatingSystem.IsMacOS())
            return Mac.MacTcpClientV4.Connect(endpoint);
        if (OperatingSystem.IsLinux())
            return Linux.LinuxTcpClientV4.Connect(endpoint);
        throw new PlatformNotSupportedException();
    }
}
