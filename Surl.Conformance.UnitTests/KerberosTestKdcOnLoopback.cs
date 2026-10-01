using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Surl.Console;
using Surl.Kerberos.TestKdc;
using Surl.Networking;
using Surl.Protocol.Abstractions;

namespace Surl.Conformance;

/// <summary>
/// The hand-built test KDC of ADR-0065 decision 1, served in-process on loopback over UDP and TCP
/// for one user, <c>tester@SURL.TEST</c>, and the service principals the test names, with their
/// keys written to a temporary MIT keytab for <c>surl --keytab</c>. Two ways to start it, one per
/// GSS-API a pinned build has:
/// <list type="bullet">
/// <item><see cref="StartAsync"/>, for the Windows reference build's SSPI, which takes <c>-u</c>'s
/// password and finds the KDC through BL-265's <c>SURL.TEST</c> realm mapping on <c>127.0.0.1:88</c>.</item>
/// <item><see cref="StartWithCredentialCacheAsync"/>, for ADR-0078's OpenLDAP build's MIT GSS-API,
/// which reads a credential cache: the KDC takes an ephemeral port, a <c>krb5.conf</c> names it, and
/// <c>kinit</c> fills the cache, as <c>Record-CurlExchange.ps1</c>'s <c>Initialize-KerberosClient</c>
/// does; curl then runs with <see cref="ClientEnvironment"/>.</item>
/// </list>
/// Starting it is <c>Inconclusive</c>, never a failure, where the proof cannot run (ADR-0065
/// decision 3). Disposing it stops the KDC and deletes what it wrote.
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

    private static readonly TimeSpan KinitTimeout = TimeSpan.FromSeconds(30);

    private readonly IDatagramListener datagramListener;
    private readonly IConnectionListener connectionListener;
    private readonly CancellationTokenSource stop;
    private readonly Task serving;
    private readonly string? clientDirectory;

    private KerberosTestKdcOnLoopback(
        IDatagramListener datagramListener,
        IConnectionListener connectionListener,
        CancellationTokenSource stop,
        Task serving,
        string keytabPath,
        string? clientDirectory,
        IReadOnlyDictionary<string, string> clientEnvironment)
    {
        this.datagramListener = datagramListener;
        this.connectionListener = connectionListener;
        this.stop = stop;
        this.serving = serving;
        this.clientDirectory = clientDirectory;
        KeytabPath = keytabPath;
        ClientEnvironment = clientEnvironment;
    }

    /// <summary>Gets the full path of the keytab holding every service principal's keys.</summary>
    public string KeytabPath { get; }

    /// <summary>
    /// Gets the environment an MIT GSS-API curl runs with - <c>KRB5_CONFIG</c> and
    /// <c>KRB5CCNAME</c> naming the <c>krb5.conf</c> and the credential cache <c>kinit</c> filled -
    /// or no variables, for a KDC <see cref="StartAsync"/> started for SSPI.
    /// </summary>
    public IReadOnlyDictionary<string, string> ClientEnvironment { get; }

    /// <summary>
    /// Starts the KDC on <c>127.0.0.1:88</c> for <paramref name="servicePrincipals"/> (such as
    /// <c>HTTP/web.surl.test</c>), for the Windows reference build's SSPI, or marks the test
    /// <c>Inconclusive</c> where it cannot run: off Windows, without BL-265's realm mapping, or with
    /// port 88 taken.
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
        var (datagramListener, connectionListener) = await StartListenersAsync(KdcPort, cancellationToken);
        var keytabPath = Path.Combine(Path.GetTempPath(), $"surl-conformance-{Guid.NewGuid():N}.keytab");
        await File.WriteAllBytesAsync(keytabPath, kdc.WriteServiceKeytab(), cancellationToken);
        var stop = new CancellationTokenSource();
        var serving = new KerberosTestKdcServer(kdc).ServeListenersAsync(datagramListener, connectionListener, stop.Token);
        return new KerberosTestKdcOnLoopback(
            datagramListener, connectionListener, stop, serving, keytabPath, clientDirectory: null, new Dictionary<string, string>());
    }

    /// <summary>
    /// Starts the KDC on an ephemeral loopback port for <paramref name="servicePrincipals"/> (such
    /// as <c>ldap/ldap.surl.test</c>), writes a <c>krb5.conf</c> naming it and runs
    /// <c>kinit tester@SURL.TEST</c> to fill a credential cache, for an MIT GSS-API build run with
    /// <see cref="ClientEnvironment"/>. Marks the test <c>Inconclusive</c> on Windows, whose pinned
    /// builds use SSPI, or where no <c>kinit</c> is on <c>PATH</c> (CI's Linux leg installs
    /// Ubuntu's <c>krb5-user</c>).
    /// </summary>
    public static async Task<KerberosTestKdcOnLoopback> StartWithCredentialCacheAsync(
        IReadOnlyList<string> servicePrincipals, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The pinned Windows builds' Kerberos is SSPI, which reads no MIT credential cache (ADR-0078).");
        }

        var kinit = FindOnPath("kinit");
        if (kinit is null)
        {
            Assert.Inconclusive("No kinit is on PATH to fill the MIT credential cache curl's GSS-API reads (Ubuntu's krb5-user has one).");
        }

        var kdc = new KerberosTestKdc(UserName, Password, servicePrincipals, TimeProvider.System, new RandomKerberosRandomSource());
        var (datagramListener, connectionListener) = await StartListenersAsync(0, cancellationToken);
        var port = connectionListener.ListenUrl.BoundPort!.Value;
        var clientDirectory = Directory.CreateTempSubdirectory("surl-conformance-krb5-").FullName;
        var keytabPath = Path.Combine(clientDirectory, "service.keytab");
        await File.WriteAllBytesAsync(keytabPath, kdc.WriteServiceKeytab(), cancellationToken);
        var stop = new CancellationTokenSource();
        var serving = new KerberosTestKdcServer(kdc).ServeListenersAsync(datagramListener, connectionListener, stop.Token);
        var started = new KerberosTestKdcOnLoopback(
            datagramListener, connectionListener, stop, serving, keytabPath, clientDirectory,
            await WriteClientConfigurationAsync(clientDirectory, port, cancellationToken));
        try
        {
            await RunKinitAsync(kinit!, started.ClientEnvironment, cancellationToken);
        }
        catch
        {
            await started.DisposeAsync();
            throw;
        }

        return started;
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
        if (clientDirectory is null)
        {
            File.Delete(KeytabPath);
        }
        else
        {
            Directory.Delete(clientDirectory, recursive: true);
        }
    }

    // Port 0 binds TCP on an ephemeral port first, then UDP on the same one (ListenerBinder).
    private static async Task<(IDatagramListener Datagram, IConnectionListener Connection)> StartListenersAsync(
        int port, CancellationToken cancellationToken)
    {
        var listenerFactory = new SocketListenerFactory();
        IDatagramListener? datagramListener = null;
        try
        {
            var connectionListener = await listenerFactory.StartConnectionListenerAsync(
                new ListenUrl("kerberos", "127.0.0.1", port), cancellationToken);
            try
            {
                datagramListener = await listenerFactory.StartDatagramListenerAsync(
                    new ListenUrl("kerberos", "127.0.0.1", connectionListener.ListenUrl.BoundPort!.Value), cancellationToken);
            }
            catch (ListenerBindException)
            {
                await connectionListener.DisposeAsync();
                throw;
            }

            return (datagramListener, connectionListener);
        }
        catch (ListenerBindException exception)
        {
            Assert.Inconclusive($"127.0.0.1:{port} could not be bound for the test KDC: {exception.Message}");
            throw;
        }
    }

    // The krb5.conf Record-CurlExchange.ps1's Initialize-KerberosClient measured with (ADR-0078):
    // TCP only, no DNS, no reverse lookup, .surl.test in SURL.TEST; here with the KDC's own port.
    private static async Task<IReadOnlyDictionary<string, string>> WriteClientConfigurationAsync(
        string directory, int kdcPort, CancellationToken cancellationToken)
    {
        var configurationPath = Path.Combine(directory, "krb5.conf");
        await File.WriteAllTextAsync(
            configurationPath,
            $"[libdefaults]\n  default_realm = {KerberosTestKdc.Realm}\n  dns_lookup_kdc = false\n  dns_lookup_realm = false\n"
            + "  rdns = false\n  dns_canonicalize_hostname = false\n  udp_preference_limit = 1\n"
            + $"[realms]\n  {KerberosTestKdc.Realm} = {{\n    kdc = 127.0.0.1:{kdcPort}\n  }}\n"
            + $"[domain_realm]\n  .surl.test = {KerberosTestKdc.Realm}\n",
            cancellationToken);
        return new Dictionary<string, string>
        {
            ["KRB5_CONFIG"] = configurationPath,
            ["KRB5CCNAME"] = "FILE:" + Path.Combine(directory, "tester.ccache"),
        };
    }

    // kinit reads the password from standard input when it is not a terminal.
    private static async Task RunKinitAsync(string kinit, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(kinit, UserPrincipal)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)!;
        await process.StandardInput.WriteLineAsync(Password.AsMemory(), cancellationToken);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(KinitTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        Assert.AreEqual(0, process.ExitCode, $"kinit {UserPrincipal} exited {process.ExitCode}: {await output}{await error}");
    }

    private static string? FindOnPath(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, program))
            .FirstOrDefault(File.Exists);

    [SupportedOSPlatform("windows")]
    private static bool HasRealmMapping()
    {
        using var realmMapping = Registry.LocalMachine.OpenSubKey(RealmMappingKey);
        return realmMapping is not null;
    }
}
