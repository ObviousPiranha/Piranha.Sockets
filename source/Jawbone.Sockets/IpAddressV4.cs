using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Jawbone.Sockets;

[StructLayout(LayoutKind.Explicit, Size = 4, Pack = 4)]
public struct IpAddressV4 : IIpAddress<IpAddressV4>
{
#pragma warning disable IDE0044
    [StructLayout(LayoutKind.Sequential)]
    [InlineArray(Length)]
    public struct ArrayU8
    {
        public const int Length = 4;
        private byte _first;
    }

    [StructLayout(LayoutKind.Sequential)]
    [InlineArray(Length)]
    public struct ArrayU16
    {
        public const int Length = 2;
        private ushort _first;
    }
#pragma warning restore IDE0044

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LinkLocalMask() => BitConverter.IsLittleEndian ? 0x0000ffff : 0xffff0000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LinkLocalSubnet() => BitConverter.IsLittleEndian ? 0x0000fea9 : 0xa9fe0000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LoopbackMask() => BitConverter.IsLittleEndian ? 0x000000ff : 0xff000000;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint LoopbackSubnet() => BitConverter.IsLittleEndian ? 0x0000007f : (uint)0x7f000000;

    public static IpAddressV4 Any => default;
    public static IpAddressV4 Local { get; } = new(127, 0, 0, 1);
    public static IpAddressV4 Broadcast { get; } = new(255, 255, 255, 255);
    public static IpAddressVersion Version => IpAddressVersion.V4;
    public static int MaxPrefixLength => 32;
    // https://en.wikipedia.org/wiki/IPv4#Link-local_addressing
    public static IpNetwork<IpAddressV4> LinkLocalNetwork => new(new IpAddressV4(169, 254, 0, 0), 16);

    public static IpAddressV4 GetMaxAddress(IpNetwork<IpAddressV4> ipNetwork)
    {
        if (ipNetwork.PrefixLength < 1)
        {
            var result = new IpAddressV4(uint.MaxValue);
            return result;
        }
        else
        {
            var mask = ~(uint.MaxValue << (MaxPrefixLength - ipNetwork.PrefixLength));
            if (BitConverter.IsLittleEndian)
                mask = BinaryPrimitives.ReverseEndianness(mask);
            var result = ipNetwork.BaseAddress | new IpAddressV4(mask);
            return result;
        }
    }

    [FieldOffset(0)]
    public ArrayU8 DataU8;

    [FieldOffset(0)]
    public ArrayU16 DataU16;

    [FieldOffset(0)]
    public uint DataU32;

    public readonly bool IsDefault => DataU32 == 0;
    public readonly bool IsLinkLocal => (DataU32 & LinkLocalMask()) == LinkLocalSubnet();
    public readonly bool IsLoopback => (DataU32 & LoopbackMask()) == LoopbackSubnet();

    public IpAddressV4(ReadOnlySpan<byte> values)
    {
        DataU32 = BitConverter.ToUInt32(values);
    }

    public IpAddressV4(byte b0, byte b1, byte b2, byte b3)
    {
        DataU8[0] = b0;
        DataU8[1] = b1;
        DataU8[2] = b2;
        DataU8[3] = b3;
    }

    internal IpAddressV4(uint address) => DataU32 = address;

    public readonly bool Equals(IpAddressV4 other) => DataU32 == other.DataU32;
    public override readonly bool Equals([NotNullWhen(true)] object? obj)
        => obj is IpAddressV4 other && Equals(other);
    public override readonly int GetHashCode() => DataU32.GetHashCode();
    public override readonly string ToString() => SpanWriter.GetString(this);

    public readonly bool TryFormat(
        Span<byte> utf8Destination,
        out int bytesWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        var writer = SpanWriter.Create(utf8Destination);
        var result =
            writer.TryWriteFormattable(DataU8[0]) &&
            writer.TryWrite((byte)'.') &&
            writer.TryWriteFormattable(DataU8[1]) &&
            writer.TryWrite((byte)'.') &&
            writer.TryWriteFormattable(DataU8[2]) &&
            writer.TryWrite((byte)'.') &&
            writer.TryWriteFormattable(DataU8[3]);
        bytesWritten = writer.Position;
        return result;
    }

    public readonly bool TryFormat(Span<byte> utf8Destination, out int bytesWritten) => TryFormat(utf8Destination, out bytesWritten, default, default);

