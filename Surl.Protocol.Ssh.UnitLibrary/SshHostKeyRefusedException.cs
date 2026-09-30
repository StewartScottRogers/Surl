namespace Surl.Protocol.Ssh;

/// <summary>
/// Thrown inside the host-key readers where a file's bytes are refused, so the refusal
/// reaches <see cref="SshHostKeyFile.Read"/> from however deep it is found.
/// </summary>
/// <param name="refusal">The refusal.</param>
internal sealed class SshHostKeyRefusedException(SshHostKeyRefusal refusal) : Exception(refusal.Text)
{
    /// <summary>
    /// The refusal.
    /// </summary>
    public SshHostKeyRefusal Refusal { get; } = refusal;
}
