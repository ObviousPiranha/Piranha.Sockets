using System;

namespace Jawbone.Sockets.Linux;

sealed class LinuxTcpListenerV6 : ITcpListener<IpAddressV6>
{
    private readonly int _fd;
    private readonly SocketOptions _socketOptions;

    public InterruptHandling HandleInterruptOnAccept { get; set; }
    public bool WasInterrupted { get; private set; }

    private LinuxTcpListenerV6(
        int fd,
        SocketOptions socketOptions)
    {
        _fd = fd;
        _socketOptions = socketOptions;
    }

    public ITcpClient<IpAddressV6>? Accept(int timeoutInMilliseconds)
    {
        WasInterrupted = false;
        var milliseconds = int.Max(0, timeoutInMilliseconds);
        var pfd = new PollFd { Fd = _fd, Events = Poll.In };

    retry:
        var start = Environment.TickCount64;
        var pollResult = Sys.Poll(ref pfd, 1, milliseconds);

        if (0 < pollResult)
        {
            if ((pfd.REvents & Poll.In) != 0)
            {
            retryAccept:
                var addressLength = SockAddrStorage.Len;
                var fd = Sys.Accept(_fd, out var address, ref addressLength);
                if (fd == -1)
                {
                    var errNo = Sys.ErrNo();
                    if (Error.IsInterrupt(errNo))
                        WasInterrupted = true;
                    if (!Error.IsInterrupt(errNo) || HandleInterruptOnAccept == InterruptHandling.Error)
                        Sys.Throw(errNo, ExceptionMessages.Accept);
                    goto retryAccept;
                }

                try
                {
                    Tcp.SetNoDelay(fd, !_socketOptions.All(SocketOptions.DisableTcpNoDelay));
                    var endpoint = address.GetV6(addressLength);
                    var result = new LinuxTcpClientV6(fd, endpoint);
                    return result;
                }
                catch
                {
                    _ = Sys.Close(fd);
                    throw;
                }
            }
            else
            {
                throw CreateExceptionFor.BadPoll();
            }
        }
        else if (pollResult == -1)
        {
            var errNo = Sys.ErrNo();
            if (Error.IsInterrupt(errNo))
                WasInterrupted = true;
            if (!Error.IsInterrupt(errNo) || HandleInterruptOnAccept == InterruptHandling.Error)
            {
                Sys.Throw(ExceptionMessages.Poll);
            }
            else if (HandleInterruptOnAccept != InterruptHandling.Abort)
            {
                var elapsed = (int)(Environment.TickCount64 - start);
                milliseconds = int.Max(0, milliseconds - elapsed);
                goto retry;
            }
        }

        return null;
    }

    public IpEndpoint<IpAddressV6> GetSocketName()
    {
        var addressLength = SockAddrStorage.Len;
        var result = Sys.GetSockName(_fd, out var address, ref addressLength);
        if (result == -1)
            Sys.Throw(ExceptionMessages.GetSocketName);
        return address.GetV6(addressLength);
    }

    public void Dispose()
    {
        var result = Sys.Close(_fd);
        if (result == -1)
            Sys.Throw(ExceptionMessages.CloseSocket);
    }

    public static LinuxTcpListenerV6 Listen(
        IpEndpoint<IpAddressV6> bindEndpoint,
        int backlog,
        SocketOptions socketOptions)
    {
        int fd = Sys.Socket(Af.INet6, Sock.Stream, 0);

        if (fd == -1)
            Sys.Throw(ExceptionMessages.OpenSocket);

        try
        {
            Ipv6.SetIpv6Only(fd, socketOptions.All(SocketOptions.EnableDualMode));
            So.SetReuseAddr(fd, socketOptions.None(SocketOptions.DoNotReuseAddress));
            var sa = SockAddrIn6.FromEndpoint(bindEndpoint);
            var bindResult = Sys.BindV6(fd, sa, SockAddrIn6.Len);

            if (bindResult == -1)
            {
                var errNo = Sys.ErrNo();
                Sys.Throw(errNo, $"Failed to bind socket to address {bindEndpoint}.");
            }

            var listenResult = Sys.Listen(fd, backlog);

            if (listenResult == -1)
            {
                var errNo = Sys.ErrNo();
                Sys.Throw(errNo, $"Failed to listen on socket bound to {bindEndpoint}.");
            }

            return new LinuxTcpListenerV6(fd, socketOptions);
        }
        catch
        {
            _ = Sys.Close(fd);
            throw;
        }
    }
}
