using System;

namespace Piranha.Sockets;

public interface IUdpClient<TAddress> : IDisposable
    where TAddress : unmanaged, IAddress<TAddress>
{
    InterruptHandling HandleInterruptOnSend { get; set; }
    InterruptHandling HandleInterruptOnReceive { get; set; }

    Endpoint<TAddress> Origin { get; }
    TransferResult Send(ReadOnlySpan<byte> message);
    TransferResult Receive(Span<byte> buffer, int timeoutInMilliseconds);
    Endpoint<TAddress> GetSocketName();
}
