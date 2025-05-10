using System;

namespace Piranha.Sockets.Test;

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

        using var socketA = UdpSocketV4.BindLocalIp();
        var endpointA = socketA.GetSocketName();
        using var socketB = UdpSocketV4.Create();
        socketB.Send(sendBuffer, AddressV4.Local.OnPort(endpointA.Port));
        var endpointB = socketB.GetSocketName();

        var resultA = socketA.Receive(receiveBuffer, Timeout, out var originA);
        Assert.Equal(endpointB.Port, originA.Port);
        Assert.Equal(AddressV4.Local, originA.Address);
        Assert.Equal(SocketResult.Success, resultA.Result);
        Assert.Equal(sendBuffer.Length, resultA.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultA.Count), sendBuffer.AsSpan());

        socketA.Send(sendBuffer, AddressV4.Local.OnPort(endpointB.Port));
        var resultB = socketB.Receive(receiveBuffer, Timeout, out var originB);
        Assert.Equal(endpointA.Port, originB.Port);
        Assert.Equal(AddressV4.Local, originB.Address);
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

        using var socketA = UdpSocketV6.BindLocalIp();
        var endpointA = socketA.GetSocketName();
        using var socketB = UdpSocketV6.Create();
        socketB.Send(sendBuffer, AddressV6.Local.OnPort(endpointA.Port));
        var endpointB = socketB.GetSocketName();

        var resultA = socketA.Receive(receiveBuffer, Timeout, out var originA);
        Assert.Equal(endpointB.Port, originA.Port);
        Assert.Equal(AddressV6.Local, originA.Address);
        Assert.Equal(SocketResult.Success, resultA.Result);
        Assert.Equal(sendBuffer.Length, resultA.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultA.Count), sendBuffer.AsSpan());

        socketA.Send(sendBuffer, AddressV6.Local.OnPort(endpointB.Port));
        var resultB = socketB.Receive(receiveBuffer, Timeout, out var originB);
        Assert.Equal(endpointA.Port, originB.Port);
        Assert.Equal(AddressV6.Local, originB.Address);
        Assert.Equal(SocketResult.Success, resultB.Result);
        Assert.Equal(sendBuffer.Length, resultB.Count);
        Assert.Equal(receiveBuffer.AsSpan(0, resultB.Count), sendBuffer.AsSpan());
    }

    [Fact]
    public void CanSendUdpV4ToV6WhenAllowed()
    {
        var v4LocalAsV6 = (AddressV6)AddressV4.Local;
        var sendBuffer = new byte[256];
        sendBuffer.AsSpan().Fill(0xab);

        // Ensure that the amount received doesn't match by luck.
        var receiveBuffer = new byte[sendBuffer.Length * 2];

        using var socketA = UdpSocketV4.Create();

        using var socketB = UdpSocketV6.Bind(v4LocalAsV6.OnAnyPort(), true);
        var endpointB = socketB.GetSocketName();
        var destinationB = AddressV4.Local.OnPort(endpointB.Port);

        socketA.Send(sendBuffer, destinationB);
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
        var resultV4 = socketA.Receive(receiveBuffer, Timeout, out var originV4);
        Assert.Equal(AddressV4.Local, originV4.Address);
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

        using var socketA = UdpSocketV4.Create();
        using var socketB = UdpSocketV6.BindLocalIp();
        var endpointB = socketB.GetSocketName();
        var destinationB = AddressV4.Local.OnPort(endpointB.Port);

        socketA.Send(sendBuffer, destinationB);
        var result = socketB.Receive(receiveBuffer, Timeout, out var origin);
        Assert.Equal(SocketResult.Timeout, result.Result);
    }

    [Fact]
    public void CannotBindSamePortV4()
    {
        Assert.Skip("Debating whether to reuse addr by default.");
        using var socketA = UdpSocketV4.BindLocalIp();
        var endpointA = socketA.GetSocketName();

        Assert.NotEqual(0, endpointA.Port.HostValue);

        Assert.Throws<SocketException>(() =>
        {
            using var socketB = UdpSocketV4.BindLocalIp(endpointA.Port);
            _ = socketB.GetSocketName();
        });
    }

    [Fact]
    public void CannotBindSamePortV6()
    {
        Assert.Skip("Debating whether to reuse addr by default.");
        using var socketA = UdpSocketV6.BindLocalIp();
        var endpoint = socketA.GetSocketName();

        Assert.NotEqual(0, endpoint.Port.HostValue);

        Assert.Throws<SocketException>(() =>
        {
            using var socketB = UdpSocketV6.BindLocalIp(endpoint.Port);
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
                using var v4 = UdpSocketV4.BindLocalIp();
                var endpointV4 = v4.GetSocketName();
                using var v6 = UdpSocketV6.BindLocalIp(endpointV4.Port);
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
    public void CannotBindSamePortOnV4AndV6InDualMode()
    {
        Assert.Skip("Debating whether to reuse addr by default.");
        var target = ((AddressV6)AddressV4.Local).OnAnyPort();
        for (int i = 0; i < 3; ++i)
        {
            using var v6 = UdpSocketV6.Bind(target, allowV4: true);
            var endpointV6 = v6.GetSocketName();

            Assert.Throws<SocketException>(
                () =>
                {
                    using var v4 = UdpSocketV4.BindLocalIp(endpointV6.Port);
                });
        }
    }

    [Fact]
    public void UdpClientSendToUdpSocketV4()
    {
        using var server = UdpSocketV4.BindLocalIp();
        var serverEndpoint = server.GetSocketName();

        var message = "greetings"u8;
        using var client = UdpClientV4.Connect(serverEndpoint);
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
        using var server = UdpSocketV6.BindLocalIp();
        var serverEndpoint = server.GetSocketName();

        var message = "greetings"u8;
        using var client = UdpClientV6.Connect(serverEndpoint);
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
