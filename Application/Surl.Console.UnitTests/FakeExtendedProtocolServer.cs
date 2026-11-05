using Surl.Composition;
using Surl.Protocol.Abstractions;

namespace Surl.Console;

/// <summary>
/// A test-only extended protocol server for the <c>s3</c> scheme: it answers nothing and
/// records nothing, and stands in for a <c>Surl.Extended.Protocol.*</c> server until one exists.
/// </summary>
internal sealed class FakeExtendedProtocolServer : IConnectionProtocolServer
{
    /// <summary>The scheme it claims.</summary>
    public const string Scheme = "s3";

    public IReadOnlyList<string> Schemes { get; } = [Scheme];

    public Task ServeAsync(IConnection connection, ExchangeContext context) => Task.CompletedTask;
}
