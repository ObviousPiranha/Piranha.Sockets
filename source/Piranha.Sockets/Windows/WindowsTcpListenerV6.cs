using System;

namespace Piranha.Sockets.Windows;

sealed class WindowsTcpListenerV6 : ITcpListener<AddressV6>
{
    private readonly nuint _fd;
    private SockAddrStorage _address;

    public InterruptHandling HandleInterruptOnAccept { get; set; }
    public bool WasInterrupted { get; private set; }

    private WindowsTcpListenerV6(nuint fd) => _fd = fd;

    public ITcpClient<AddressV6>? Accept(int timeoutInMilliseconds)
    {
        WasInterrupted = false;
        var milliseconds = int.Max(0, timeoutInMilliseconds);
        var pfd = new WsaPollFd { Fd = _fd, Events = Poll.In };

    retry:
        var start = Environment.TickCount64;
        var pollResult = Sys.WsaPoll(ref pfd, 1, milliseconds);

        if (0 < pollResult)
        {
            if ((pfd.REvents & Poll.In) != 0)
            {
            retryAccept:
                var addressLength = SockAddrStorage.Len;
                var fd = Sys.Accept(_fd, out _address, ref addressLength);
                if (fd == Sys.InvalidSocket)
                {
                    var error = Sys.WsaGetLastError();
                    if (Error.IsInterrupt(error))
                        WasInterrupted = true;
                    if (!Error.IsInterrupt(error) || HandleInterruptOnAccept == InterruptHandling.Error)
                        Sys.Throw(error, ExceptionMessages.Accept);
                    goto retryAccept;
                }

                try
                {
                    Tcp.SetNoDelay(fd);
                    var endpoint = _address.GetV6(addressLength);
                    var result = new WindowsTcpClientV6(fd, endpoint);
                    return result;
                }
                catch
                {
                    _ = Sys.CloseSocket(fd);
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
            var error = Sys.WsaGetLastError();
            if (Error.IsInterrupt(error))
                WasInterrupted = true;
            if (!Error.IsInterrupt(error) || HandleInterruptOnAccept == InterruptHandling.Error)
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

    public Endpoint<AddressV6> GetSocketName()
    {
        var addressLength = SockAddrStorage.Len;
        var result = Sys.GetSockName(_fd, out _address, ref addressLength);
        if (result == -1)
            Sys.Throw(ExceptionMessages.GetSocketName);
        return _address.GetV6(addressLength);
    }

    public void Dispose()
    {
        var result = Sys.CloseSocket(_fd);
        if (result == -1)
            Sys.Throw(ExceptionMessages.CloseSocket);
    }

    public static WindowsTcpListenerV6 Listen(Endpoint<AddressV6> bindEndpoint, int backlog, bool allowV4)
    {
        var fd = Sys.Socket(Af.INet6, Sock.Stream, 0);

        if (fd == Sys.InvalidSocket)
            Sys.Throw(ExceptionMessages.OpenSocket);

        try
        {
            So.SetReuseAddr(fd);
            Ipv6.SetIpv6Only(fd, allowV4);
            var sa = SockAddrIn6.FromEndpoint(bindEndpoint);
            var bindResult = Sys.BindV6(fd, sa, SockAddrIn6.Len);

            if (bindResult == -1)
            {
                var error = Sys.WsaGetLastError();
                Sys.Throw(error, $"Failed to bind socket to address {bindEndpoint}.");
            }

            var listenResult = Sys.Listen(fd, backlog);

            if (listenResult == -1)
            {
                var error = Sys.WsaGetLastError();
                Sys.Throw(error, $"Failed to listen on socket bound to {bindEndpoint}.");
            }

            return new WindowsTcpListenerV6(fd);
        }
        catch
        {
            _ = Sys.CloseSocket(fd);
            throw;
        }
    }
}
