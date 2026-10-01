namespace Surl.Kerberos.TestKdc;

/// <summary>A clock that reads whatever time a test sets.</summary>
internal sealed class SettableTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