    public readonly bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        var writer = SpanWriter.Create(destination);
        var result =
            writer.TryWriteFormattable(DataU8[0]) &&
            writer.TryWrite('.') &&
            writer.TryWriteFormattable(DataU8[1]) &&
            writer.TryWrite('.') &&
            writer.TryWriteFormattable(DataU8[2]) &&
            writer.TryWrite('.') &&
            writer.TryWriteFormattable(DataU8[3]);
        charsWritten = writer.Position;
        return result;
    }

    public readonly bool TryFormat(Span<char> destination, out int charsWritten) => TryFormat(destination, out charsWritten, default, default);

    public readonly string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    public static IpAddressV4 Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        var result = default(IpAddressV4);
        var reader = SpanReader.Create(s);
        if (!reader.TryParseByte(out result.DataU8[0]))
            throw new FormatException();
        for (int i = 1; i < 4; ++i)
        {
            if (!reader.TryMatch('.'))
                throw new FormatException();
            if (!reader.TryParseByte(out result.DataU8[i]))
                throw new FormatException();
        }

        if (!reader.AtEnd)
            throw new FormatException();

        return result;
    }

    public static IpAddressV4 Parse(ReadOnlySpan<char> s) => Parse(s, null);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out IpAddressV4 result)
    {
        var reader = SpanReader.Create(s);
        result = default;
        var succeeded =
            reader.TryParseByte(out result.DataU8[0]) &&
            reader.TryMatch('.') &&
            reader.TryParseByte(out result.DataU8[1]) &&
            reader.TryMatch('.') &&
            reader.TryParseByte(out result.DataU8[2]) &&
            reader.TryMatch('.') &&
            reader.TryParseByte(out result.DataU8[3]) &&
            reader.AtEnd;

        if (!succeeded)
            result = default;

        return succeeded;
    }

    public static bool TryParse(ReadOnlySpan<char> s, out IpAddressV4 result) => TryParse(s, null, out result);

    public static IpAddressV4 Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Parse(s.AsSpan(), provider);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out IpAddressV4 result)
    {
        return TryParse(s.AsSpan(), provider, out result);
    }
    public static IpAddressV4 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        var result = default(IpAddressV4);
        var reader = SpanReader.Create(utf8Text);
        if (!reader.TryParseByte(out result.DataU8[0]))
            throw new FormatException();
        for (int i = 1; i < 4; ++i)
        {
            if (!reader.TryMatch((byte)'.'))
                throw new FormatException();
            if (!reader.TryParseByte(out result.DataU8[i]))
                throw new FormatException();
        }

        if (!reader.AtEnd)
            throw new FormatException();

        return result;
    }

    public static IpAddressV4 Parse(ReadOnlySpan<byte> utf8Text) => Parse(utf8Text, null);

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out IpAddressV4 result)
    {
        var reader = SpanReader.Create(utf8Text);
        result = default;
        var succeeded =
            reader.TryParseByte(out result.DataU8[0]) &&
            reader.TryMatch((byte)'.') &&
            reader.TryParseByte(out result.DataU8[1]) &&
            reader.TryMatch((byte)'.') &&
            reader.TryParseByte(out result.DataU8[2]) &&
            reader.TryMatch((byte)'.') &&
            reader.TryParseByte(out result.DataU8[3]) &&
            reader.AtEnd;

        if (!succeeded)
            result = default;

        return succeeded;
    }

    public static bool TryParse(ReadOnlySpan<byte> utf8Text, out IpAddressV4 result) => TryParse(utf8Text, null, out result);

    public static IpNetwork<IpAddressV4> CreateNetwork(IpAddressV4 ipAddress, int prefixLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(prefixLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(prefixLength, 32);
        var mask = (uint)((long)uint.MaxValue << (32 - prefixLength));
        if (BitConverter.IsLittleEndian)
            mask = BinaryPrimitives.ReverseEndianness(mask);
        if ((ipAddress.DataU32 & mask) != ipAddress.DataU32)
            ThrowExceptionFor.InvalidNetwork(ipAddress, prefixLength);
        return new(ipAddress, prefixLength);
    }

    public static bool TryCreateNetwork(
        IpAddressV4 ipAddress,
        int prefixLength,
        out IpNetwork<IpAddressV4> ipNetwork)
    {
        if (prefixLength < 0 || 32 < prefixLength)
            goto failure;
        var mask = (uint)((long)uint.MaxValue << (32 - prefixLength));
        if (BitConverter.IsLittleEndian)
            mask = BinaryPrimitives.ReverseEndianness(mask);
        if ((ipAddress.DataU32 & mask) != ipAddress.DataU32)
            goto failure;
        ipNetwork = new(ipAddress, prefixLength);
        return true;
    failure:
        ipNetwork = default;
        return false;
    }

    public static bool IsInNetwork(IpAddressV4 ipAddress, IpNetwork<IpAddressV4> ipNetwork)
    {
        if (ipNetwork.PrefixLength == 0)
            return true;
        var mask = uint.MaxValue << (32 - ipNetwork.PrefixLength);
        if (BitConverter.IsLittleEndian)
            mask = BinaryPrimitives.ReverseEndianness(mask);
        var result = (ipAddress.DataU32 & mask) == ipNetwork.BaseAddress.DataU32;
        return result;
    }

    public static IpAddressV4 Create(
        byte b0 = 0,
        byte b1 = 0,
        byte b2 = 0,
        byte b3 = 0)
    {
        return new(b0, b1, b2, b3);
    }

    public static IpAddressV4 FromHostU32(uint hostValue)
    {
        var networkValue = BitConverter.IsLittleEndian ?
            BinaryPrimitives.ReverseEndianness(hostValue) :
            hostValue;
        var result = new IpAddressV4(networkValue);
        return result;
    }

    public static IpAddressV4 FromNetworkU32(uint networkValue) => new(networkValue);

    public static IUdpSocket<IpAddressV4> BindUdpSocket(
        IpEndpoint<IpAddressV4> ipEndpoint,
        SocketOptions socketOptions = default)
    {
        if (OperatingSystem.IsWindows())
            return Windows.WindowsUdpSocketV4.Bind(ipEndpoint, socketOptions);
        else if (OperatingSystem.IsMacOS())
            return Mac.MacUdpSocketV4.Bind(ipEndpoint, socketOptions);
        else if (OperatingSystem.IsLinux())
            return Linux.LinuxUdpSocketV4.Bind(ipEndpoint, socketOptions);
        else
            throw new PlatformNotSupportedException();
    }

    public static IUdpClient<IpAddressV4> ConnectUdpClient(
        IpEndpoint<IpAddressV4> ipEndpoint,
        SocketOptions socketOptions = default)
    {
        if (OperatingSystem.IsWindows())
            return Windows.WindowsUdpClientV4.Connect(ipEndpoint, socketOptions);
        if (OperatingSystem.IsMacOS())
            return Mac.MacUdpClientV4.Connect(ipEndpoint, socketOptions);
        if (OperatingSystem.IsLinux())
            return Linux.LinuxUdpClientV4.Connect(ipEndpoint, socketOptions);
        throw new PlatformNotSupportedException();
    }

    public static ITcpListener<IpAddressV4> TcpListen(
        IpEndpoint<IpAddressV4> bindEndpoint,
        int backlog,
        SocketOptions socketOptions = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(backlog);
        if (OperatingSystem.IsWindows())
            return Windows.WindowsTcpListenerV4.Listen(bindEndpoint, backlog, socketOptions);
        if (OperatingSystem.IsMacOS())
            return Mac.MacTcpListenerV4.Listen(bindEndpoint, backlog, socketOptions);
        if (OperatingSystem.IsLinux())
            return Linux.LinuxTcpListenerV4.Listen(bindEndpoint, backlog, socketOptions);
        throw new PlatformNotSupportedException();
    }

    public static ITcpClient<IpAddressV4> ConnectTcpClient(
        IpEndpoint<IpAddressV4> ipEndpoint,
        SocketOptions socketOptions = default)
    {
        if (OperatingSystem.IsWindows())
            return Windows.WindowsTcpClientV4.Connect(ipEndpoint, socketOptions);
        if (OperatingSystem.IsMacOS())
            return Mac.MacTcpClientV4.Connect(ipEndpoint, socketOptions);
        if (OperatingSystem.IsLinux())
            return Linux.LinuxTcpClientV4.Connect(ipEndpoint, socketOptions);
        throw new PlatformNotSupportedException();
    }

    public static explicit operator IpAddressV4(IPAddress ipAddress)
    {
        ArgumentNullException.ThrowIfNull(ipAddress);
        if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            throw new InvalidCastException("IPAddress instance is not IPv4.");
        var result = default(IpAddressV4);
        if (!ipAddress.TryWriteBytes(result.DataU8, out var bytesWritten) || bytesWritten != ArrayU8.Length)
            throw new InvalidCastException("Failed to write address bytes.");
        return result;
    }

    public static explicit operator IPAddress(IpAddressV4 ipAddress)
    {
        var result = new IPAddress(ipAddress.DataU32);
        return result;
    }

    public static implicit operator IpAddress(IpAddressV4 ipAddress) => new(ipAddress);

    public static explicit operator IpAddressV4(IpAddress ipAddress)
    {
        if (ipAddress.Version != Version)
            throw new InvalidCastException();

        return ipAddress.AsV4();
    }

    public static IpAddressV4 operator |(IpAddressV4 a, IpAddressV4 b) => new(a.DataU32 | b.DataU32);
    public static IpAddressV4 operator &(IpAddressV4 a, IpAddressV4 b) => new(a.DataU32 & b.DataU32);
    public static IpAddressV4 operator ^(IpAddressV4 a, IpAddressV4 b) => new(a.DataU32 ^ b.DataU32);
    public static IpAddressV4 operator ~(IpAddressV4 ipAddress) => new(~ipAddress.DataU32);

    public static bool operator ==(IpAddressV4 a, IpAddressV4 b) => a.Equals(b);
    public static bool operator !=(IpAddressV4 a, IpAddressV4 b) => !a.Equals(b);

    public static bool operator ==(IpAddressV4 a, IpAddress b) => b.Version == Version && b.AsV4().Equals(a);
    public static bool operator !=(IpAddressV4 a, IpAddress b) => b.Version != Version || !b.AsV4().Equals(a);
    public static bool operator ==(IpAddress a, IpAddressV4 b) => a.Version == Version && a.AsV4().Equals(b);
    public static bool operator !=(IpAddress a, IpAddressV4 b) => a.Version != Version || !a.AsV4().Equals(b);
}
