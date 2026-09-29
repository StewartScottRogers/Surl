# ADR-0004 — The listener seam and the exchange context

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

Rule 2 of the product overview's "Architecture" says a protocol server receives its
transport from an injected seam and never constructs a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener`. Only `Surl.Networking` constructs those. The
seam, and what a protocol server is told about each exchange, are the first contracts of
Phase 1: BL-005 writes them into `Surl.Protocol.Abstractions.UnitLibrary`, BL-011 and
BL-031 implement the listeners, BL-015 and BL-032 build the serving engine on them,
BL-016 receives the exchange log and BL-017 writes the first protocol server against them.

The constraints the shape has to meet:

- **Base class library only.** Every type used here is in the `Microsoft.NETCore.App`
  shared framework (`System.Net.EndPoint`, `System.Net.IPEndPoint`, `TimeProvider`,
  `CancellationToken`, `ValueTask`, `Memory<byte>`). No package, and no
  `Microsoft.AspNetCore.*` type such as `ConnectionContext` or `IDuplexPipe` from
  `Microsoft.AspNetCore.Connections.Abstractions`.
- **Native AOT.** No reflection, no assembly scanning; servers are found by explicit
  construction.
- **Tests with no network.** Protocol tests replay a byte script measured from pinned
  upstream curl (ADR-0003) through an in-memory connection, and every protocol test
  project can use it without referencing another test project.
- **Abstractions references nothing** (its `CLAUDE.md`, and
  `ProtocolIsolationTests.Abstractions_ReferencesNothing`).
- **Every datagram protocol upstream curl speaks is TFTP**, and a TFTP server answers each
  transfer from a new port (RFC 1350, section 4).
- **The same server serves the secure variant** (ADR-0002, "Consequences"): `https` is the
  HTTP server over a secured connection. How a connection is secured, or upgraded in the
  middle of an exchange, is BL-002's ADR; this ADR only has to leave room for it.

Alternatives considered and rejected:

- **Hand protocol servers a `System.IO.Stream`.** It is in the BCL and `SslStream` is one,
  but `Stream` has no half-close, no abort and no endpoints, and a server that received
  one could wrap it in anything, including an `SslStream`. A narrow interface says exactly
  what a server may do to its transport.
- **`System.IO.Pipelines`.** Part of the shared framework, but its contract (reader and
  writer buffer ownership, `AdvanceTo`) is larger than any Phase 1 server needs and
  harder to fake by hand. A server that wants a `PipeReader` can build one over the
  connection itself later.
- **One `IProtocolServer` with a transport enum and both a connection and a flow method.**
  Every server would carry a method it cannot honour. Two interfaces make the kind of
  transport a compile-time fact.
- **Put the in-memory connection in a shared test project.** Test projects referencing a
  test-support library is a second kind of project for one class; putting it beside the
  contract it fakes keeps it covered by `Surl.Protocol.Abstractions.UnitTests` and usable
  from every protocol test project through the reference they already have.

## Decision

All types below live in `Surl.Protocol.Abstractions.UnitLibrary`, namespace
`Surl.Protocol.Abstractions`, one public type per file, unless a section says otherwise.
Endpoints are typed `System.Net.EndPoint` so that a later listen URL for a Unix domain
socket (upstream curl's `--unix-socket`) fits without changing a contract; in Phase 1
every endpoint is an `IPEndPoint`.

### 1. `ListenUrl` - what `surl` was asked to listen on

`public sealed record ListenUrl(string Scheme, string Host, int Port)`, produced by
`Surl.Cli` (BL-013) and consumed by `Surl.Core` and `Surl.Networking`.

| Member | Meaning |
| --- | --- |
| `string Scheme` | The scheme, lower-cased by `Surl.Cli` (`http`, `https`, `tftp`). Picks the protocol server. |
| `string Host` | The host to bind, as written, with IPv6 brackets removed: an IPv4 literal, an IPv6 literal, or a host name. |
| `int Port` | The port asked for, 0 to 65535. `Surl.Cli` fills in the scheme's default when the URL names none (BL-003 decides the defaults). 0 asks for an ephemeral port. |
| `int? BoundPort` | `init`-only, `null` until a listener has bound. The port actually bound: equal to `Port` unless `Port` was 0. |
| `ListenUrl WithBoundPort(int boundPort)` | A copy with `BoundPort` set. Throws `ArgumentOutOfRangeException` outside 1 to 65535. |

Record equality compares all four values, so a URL before binding and the same URL after
binding are different values. The listener, not the caller, calls `WithBoundPort`. The
status line (BL-016) and the conformance tests (BL-020) read `BoundPort`.

A path in the listen URL, and what it means for each scheme, is BL-003's decision; if it
needs one, a `string Path` member is added then, with its own ADR line.

### 2. `IConnection` - a stream-oriented connection

```csharp
public interface IConnection : IAsyncDisposable
{
    EndPoint LocalEndPoint { get; }
    EndPoint RemoteEndPoint { get; }
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
    ValueTask CompleteWritesAsync(CancellationToken cancellationToken);
    void Abort();
}
```

- **Reading.** `ReadAsync` waits for at least one byte and returns how many it copied,
  never more than `buffer.Length`. It returns 0 once the peer has half-closed (sent FIN)
  and every byte before it has been read, and 0 again on every later call. It never
  returns 0 for an empty buffer by accident: an empty buffer throws
  `ArgumentException`. A read cut off by `cancellationToken` throws
  `OperationCanceledException`.
- **Writing.** `WriteAsync` returns once every byte has been handed to the transport. A
  write cut off by `cancellationToken` throws `OperationCanceledException`. Writing after
  `CompleteWritesAsync` throws `InvalidOperationException`.
- **Half-close.** `CompleteWritesAsync` flushes and sends FIN
  (`Socket.Shutdown(SocketShutdown.Send)` in `Surl.Networking`); the connection can still
  be read. Calling it twice is harmless. This is how a server ends a response whose end is
  marked by the close of the connection (HTTP/1.0 without `Content-Length`, Gopher, DICT
  after the reply) while still draining what the client sends.
- **Abort.** `Abort` drops the connection at once with a reset (`LingerState` of 0, then
  close, in `Surl.Networking`); unsent bytes are discarded. Pending and later reads and
  writes throw `IOException`. Calling it twice is harmless. A server aborts when a request
  is malformed beyond answering; the serving engine aborts when a server throws.
- **Close.** `DisposeAsync` closes gracefully: it completes writes if the server did not,
  then releases the transport. The serving engine disposes every connection when the
  server's `ServeAsync` returns, so a server never has to.
- **I/O failures.** A reset by the peer or any other transport failure surfaces as
  `IOException` from `ReadAsync` or `WriteAsync`, whatever the transport; `Surl.Networking`
  translates `SocketException` into it. Servers catch `IOException` only where they have
  something useful to do; otherwise it ends the exchange (section 5).
- **TLS.** A connection may already be secured when a server receives it (for `https`),
  and a server may need to upgrade one (`STARTTLS`, FTP `AUTH TLS`). BL-002's ADR adds that
  to this contract; `IConnection` itself always carries plaintext.

### 3. `IDatagramFlow` - a datagram flow, for TFTP

Only the shape is fixed here. BL-031 and BL-032 implement it with TFTP; nothing before
them needs it.

```csharp
public interface IDatagramFlow : IAsyncDisposable
{
    EndPoint LocalEndPoint { get; }
    EndPoint RemoteEndPoint { get; }
    ReadOnlyMemory<byte> FirstDatagram { get; }
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
    ValueTask SendAsync(ReadOnlyMemory<byte> datagram, CancellationToken cancellationToken);
    ValueTask MoveToNewLocalPortAsync(CancellationToken cancellationToken);
}
```

- A flow is everything one remote endpoint sends to one local endpoint, starting with the
  datagram that opened it (`FirstDatagram`, a TFTP `RRQ` or `WRQ`, arriving on the listen
  port).
- `ReceiveAsync` returns the next datagram from `RemoteEndPoint` to the flow's current
  `LocalEndPoint`, one datagram per call, whole. Datagrams from any other endpoint never
  reach the flow. Waiting for a datagram that does not come is cut off by
  `cancellationToken`; retransmission timing is the server's, measured on the exchange
  context's `TimeProvider`.
- `SendAsync` sends one datagram to `RemoteEndPoint` from the current `LocalEndPoint`.
- `MoveToNewLocalPortAsync` binds a fresh ephemeral port on the same local address and
  makes it the flow's `LocalEndPoint`: every later send leaves from it and every later
  receive arrives on it. This is RFC 1350's new transfer identifier. A TFTP server calls
  it before its first reply.
- `DisposeAsync` releases any port the flow bound. The listen port stays with the
  listener.

The in-memory datagram flow for TFTP's tests is added to Abstractions with the TFTP
server (BL-037), next to `InMemoryConnection` and on the same terms (section 7).

### 4. `IProtocolServer` and how servers are registered

```csharp
public interface IProtocolServer
{
    IReadOnlyList<string> Schemes { get; }
}

