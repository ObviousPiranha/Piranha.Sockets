using System;

namespace Piranha.Sockets.Linux;

sealed class LinuxTcpClientV4 : ITcpClient<AddressV4>
{
    private readonly int _fd;

    public Endpoint<AddressV4> Origin { get; }

    public InterruptHandling HandleInterruptOnSend { get; set; }
    public InterruptHandling HandleInterruptOnReceive { get; set; }
    public bool HungUp { get; private set; }

    public LinuxTcpClientV4(int fd, Endpoint<AddressV4> origin)
    {
        _fd = fd;
        Origin = origin;
    }

    public void Dispose()
    {
        // _ = Sys.Shutdown(_fd, Shut.Wr);
        // _ = Sys.Shutdown(_fd, Shut.Rd);
        var result = Sys.Close(_fd);
        if (result == -1)
            Sys.Throw(ExceptionMessages.CloseSocket);
    }

    public TransferResult Receive(Span<byte> buffer, int timeoutInMilliseconds)
    {
        var milliseconds = int.Max(0, timeoutInMilliseconds);
        var pfd = new PollFd { Fd = _fd, Events = Poll.In };

    retry:
        var start = Environment.TickCount64;
        var pollResult = Sys.Poll(ref pfd, 1, milliseconds);

        if (0 < pollResult)
        {
            ObjectDisposedException.ThrowIf((pfd.REvents & Poll.Nval) != 0, this);

            if ((pfd.REvents & Poll.Hup) != 0)
            {
                HungUp = true;
            }

            if ((pfd.REvents & Poll.In) != 0)
            {
            retryReceive:
                var readResult = Sys.Read(
                    _fd,
                    out buffer.GetPinnableReference(),
                    (nuint)buffer.Length);

                if (readResult == -1)
                {
                    var errNo = Sys.ErrNo();
                    if (!Error.IsInterrupt(errNo) || HandleInterruptOnReceive == InterruptHandling.Error)
                        Sys.Throw(ExceptionMessages.ReceiveData);
                    if (HandleInterruptOnReceive == InterruptHandling.Abort)
                        return new(SocketResult.Interrupt);
                    goto retryReceive;
                }

                return new((int)readResult);
            }

            if ((pfd.REvents & Poll.Err) != 0)
                ThrowExceptionFor.PollSocketError();

            return new(0);
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
                return new(SocketResult.Interrupt);
            }
            else
            {
                var elapsed = (int)(Environment.TickCount64 - start);
                milliseconds = int.Max(0, milliseconds - elapsed);
                goto retry;
            }
        }

        return new(SocketResult.Timeout);
    }

    public TransferResult Send(ReadOnlySpan<byte> message)
    {
    retry:
        var writeResult = Sys.Write(
            _fd,
            message.GetPinnableReference(),
            (nuint)message.Length);

        if (writeResult == -1)
        {
            var errNo = Sys.ErrNo();
            if (!Error.IsInterrupt(errNo) || HandleInterruptOnSend == InterruptHandling.Error)
                Sys.Throw(ExceptionMessages.SendStream);
            if (HandleInterruptOnSend == InterruptHandling.Abort)
                return new(SocketResult.Interrupt);
            goto retry;
        }

        return new((int)writeResult);
    }

    public Endpoint<AddressV4> GetSocketName()
    {
        var addressLength = SockAddrStorage.Len;
        var result = Sys.GetSockName(_fd, out var address, ref addressLength);
        if (result == -1)
            Sys.Throw(ExceptionMessages.GetSocketName);
        return address.GetV4(addressLength);
    }

    public static LinuxTcpClientV4 Connect(Endpoint<AddressV4> endpoint)
    {
        int fd = Sys.Socket(Af.INet, Sock.Stream, 0);

        if (fd == -1)
            Sys.Throw(ExceptionMessages.OpenSocket);

        try
        {
            Tcp.SetNoDelay(fd);
            var addr = SockAddrIn.FromEndpoint(endpoint);
            var result = Sys.ConnectV4(fd, addr, SockAddrIn.Len);
            if (result == -1)
            {
                var errNo = Sys.ErrNo();
                Sys.Throw(errNo, $"Failed to connect to {endpoint}.");
            }

            return new LinuxTcpClientV4(fd, endpoint);
        }
        catch
        {
            _ = Sys.Close(fd);
            throw;
        }
    }
}
