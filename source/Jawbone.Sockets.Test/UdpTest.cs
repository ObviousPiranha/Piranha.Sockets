using System;

namespace Jawbone.Sockets.Test;

public class UdpTest
{
    private const int Timeout = 1000;
    private readonly ITestOutputHelper _output;

    public UdpTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SendAndReceiveUdpV4()
    {
        var sendBuffer = new byte[256];
        sendBuffer.AsSpan().Fill(0xab);

        // Ensure that the amount received doesn't match by luck.
        var receiveBuffer = new byte[sendBuffer.Length * 2];

        using var socketA = UdpSocket.BindLocalIpV4();
        var endpointA = socketA.GetSocketName();
        using var socketB = UdpClient.Connect(endpointA);
        socketB.Send(sendBuffer);
        var endpointB = socketB.GetSocketName();

        var resultA = socketA.Receive(receiveBuffer, Timeout, out var originA);
        Assert.Equal(endpointB.Port, originA.Port);
        Assert.Equal(IpAddressV4.Local, originA.Address);
        Assert.Equal(SocketResult.Success, resultA.Result);
        Assert.Equal(sendBuffer.Length, resultA.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultA.Count), sendBuffer.AsSpan());

        socketA.Send(sendBuffer, IpAddressV4.Local.OnPort(endpointB.Port));
        var resultB = socketB.Receive(receiveBuffer, Timeout);
        Assert.Equal(SocketResult.Success, resultB.Result);
        Assert.Equal(sendBuffer.Length, resultB.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultB.Count), sendBuffer.AsSpan());
    }

    [Fact]
    public void SendAndReceiveUdpV6()
    {
        var sendBuffer = new byte[256];
        sendBuffer.AsSpan().Fill(0xab);

        // Ensure that the amount received doesn't match by luck.
        var receiveBuffer = new byte[sendBuffer.Length * 2];

        using var socketA = UdpSocket.BindLocalIpV6();
        var endpointA = socketA.GetSocketName();
        using var socketB = UdpClient.Connect(endpointA);
        socketB.Send(sendBuffer);
        var endpointB = socketB.GetSocketName();

        var resultA = socketA.Receive(receiveBuffer, Timeout, out var originA);
        Assert.Equal(endpointB.Port, originA.Port);
        Assert.Equal(IpAddressV6.Local, originA.Address);
        Assert.Equal(SocketResult.Success, resultA.Result);
        Assert.Equal(sendBuffer.Length, resultA.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultA.Count), sendBuffer.AsSpan());

        socketA.Send(sendBuffer, IpAddressV6.Local.OnPort(endpointB.Port));
        var resultB = socketB.Receive(receiveBuffer, Timeout);
        Assert.Equal(SocketResult.Success, resultB.Result);
        Assert.Equal(sendBuffer.Length, resultB.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultB.Count), sendBuffer.AsSpan());
    }

    [Fact]
    public void CanSendUdpV4ToV6WhenAllowed()
    {
        var v4LocalAsV6 = (IpAddressV6)IpAddressV4.Local;
        var sendBuffer = new byte[256];
        sendBuffer.AsSpan().Fill(0xab);

        // Ensure that the amount received doesn't match by luck.
        var receiveBuffer = new byte[sendBuffer.Length * 2];


        using var socketB = UdpSocket.Bind(v4LocalAsV6.OnAnyPort(), SocketOptions.EnableDualMode);
        var endpointB = socketB.GetSocketName();
        var destinationB = IpAddressV4.Local.OnPort(endpointB.Port);

        using var socketA = UdpClient.Connect(destinationB);
        socketA.Send(sendBuffer);
        var endpointA = socketA.GetSocketName();
        var destinationA = v4LocalAsV6.OnPort(endpointA.Port);
        var resultV6 = socketB.Receive(receiveBuffer, Timeout, out var originV6);
        var debug1 = v4LocalAsV6.ToString();
        var debug2 = originV6.ToString();
        var debug3 = endpointB.ToString();
        Assert.Equal(v4LocalAsV6, originV6.Address);
        Assert.Equal(SocketResult.Success, resultV6.Result);
        Assert.Equal(sendBuffer.Length, resultV6.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultV6.Count), sendBuffer.AsSpan());

        receiveBuffer.AsSpan().Clear();
        Assert.False(receiveBuffer.AsSpan(0, resultV6.Count).SequenceEqual(sendBuffer));

        socketB.Send(sendBuffer, destinationA);
        var resultV4 = socketA.Receive(receiveBuffer, Timeout);
        Assert.Equal(SocketResult.Success, resultV4.Result);
        Assert.Equal(sendBuffer.Length, resultV4.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultV4.Count), sendBuffer.AsSpan());
    }

    [Fact]
    public void CannotSendUdpV4ToV6WhenDisallowed()
    {
        var sendBuffer = new byte[256];
        sendBuffer.AsSpan().Fill(0xab);

        // Ensure that the amount received doesn't match by luck.
        var receiveBuffer = new byte[sendBuffer.Length * 2];

        using var socketB = UdpSocket.BindLocalIpV6();
        var endpointB = socketB.GetSocketName();
        var destinationB = IpAddressV4.Local.OnPort(endpointB.Port);

        using var socketA = UdpClient.Connect(destinationB);
        socketA.Send(sendBuffer);
        var result = socketB.Receive(receiveBuffer, Timeout, out var origin);
        Assert.Equal(SocketResult.Timeout, result.Result);
    }

    [Fact]
    public void AddressReuseDisabled_CannotBindSamePortV4()
    {
        using var socketA = UdpSocket.BindLocalIpV4(SocketOptions.DoNotReuseAddress);
        var endpointA = socketA.GetSocketName();

        Assert.NotEqual(0, endpointA.Port.HostValue);

        Assert.Throws<SocketException>(() =>
        {
            using var socketB = UdpSocket.BindLocalIpV4(endpointA.Port, SocketOptions.DoNotReuseAddress);
            _ = socketB.GetSocketName();
        });
    }

    [Fact]
    public void AddressReuseDisabled_CannotBindSamePortV6()
    {
        using var socketA = UdpSocket.BindLocalIpV6(SocketOptions.DoNotReuseAddress);
        var endpoint = socketA.GetSocketName();

        Assert.NotEqual(0, endpoint.Port.HostValue);

        Assert.Throws<SocketException>(() =>
        {
            using var socketB = UdpSocket.BindLocalIpV6(endpoint.Port, SocketOptions.DoNotReuseAddress);
            _ = socketB.GetSocketName();
        });
    }

    [Fact]
    public void CanBindSamePortOnV4AndV6()
    {
        // Allow up to three attempts for really unlucky port selection.
        for (int i = 0; i < 3; ++i)
        {
            try
            {
                using var v4 = UdpSocket.BindLocalIpV4();
                var endpointV4 = v4.GetSocketName();
                using var v6 = UdpSocket.BindLocalIpV6(endpointV4.Port);
                var endpointV6 = v6.GetSocketName();
                Assert.Equal(endpointV4.Port, endpointV6.Port);
                _output.WriteLine($"Bound on port {endpointV4.Port}.");
                return;
            }
            catch
            {
                _output.WriteLine($"Bind #{i + 1} was unsuccessful.");
            }
        }

        Assert.Fail("Ran out of retries. Unable to bind V4 and V6 to same port.");
    }

    [Fact]
    public void AddressReuseDisabled_CannotBindSamePortOnV4AndV6InDualMode()
    {
        var target = ((IpAddressV6)IpAddressV4.Local).OnAnyPort();
        for (int i = 0; i < 3; ++i)
        {
            using var v6 = UdpSocket.Bind(target, SocketOptions.EnableDualMode | SocketOptions.DoNotReuseAddress);
            var endpointV6 = v6.GetSocketName();

            Assert.Throws<SocketException>(
                () =>
                {
                    using var v4 = UdpSocket.BindLocalIpV4(endpointV6.Port, SocketOptions.DoNotReuseAddress);
                });
        }
    }

    [Fact]
    public void UdpClientSendToUdpSocketV4()
    {
        using var server = UdpSocket.BindLocalIpV4();
        var serverEndpoint = server.GetSocketName();

        var message = "greetings"u8;
        using var client = UdpClient.Connect(serverEndpoint);
        var clientEndpoint = client.GetSocketName();
        var clientSendResult = client.Send(message);
        Assert.Equal(message.Length, clientSendResult.Count);

        Span<byte> buffer = new byte[64];
        var serverReceiveResult = server.Receive(buffer, Timeout, out var origin);
        Assert.Equal(SocketResult.Success, serverReceiveResult.Result);
        Assert.Equal(origin, clientEndpoint);
        Assert.Equal(message, buffer[..serverReceiveResult.Count]);

        var message2 = "salutations"u8;
        var serverSendResult = server.Send(message2, clientEndpoint);
        Assert.Equal(message2.Length, serverSendResult.Count);

        var clientReceiveResult = client.Receive(buffer, Timeout);
        Assert.Equal(SocketResult.Success, clientReceiveResult.Result);
        Assert.Equal(message2, buffer[..clientReceiveResult.Count]);
    }

    [Fact]
    public void UdpClientSendToUdpSocketV6()
    {
        using var server = UdpSocket.BindLocalIpV6();
        var serverEndpoint = server.GetSocketName();

        var message = "greetings"u8;
        using var client = UdpClient.Connect(serverEndpoint);
        var clientEndpoint = client.GetSocketName();
        var clientSendResult = client.Send(message);
        Assert.Equal(message.Length, clientSendResult.Count);

        Span<byte> buffer = new byte[64];
        var serverReceiveResult = server.Receive(buffer, Timeout, out var origin);
        Assert.Equal(SocketResult.Success, serverReceiveResult.Result);
        Assert.Equal(origin, clientEndpoint);
        Assert.Equal(message, buffer[..serverReceiveResult.Count]);

        var message2 = "salutations"u8;
        var serverSendResult = server.Send(message2, clientEndpoint);
        Assert.Equal(message2.Length, serverSendResult.Count);

        var clientReceiveResult = client.Receive(buffer, Timeout);
        Assert.Equal(SocketResult.Success, clientReceiveResult.Result);
        Assert.Equal(message2, buffer[..clientReceiveResult.Count]);
    }
}