public interface IConnectionProtocolServer : IProtocolServer
{
    Task ServeAsync(IConnection connection, ExchangeContext context);
}

public interface IDatagramProtocolServer : IProtocolServer
{
    Task ServeAsync(IDatagramFlow flow, ExchangeContext context);
}
```

- **Schemes.** `Schemes` lists every lower-case scheme the server answers, secure variants
  included: the HTTP server lists `http` and `https`. The list never changes after
  construction.
- **Kind of transport.** A server takes connections by implementing
  `IConnectionProtocolServer`, or datagram flows by implementing `IDatagramProtocolServer`.
  It implements exactly one. The serving engine asks the listener factory for the matching
  kind of listener with a type test (`server is IDatagramProtocolServer`), which is AOT
  safe.
- **One `ServeAsync` call is one exchange.** It returns when the exchange is over. The
  server owns the exchange from the first byte to its last write; the engine owns the
  connection's lifetime (section 2, "Close").
- **Registration.** `Surl.Console`'s composition root constructs every protocol server
  with `new`, passing its dependencies (the content store, the `TimeProvider`), and hands
  the serving engine an `IReadOnlyList<IProtocolServer>`. The engine (`Surl.Core`, BL-015)
  builds an ordinal, lower-case scheme-to-server table from `Schemes` and throws
  `ArgumentException` if two servers claim one scheme. There is no attribute, no
  reflection and no assembly scan. A listen URL whose scheme is in no server's `Schemes`
  is refused before any listener starts, with the exit code BL-001's ADR assigns to an
  unsupported scheme.

### 5. `ExchangeContext` - what a server is told about one exchange

```csharp
public sealed record ExchangeContext(
    long ExchangeId,
    ListenUrl ListenUrl,
    EndPoint LocalEndPoint,
    EndPoint RemoteEndPoint,
    IExchangeLog Log,
    TimeProvider TimeProvider,
    CancellationToken CancellationToken)
{
    public string Scheme => ListenUrl.Scheme;
}
```

| Member | Meaning |
| --- | --- |
| `ExchangeId` | A number the engine gives each exchange, from 1, unique for the life of the process. The verbose log uses it to tell concurrent exchanges apart. |
| `ListenUrl` | The listen URL whose listener accepted the exchange, with `BoundPort` set. |
| `Scheme` | `ListenUrl.Scheme`: which of the server's `Schemes` this exchange is (`http` or `https`). |
| `LocalEndPoint`, `RemoteEndPoint` | The connection's or flow's endpoints when the exchange began. |
| `Log` | Where the exchange's events go (below). |
| `TimeProvider` | The one clock. Every timeout and every date a server writes (HTTP `Date`, FTP listings) comes from it. |
| `CancellationToken` | Cancelled when the engine gives up on the exchange: at shutdown, once the grace period on `TimeProvider` has run out (BL-015). The server passes it to every `IConnection` and `IDatagramFlow` call. |

The engine creates a fresh context for every exchange. Per-exchange limits and idle
timeouts are BL-024's decision; they reach the server through this token or a later
member, never through a clock of its own.

#### How a server reports exchange events

```csharp
public interface IExchangeLog
{
    void BytesReceived(ReadOnlySpan<byte> bytes);
    void BytesSent(ReadOnlySpan<byte> bytes);
    void Note(string text);
}

