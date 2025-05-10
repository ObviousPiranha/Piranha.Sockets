using System;

namespace Piranha.Sockets.Mac;

sealed class MacUdpSocketV6 : IUdpSocket<AddressV6>
{
    private readonly int _fd;
    private SockAddrStorage _address;

    public InterruptHandling HandleInterruptOnSend { get; set; }
    public InterruptHandling HandleInterruptOnReceive { get; set; }

    private MacUdpSocketV6(int fd)
    {
        _fd = fd;
    }

    public void Dispose()
    {
        var result = Sys.Close(_fd);
        if (result == -1)
            Sys.Throw(ExceptionMessages.CloseSocket);
    }

    public unsafe TransferResult Send(ReadOnlySpan<byte> message, Endpoint<AddressV6> destination)
    {
        var sa = SockAddrIn6.FromEndpoint(destination);

    retry:
        var result = Sys.SendToV6(
            _fd,
            message.GetPinnableReference(),
            (nuint)message.Length,
            0,
            sa,
            SockAddrIn6.Len);

        if (result == -1)
        {
            var errNo = Sys.ErrNo();
            if (!Error.IsInterrupt(errNo) || HandleInterruptOnSend == InterruptHandling.Error)
                Sys.Throw(errNo, ExceptionMessages.SendDatagram);
            if (HandleInterruptOnSend == InterruptHandling.Abort)
                return new(SocketResult.Interrupt);
            goto retry;
        }

        return new((int)result);
    }

    public unsafe TransferResult Receive(
        Span<byte> buffer,
        int timeoutInMilliseconds,
        out Endpoint<AddressV6> origin)
    {
        var milliseconds = int.Max(0, timeoutInMilliseconds);
        var pfd = new PollFd { Fd = _fd, Events = Poll.In };

    retry:
        var start = Environment.TickCount64;
        var pollResult = Sys.Poll(ref pfd, 1, milliseconds);

        if (0 < pollResult)
        {
            ObjectDisposedException.ThrowIf((pfd.REvents & Poll.Nval) != 0, this);
            if ((pfd.REvents & Poll.In) != 0)
            {
            retryReceive:
                var addressLength = SockAddrStorage.Len;
                var receiveResult = Sys.RecvFrom(
                    _fd,
                    out buffer.GetPinnableReference(),
                    (nuint)buffer.Length,
                    0,
                    out _address,
                    ref addressLength);

                if (receiveResult == -1)
                {
                    var errNo = Sys.ErrNo();
                    if (!Error.IsInterrupt(errNo) || HandleInterruptOnReceive == InterruptHandling.Error)
                        Sys.Throw(errNo, ExceptionMessages.ReceiveData);
                    if (HandleInterruptOnReceive == InterruptHandling.Abort)
                    {
                        origin = default;
                        return new(SocketResult.Interrupt);
                    }
                    goto retryReceive;
                }

                origin = _address.GetV6(addressLength);
                return new((int)receiveResult);
            }

            if ((pfd.REvents & Poll.Err) != 0)
                ThrowExceptionFor.PollSocketError();
            ThrowExceptionFor.BadPollState();
        }
        else if (pollResult == -1)
        {
            var errNo = Sys.ErrNo();
            if (!Error.IsInterrupt(errNo) || HandleInterruptOnReceive == InterruptHandling.Error)
            {
                Sys.Throw(errNo, ExceptionMessages.Poll);
            }
            else if (HandleInterruptOnReceive == InterruptHandling.Abort)
            {
                origin = default;
                return new(SocketResult.Interrupt);
            }
            else
            {
                var elapsed = (int)(Environment.TickCount64 - start);
                milliseconds = int.Max(0, milliseconds - elapsed);
                goto retry;
            }
        }

        origin = default;
        return new(SocketResult.Timeout);
    }

    public unsafe Endpoint<AddressV6> GetSocketName()
    {
        var addressLength = SockAddrStorage.Len;
        var result = Sys.GetSockName(_fd, out _address, ref addressLength);
        if (result == -1)
            Sys.Throw(ExceptionMessages.GetSocketName);
        return _address.GetV6(addressLength);
    }

    public static MacUdpSocketV6 Create(bool allowV4)
    {
        var fd = CreateSocket(allowV4);
        return new MacUdpSocketV6(fd);
    }

    public static MacUdpSocketV6 Bind(Endpoint<AddressV6> endpoint, bool allowV4)
    {
        var fd = CreateSocket(allowV4);

        try
        {
            So.SetReuseAddr(fd);
            var sa = SockAddrIn6.FromEndpoint(endpoint);
            var bindResult = Sys.BindV6(fd, sa, SockAddrIn6.Len);

            if (bindResult == -1)
            {
                var errNo = Sys.ErrNo();
                Sys.Throw(errNo, $"Failed to bind socket to address {endpoint}.");
            }

            return new MacUdpSocketV6(fd);
        }
        catch
        {
            _ = Sys.Close(fd);
            throw;
        }
    }

    private static int CreateSocket(bool allowV4)
    {
        var fd = Sys.Socket(Af.INet6, Sock.DGram, IpProto.Udp);

        if (fd == -1)
            Sys.Throw(ExceptionMessages.OpenSocket);

        try
        {
            Ipv6.SetIpv6Only(fd, allowV4);
            return fd;
        }
        catch
        {
            _ = Sys.Close(fd);
            throw;
        }
    }
}
