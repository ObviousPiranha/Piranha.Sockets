using System;
using System.Net;

namespace Jawbone.Sockets;

public interface IIpAddress :
    ISpanFormattable,
    IUtf8SpanFormattable
{
    bool IsDefault { get; }
    bool IsLinkLocal { get; }
    bool IsLoopback { get; }

    bool TryFormat(Span<char> destination, out int charsWritten);
    bool TryFormat(Span<byte> utf8Destination, out int bytesWritten);
}

public interface IIpAddress<TAddress> :
    IEquatable<TAddress>,
    ISpanParsable<TAddress>,
    IUtf8SpanParsable<TAddress>,
    IIpAddress
    where TAddress : unmanaged, IIpAddress<TAddress>
{
    bool IsInNetwork(IpNetwork<TAddress> ipNetwork);

    static abstract TAddress Any { get; }
    static abstract TAddress Local { get; }
    static abstract IpAddressVersion Version { get; }
    static abstract IpNetwork<TAddress> LinkLocalNetwork { get; }
    static abstract TAddress Parse(ReadOnlySpan<char> s);
    static abstract bool TryParse(ReadOnlySpan<char> s, out TAddress result);
    static abstract TAddress Parse(ReadOnlySpan<byte> utf8Text);
    static abstract bool TryParse(ReadOnlySpan<byte> utf8Text, out TAddress result);
    static abstract IpNetwork<TAddress> CreateNetwork(TAddress address, int prefixLength);

    static abstract explicit operator TAddress(IPAddress ipAddress);
    static abstract explicit operator IPAddress(TAddress ipAddress);

    static abstract bool operator ==(TAddress a, TAddress b);
    static abstract bool operator !=(TAddress a, TAddress b);
}
