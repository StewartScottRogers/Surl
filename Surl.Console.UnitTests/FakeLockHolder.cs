namespace Surl.Console;

/// <summary>
/// A data-directory lock holder that holds nothing and records whether it was disposed.
/// </summary>
internal sealed class FakeLockHolder : IDisposable
{
    /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
    public bool Disposed { get; private set; }

    public void Dispose() => Disposed = true;
}