public interface IExchangeLogFactory
{
    IExchangeLog Create(long exchangeId, EndPoint remoteEndPoint);
}
```

- **Bytes in and bytes out are reported for the server, not by it.** The serving engine
  wraps each connection and flow in a recording decorator (`Surl.Core`, internal) that
  calls `BytesReceived` with every byte `ReadAsync` or `ReceiveAsync` returned and
  `BytesSent` with every byte written or sent, before handing it to the server. A server
  cannot forget to log a byte, and the log shows exactly what crossed the transport, as
  plaintext. BL-002's upgrade contract keeps that true after a TLS upgrade.
- **Notes are the server's.** A server calls `context.Log.Note(...)` for what the bytes do
  not say: a request it refused and why, the file a path mapped to, a TLS upgrade. The
  engine adds its own notes: exchange opened, exchange closed, server threw (with the
  exception's message).
- **`Surl.Output` implements both interfaces** (BL-016), and renders each call as the
  verbose-log line BL-003's ADR specifies. With verbose off it hands out a log that writes
  nothing. `Surl.Console` passes the factory to the engine, which calls `Create` once per
  exchange.
- The span passed to `BytesReceived` and `BytesSent` is valid only during the call; an
  implementation that keeps bytes copies them.

#### When a server throws

An exception escaping `ServeAsync` ends that exchange only: the engine notes it, calls
`Abort` on the connection (or disposes the flow), and keeps accepting. An
`OperationCanceledException` caused by `context.CancellationToken` is a normal end at
shutdown, not a failure.

### 6. The listeners, and a failure to bind

```csharp
public interface IListenerFactory
{
    ValueTask<IConnectionListener> StartConnectionListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken);
    ValueTask<IDatagramListener> StartDatagramListenerAsync(ListenUrl listenUrl, CancellationToken cancellationToken);
}

