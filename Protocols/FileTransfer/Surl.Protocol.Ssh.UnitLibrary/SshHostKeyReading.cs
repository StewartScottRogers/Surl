namespace Surl.Protocol.Ssh;

/// <summary>
/// What reading a host-key file's bytes gave: a host key, or why not.
/// </summary>
/// <param name="Key">The host key; <see langword="null"/> when refused.</param>
/// <param name="Refusal">Why the bytes give no host key; <see langword="null"/> when a key was read.</param>
public sealed record SshHostKeyReading(SshHostKey? Key, SshHostKeyRefusal? Refusal);
