using Surl.Tls13;

namespace Surl.Tls13Stream;

/// <summary>
/// One TLS 1.3 connection's record layer over a transport: plaintext records until traffic secrets
/// are installed, protected records after (RFC 8446 section 5), chunked to 2^14 bytes, written one
/// record at a time under a lock.
/// </summary>
internal sealed class Tls13Connection(Stream transport, Tls13RecordReader reader) : IDisposable
{
    private const int MaximumContentLength = 1 << 14;
    private const byte FatalAlertLevel = 2;
    private const byte WarningAlertLevel = 1;

    private static readonly byte[] KeyUpdateNotRequested = [24, 0, 0, 1, 0];

    private readonly SemaphoreSlim writeLock = new(1, 1);
    private TlsRecordProtector? protector;
    private TlsRecordUnprotector? unprotector;

    /// <summary>Protects what is written next with the server secret and unprotects what is read next with the client secret.</summary>
    internal void InstallKeys(TlsCipherSuite cipherSuite, TlsTrafficSecrets secrets)
    {
        protector = new TlsRecordProtector(cipherSuite, secrets.Server);
        unprotector = new TlsRecordUnprotector(cipherSuite, secrets.Client);
    }

    /// <summary>
    /// Reads the next record's content and real content type. With <paramref name="skipCompatibilityChangeCipherSpec"/>
    /// a plaintext <c>change_cipher_spec</c> record of one byte <c>01</c> is skipped (RFC 8446 appendix D.4).
    /// </summary>
    /// <returns>The content, or null when the transport ended between two records.</returns>
    internal async ValueTask<TlsInnerPlaintext?> ReadAsync(bool skipCompatibilityChangeCipherSpec, CancellationToken cancellationToken)
    {
        while (true)
        {
            var record = await reader.ReadRecordAsync(cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                return null;
            }

            if (record[0] == (byte)TlsContentType.ChangeCipherSpec && skipCompatibilityChangeCipherSpec)
            {
                RequireCompatibilityChangeCipherSpec(record);
                continue;
            }

            return unprotector is null
                ? new TlsInnerPlaintext((TlsContentType)record[0], record[Tls13RecordReader.HeaderLength..])
                : unprotector.Unprotect(record);
        }
    }

    /// <summary>Writes <paramref name="content"/> as records of <paramref name="contentType"/>, protected once keys are installed.</summary>
    internal async Task WriteAsync(TlsContentType contentType, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var offset = 0; offset < content.Length; offset += MaximumContentLength)
            {
                var chunk = content.Slice(offset, Math.Min(MaximumContentLength, content.Length - offset));
                await transport.WriteAsync(Seal(contentType, chunk.Span), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }

    /// <summary>Writes a fatal alert, ignoring a connection that is already gone: nobody is left to tell.</summary>
    internal Task TryWriteFatalAlertAsync(TlsAlert alert, CancellationToken cancellationToken) =>
        TryWriteAlertAsync(FatalAlertLevel, alert, cancellationToken);

    /// <summary>
    /// Waits for <paramref name="operation"/>; when it fails with a <see cref="TlsAlertException"/>, writes that
    /// fatal alert first and then lets the failure surface from the operation's own task.
    /// </summary>
    internal async Task WriteFatalAlertOnFailureAsync(Task operation, CancellationToken cancellationToken)
    {
        TlsAlertException failure;
        try
        {
            await operation.ConfigureAwait(false);
            return;
        }
        catch (TlsAlertException alert)
        {
            failure = alert;
        }

        await TryWriteFatalAlertAsync(failure.Alert, cancellationToken).ConfigureAwait(false);
        await operation.ConfigureAwait(false);
    }

    /// <summary>Writes <c>close_notify</c>, ignoring a connection that is already gone.</summary>
    internal Task TryWriteCloseNotifyAsync(CancellationToken cancellationToken) =>
        TryWriteAlertAsync(WarningAlertLevel, TlsAlert.CloseNotify, cancellationToken);

    /// <summary>Writes <c>close_notify</c>; an <see cref="IOException"/> reports a connection that is gone.</summary>
    internal Task WriteCloseNotifyAsync(CancellationToken cancellationToken) =>
        WriteAsync(TlsContentType.Alert, new byte[] { WarningAlertLevel, (byte)TlsAlert.CloseNotify }, cancellationToken);

    /// <summary>The client sent a KeyUpdate: its next records use the next secret.</summary>
    internal void UpdateReadKeys() => unprotector!.UpdateKeys();

    /// <summary>Answers a KeyUpdate that requested one: sends ours with <c>update_not_requested</c>, then switches to the next secret (RFC 8446 section 4.6.3).</summary>
    internal async Task WriteKeyUpdateAsync(CancellationToken cancellationToken)
    {
        await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await transport.WriteAsync(Seal(TlsContentType.Handshake, KeyUpdateNotRequested), cancellationToken).ConfigureAwait(false);
            protector!.UpdateKeys();
        }
        finally
        {
            writeLock.Release();
        }
    }

    public void Dispose()
    {
        transport.Dispose();
        writeLock.Dispose();
    }

    private async Task TryWriteAlertAsync(byte level, TlsAlert alert, CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(TlsContentType.Alert, new byte[] { level, (byte)alert }, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    private byte[] Seal(TlsContentType contentType, ReadOnlySpan<byte> content) =>
        protector is not null
            ? protector.Protect(contentType, content)
            : [(byte)contentType, 3, 3, (byte)(content.Length >> 8), (byte)content.Length, .. content];

    private static void RequireCompatibilityChangeCipherSpec(byte[] record)
    {
        if (record.Length != Tls13RecordReader.HeaderLength + 1 || record[Tls13RecordReader.HeaderLength] != 1)
        {
            throw new TlsAlertException(TlsAlert.UnexpectedMessage, "A change_cipher_spec record is the single byte 01 (RFC 8446 appendix D.4).");
        }
    }
}
