using System;
using System.Net;
using System.Net.Sockets;

namespace Jawbone.Sockets;

public readonly struct IpAddress : IEquatable<IpAddress>, ISpanFormattable, IUtf8SpanFormattable
{
    private readonly IpAddressV6 _storage;

    public readonly IpAddressVersion Version { get; }

    public IpAddress(IPAddress? ipAddress)
    {
        if (ipAddress is null)
            return;
        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            Version = IpAddressVersion.V4;
            _ = ipAddress.TryWriteBytes(_storage.DataU8[..4], out _);
        }
        else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            Version = IpAddressVersion.V6;
            _ = ipAddress.TryWriteBytes(_storage.DataU8, out _);
        }
    }

    public IpAddress(IpAddressV4 address)
    {
        Version = IpAddressVersion.V4;
        _storage.DataU32[0] = address.DataU32;
    }

    public IpAddress(IpAddressV6 address)
    {
        Version = IpAddressVersion.V6;
        _storage = address;
    }

    public readonly bool IsV4(out IpAddressV4 address)
    {
        var result = Version == IpAddressVersion.V4;
        address = result ? AsV4() : default;
        return result;
    }

    public readonly bool IsV6(out IpAddressV6 address)
    {
        var result = Version == IpAddressVersion.V6;
        address = result ? AsV6() : default;
        return result;
    }

    internal readonly IpAddressV4 AsV4() => new(_storage.DataU32[0]);
    internal readonly IpAddressV6 AsV6() => _storage;

    public readonly override bool Equals(object? obj) => obj is IpAddress other && Equals(other);

    public readonly override int GetHashCode()
    {
        return Version switch
        {
            IpAddressVersion.V4 => AsV4().GetHashCode(),
            IpAddressVersion.V6 => AsV6().GetHashCode(),
            _ => 0
        };
    }

    public readonly override string ToString()
    {
        return Version switch
        {
            IpAddressVersion.V4 => AsV4().ToString(),
            IpAddressVersion.V6 => AsV6().ToString(),
            _ => ""
        };
    }

    public readonly bool Equals(IpAddress other)
    {
        return Version == other.Version && Version switch
        {
            IpAddressVersion.V4 => AsV4().Equals(other.AsV4()),
            IpAddressVersion.V6 => AsV6().Equals(other.AsV6()),
            _ => true
        };
    }

    public readonly bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = default)
    {
        bytesWritten = 0;
        var result = Version switch
        {
            IpAddressVersion.V4 => AsV4().TryFormat(utf8Destination, out bytesWritten, format, provider),
            IpAddressVersion.V6 => AsV6().TryFormat(utf8Destination, out bytesWritten, format, provider),
            _ => false
        };

        return result;
    }

    public readonly bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format = default,
        IFormatProvider? provider = default)
    {
        charsWritten = 0;
        var result = Version switch
        {
            IpAddressVersion.V4 => AsV4().TryFormat(destination, out charsWritten, format, provider),
            IpAddressVersion.V6 => AsV6().TryFormat(destination, out charsWritten, format, provider),
            _ => false
        };

        return result;
    }

    public readonly string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public static implicit operator IPAddress?(IpAddress address)
    {
        var result = address.Version switch
        {
            IpAddressVersion.V4 => (IPAddress)address.AsV4(),
            IpAddressVersion.V6 => (IPAddress)address.AsV6(),
            _ => null
        };

        return result;
    }

    public static explicit operator IpAddressV4(IpAddress address)
    {
        if (address.Version != IpAddressVersion.V4)
            throw new InvalidCastException();

        return address.AsV4();
    }

    public static explicit operator IpAddressV6(IpAddress address)
    {
        if (address.Version != IpAddressVersion.V6)
            throw new InvalidCastException();

        return address.AsV6();
    }

    public static implicit operator IpAddress(IPAddress? ipAddress) => new(ipAddress);
    public static implicit operator IpAddress(IpAddressV4 address) => new(address);
    public static implicit operator IpAddress(IpAddressV6 address) => new(address);
    public static bool operator ==(IpAddress a, IpAddress b) => a.Equals(b);
    public static bool operator !=(IpAddress a, IpAddress b) => !a.Equals(b);
}
