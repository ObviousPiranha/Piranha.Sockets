# Piranha.Sockets

UDP and TCP socket library for game engines!

The [TLDR](rant.md) is that the .NET socket libraries allocate way too much and support too many address families. This library focuses on the essentials.

## Design

### IP Addresses

There are two address types: `AddressV4` and `AddressV6`. Both are structs. They are very simple to use.

```csharp
var host = new AddressV4(10, 0, 0, 23);

// Lots of shortcuts.
var localhost = AddressV4.Local;

// IPv6 is a little bigger. It accepts spans.
var v6 = new AddressV6([55, 23, 11, 1, 9, 5, 22, 1, 0, 0, 0, 3, 12, 94, 201, 7]);

// Or parse it. Lots of options.
var parsedV6 = AddressV6.Parse("7f13:22e9::4000:910d", null);
```

### IP Endpoints

When you're ready to pair an address with a port, just use `Endpoint<T>` (also a struct).

```csharp
var endpointV4 = new Endpoint<AddressV4>(AddressV4.Local, 5000);
var endpointV6 = new Endpoint<AddressV6>(AddressV6.Local, 5000);

// Lots more shortcuts.
Endpoint<AddressV4> origin = Endpoint.Create(AddressV4.Local, 5000);

// Or you can use some extensions.
var host = new AddressV4(10, 0, 0, 23);
Endpoint<AddressV4> endpoint = host.OnPort(5000);
```

### UDP Sockets

Now you're ready to make a socket! All socket types are generic as they are _constrained_ to IPv4 or IPv6.

```csharp
// Create a socket and listen on port 10215. Ideal for servers.
using IUdpSocket<AddressV4> server = UdpSocketV4.BindAnyIp(10215);

// Connect a client.
Endpoint<AddressV4> origin = new AddressV4(10, 0, 0, 23).OnPort(10215);
using IUdpClient<AddressV4> client = UdpClientV4.Connect(origin);

// Create an IPv6 server and (optionally) allow interop with IPv4!
using IUdpSocket<AddressV6> serverV6 = UdpSocketV6.BindAnyIp(38555, allowV4: true);

// Connect an IPv6 client.
Endpoint<AddressV6> originV6 = myAddressV6.OnPort(38555);
using IUdpClient<AddressV6> clientV6 = UdpClientV6.Connect(originV6);
```

Sending data is very simple. The `Send` method accepts any `ReadOnlySpan<byte>`.

```csharp
var destination = AddressV4.Local.OnPort(10215);

// IUdpSocket needs a destination address.
server.Send("Hello!"u8, destination);

// IUdpClient is locked to a single address.
client.Send("Greetings!"u8);
```

Receiving data is only marginally more complex. It lets you specify a timeout in milliseconds. (Simply pick zero if you want a non-blocking call.)

```csharp
var buffer = new byte[2048];
var timeout = 1000; // One second
var result = server.Receive(buffer, timeout, out var sender);
if (0 < result.Count)
{
    var message = buffer.AsSpan(0, result.Count);
    // Handle received bytes here!
    Console.WriteLine($"Received {result.Count} bytes from host {sender}.");
}
else
{
    // Probably a timeout.
    // The field result.Result will tell you if it was a timeout or an interrupt.
}
```

### TCP Sockets

Create a TCP listener to get started.

```csharp
var bindEndpoint = AddressV4.Local.OnPort(5555);
using ITcpListener<AddressV4> listener = TcpListenerV4.Listen(bindEndpoint, 4); // Backlog of 4 pending connections.
```

Connect with a client.

```csharp
using ITcpClient<AddressV4> client = TcpClientV4.Connect(serverEndpoint);
```

Accept the connection into another `ITcpClient<T>` on the server side.

```csharp
var timeout = 1000; // One second
using ITcpClient<AddressV4> server = listener.Accept(timeout);
if (server is null)
{
    // Null object just means it times out or was interrupted.
}
```

Communicate back and forth with TCP goodness. All TCP sockets enable `TCP_NODELAY` automatically.

```csharp
client.Send("HTTP shenanigans"u8);

var result = server.Receive(buffer, timeout);
if (0 < result.Count)
{
    // Conquer the world here.
}
```
