using System.Runtime.Versioning;
using Microsoft.Win32;
using Surl.Console;
using Surl.Kerberos.TestKdc;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// The hand-built test KDC of ADR-0065 decision 1, served in-process on <c>127.0.0.1:88</c> over
/// UDP and TCP for one user, <c>tester@SURL.TEST</c>, and the service principals the test names,
/// with their keys written to a temporary MIT keytab for <c>surl --keytab</c>. Starting it is
/// <c>Inconclusive</c>, never a failure, where the proof cannot run (ADR-0065 decision 3): off
/// Windows, without BL-265's <c>SURL.TEST</c> realm mapping, or with port 88 taken. Disposing it
/// stops the KDC and deletes the keytab.
/// </summary>
internal sealed class KerberosTestKdcOnLoopback : IAsyncDisposable
{
    /// <summary>The user's name inside the realm.</summary>
    public const string UserName = "tester";

    /// <summary>The user's password, from which the KDC derives the user's keys.</summary>
    public const string Password = "surl-test-password";

    /// <summary>The user principal, the name curl's <c>-u</c> gives and a surl account must have.</summary>
    public const string UserPrincipal = $"{UserName}@{KerberosTestKdc.Realm}";

    private const int KdcPort = 88;

    // ksetup /addkdc writes the realm's key here; reading it needs no administrator rights.
    private const string RealmMappingKey = @"SYSTEM\CurrentControlSet\Control\Lsa\Kerberos\Domains\" + KerberosTestKdc.Realm;

    private readonly IDatagramListener datagramListener;
    private readonly IConnectionListener connectionListener;
    private readonly CancellationTokenSource stop;
    private readonly Task serving;

    private KerberosTestKdcOnLoopback(
        IDatagramListener datagramListener, IConnectionListener connectionListener, CancellationTokenSource stop, Task serving, string keytabPath)
    {
        this.datagramListener = datagramListener;
        this.connectionListener = connectionListener;
        this.stop = stop;
        this.serving = serving;
        KeytabPath = keytabPath;
    }

    /// <summary>Gets the full path of the keytab holding every service principal's keys.</summary>
    public string KeytabPath { get; }

    /// <summary>
    /// Starts the KDC for <paramref name="servicePrincipals"/> (such as <c>HTTP/web.surl.test</c>),
    /// or marks the test <c>Inconclusive</c> where it cannot run.
    /// </summary>
    public static async Task<KerberosTestKdcOnLoopback> StartAsync(IReadOnlyList<string> servicePrincipals, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Only the Windows reference build has Kerberos (ADR-0065 decision 3).");
        }
        else if (!HasRealmMapping())
        {
            Assert.Inconclusive($"This machine maps no {KerberosTestKdc.Realm} realm to a KDC; BL-265's ksetup has not been run here.");
        }

        var kdc = new KerberosTestKdc(UserName, Password, servicePrincipals, TimeProvider.System, new RandomKerberosRandomSource());
        var listenerFactory = new SocketListenerFactory();
        var listenUrl = new ListenUrl("kerberos", "127.0.0.1", KdcPort);
        IDatagramListener? datagramListener = null;
        IConnectionListener connectionListener;
        try
        {
            datagramListener = await listenerFactory.StartDatagramListenerAsync(listenUrl, cancellationToken);
            connectionListener = await listenerFactory.StartConnectionListenerAsync(listenUrl, cancellationToken);
        }
        catch (ListenerBindException exception)
        {
            if (datagramListener is not null)
            {
                await datagramListener.DisposeAsync();
            }

            Assert.Inconclusive($"127.0.0.1:{KdcPort} could not be bound for the test KDC: {exception.Message}");
            throw;
        }

        var keytabPath = Path.Combine(Path.GetTempPath(), $"surl-conformance-{Guid.NewGuid():N}.keytab");
        await File.WriteAllBytesAsync(keytabPath, kdc.WriteServiceKeytab(), cancellationToken);
        var stop = new CancellationTokenSource();
        var serving = new KerberosTestKdcServer(kdc).ServeListenersAsync(datagramListener, connectionListener, stop.Token);
        return new KerberosTestKdcOnLoopback(datagramListener, connectionListener, stop, serving, keytabPath);
    }

    [SupportedOSPlatform("windows")]
    private static bool HasRealmMapping()
    {
        using var realmMapping = Registry.LocalMachine.OpenSubKey(RealmMappingKey);
        return realmMapping is not null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // The KDC's serving task ends cancelled, which is how it stops.
        await stop.CancelAsync();
        await Task.WhenAny(serving);
        await connectionListener.DisposeAsync();
        await datagramListener.DisposeAsync();
        stop.Dispose();
        File.Delete(KeytabPath);
    }
}
