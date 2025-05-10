using System;

namespace Piranha.Sockets.Mac;

sealed class MacTcpListenerV4 : ITcpListener<AddressV4>
{
    private readonly int _fd;
    private SockAddrStorage _address;

    public InterruptHandling HandleInterruptOnAccept { get; set; }
    public bool WasInterrupted { get; private set; }

    private MacTcpListenerV4(int fd) => _fd = fd;

    public ITcpClient<AddressV4>? Accept(int timeoutInMilliseconds)
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
                var fd = Sys.Accept(_fd, out _address, ref addressLength);
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
                    Tcp.SetNoDelay(fd);
                    var endpoint = _address.GetV4(addressLength);
                    var result = new MacTcpClientV4(fd, endpoint);
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

    public Endpoint<AddressV4> GetSocketName()
    {
        var addressLength = SockAddrStorage.Len;
        var result = Sys.GetSockName(_fd, out _address, ref addressLength);
        if (result == -1)
            Sys.Throw(ExceptionMessages.GetSocketName);
        return _address.GetV4(addressLength);
    }

    public void Dispose()
    {
        var result = Sys.Close(_fd);
        if (result == -1)
            Sys.Throw(ExceptionMessages.CloseSocket);
    }

    public static MacTcpListenerV4 Listen(Endpoint<AddressV4> bindEndpoint, int backlog)
    {
        int fd = Sys.Socket(Af.INet, Sock.Stream, 0);

        if (fd == -1)
            Sys.Throw(ExceptionMessages.OpenSocket);

        try
        {
            So.SetReuseAddr(fd);
            var sa = SockAddrIn.FromEndpoint(bindEndpoint);
            var bindResult = Sys.BindV4(fd, sa, SockAddrIn.Len);

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

            return new MacTcpListenerV4(fd);
        }
        catch
        {
            _ = Sys.Close(fd);
            throw;
        }
    }
}