public interface IConnectionListener : IAsyncDisposable
{
    ListenUrl ListenUrl { get; }
    IReadOnlyList<EndPoint> BoundEndPoints { get; }
    ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken);
}

public interface IDatagramListener : IAsyncDisposable
{
    ListenUrl ListenUrl { get; }
    IReadOnlyList<EndPoint> BoundEndPoints { get; }
    ValueTask<IDatagramFlow> AcceptFlowAsync(CancellationToken cancellationToken);
}
```

- A `Start…` method returns only once every address is bound and listening. The
  returned listener's `ListenUrl` carries `BoundPort`.
- **Addresses.** An IP literal binds that address. A host name is resolved with
  `System.Net.Dns` and every address it resolves to is bound, on one port: with port 0,
  the port the first address got is asked for on the rest. `localhost` therefore listens
  on both `127.0.0.1` and `::1`, as upstream curl may try either.
- `AcceptAsync` waits for the next connection; `AcceptFlowAsync` for the first datagram
  from a remote endpoint that has no open flow. Cancelling throws
  `OperationCanceledException`. `DisposeAsync` stops listening; connections and flows
  already handed out are unaffected.
- `Surl.Networking` implements `IListenerFactory` (BL-011 for connections, BL-031 for
  flows). `Surl.Core`'s tests fake it.

**A failure to bind** is thrown from `Start…` as `ListenerBindException`, an Abstractions
exception:

| Member | Meaning |
| --- | --- |
| `ListenUrl ListenUrl` | The listen URL that could not be bound. |
| `EndPoint? EndPoint` | The address that failed, or `null` when the host did not resolve. |
| `ListenerBindFailure Failure` | Why, as an Abstractions enum: `AddressInUse`, `AddressNotAvailable`, `PermissionDenied`, `HostNotFound`, `Other`. |
| `InnerException` | The `SocketException` behind it, kept for the verbose log. |

`Surl.Networking` maps `SocketError` to `ListenerBindFailure` (`AddressAlreadyInUse`,
`AddressNotAvailable`, `AccessDenied`, `HostNotFound` and `NoData`; everything else is
`Other`), releases whatever it had bound for that URL, and throws. `Surl.Core` catches it,
stops every listener it had already started, and maps `Failure` to the `SurlExitCode`
BL-001's ADR assigns. Networking never picks an exit code: the mapping from what went wrong
to what the process returns lives in one place, the engine.

### 7. `InMemoryConnection` - the byte-script replay for protocol tests

`InMemoryConnection` is a public sealed class in `Surl.Protocol.Abstractions.UnitLibrary`
implementing `IConnection`, beside the contract it fakes. Every
`Surl.Protocol.*.UnitTests` project reaches it through its protocol server's reference to
Abstractions, so no test project references another. It is production code, so
`Surl.Protocol.Abstractions.UnitTests` holds it to the coverage gates (BL-005). It opens
nothing and constructs no transport type.

- Constructed with the inbound byte script as an ordered list of chunks
  (`IEnumerable<ReadOnlyMemory<byte>>`), optional local and remote endpoints (default
  `127.0.0.1:80` and `127.0.0.1:50000`), and whether the peer half-closes when the script
  is exhausted (default yes).
- `ReadAsync` returns bytes from the current chunk only, never joining two chunks in one
  read, so a test controls how a request is split across reads. Once the script is
  exhausted it returns 0 if the peer half-closes, or waits until cancelled if not.
- Every byte written is recorded in order: `WrittenBytes` returns a copy of all of them.
- `WritesCompleted` is true after `CompleteWritesAsync`, `Aborted` after `Abort`,
  `Disposed` after `DisposeAsync`, each with the behaviour section 2 defines, so a test
  can assert how the server ended the exchange.
- An already cancelled token makes every call throw `OperationCanceledException`
  (`ThrowIfCancellationRequested`), which the MSTest rules require over
  `TaskCanceledException`.

Next to it, `RecordingExchangeLog` implements `IExchangeLog` and records every call in
order, so a protocol test can build an `ExchangeContext` and assert the notes a server
wrote without writing its own fake.

### 8. How `Surl.Networking` meets the coverage gate

The fast tests open no socket, and every `.UnitLibrary` is held to 100% line and branch
coverage. `Surl.Networking` meets both this way:

- **Everything that decides is pure and fast-tested.** Choosing addresses from a host,
  mapping `SocketError` to `ListenerBindFailure`, demultiplexing datagrams by remote
  endpoint, flow lifetime and the adapters' state rules (write after half-close, calls
  after abort) live in classes that take no socket, and fast tests cover them fully.
- **Only the members that call a socket are excluded.** Each carries
  `[ExcludeFromCodeCoverage(Justification = "...")]` with a comment directly above it
  saying which socket call it wraps, which is what `Measure-CodeQuality.ps1` asks of an
  exclusion. Such a member holds no branch beyond the call and its hand-off to the tested
  logic; a member that grows a decision has the decision extracted.
- **The excluded members are exercised by `[TestCategory("Integration")]` tests** on
  `127.0.0.1` and `[::1]` with port 0, never a fixed port, because lanes and tests run in
  parallel.

No other library needs an exclusion for transport reasons: `Surl.Core` and every protocol
server see only the interfaces above.

## Consequences

- BL-005 adds, to `Surl.Protocol.Abstractions.UnitLibrary`: `ListenUrl`, `IConnection`,
  `IDatagramFlow`, `IProtocolServer`, `IConnectionProtocolServer`,
  `IDatagramProtocolServer`, `ExchangeContext`, `IExchangeLog`, `IExchangeLogFactory`,
  `IListenerFactory`, `IConnectionListener`, `IDatagramListener`, `ListenerBindException`,
  `ListenerBindFailure`, `InMemoryConnection` and `RecordingExchangeLog`. The in-memory
  datagram flow waits for BL-037.
- BL-011 maps `SocketError` to `ListenerBindFailure`, not to `SurlExitCode`; where BL-011's
  text says Networking maps to the exit code, this ADR wins, and `Surl.Core` (BL-015) maps
  the failure to the code.
- Abstractions gains no reference: every type above is in the shared framework.
- A protocol server's tests need nothing but Abstractions and the byte script, so none of
  them is ever `Integration`.
- Changing any of these contracts touches every protocol server and the engine; a change
  is a new ADR that supersedes this one.
