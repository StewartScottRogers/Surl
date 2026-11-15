using Surl.Protocol.Abstractions;
using Surl.Protocol.Ldap;

namespace Surl.Composition;

/// <summary>
/// Joins the composed LDAP server's directory to whole-instance reset, snapshot and restore
/// (ADR-0158 decisions 4 to 6). A directory loaded from <c>--directory</c> is an in-memory copy of a file
/// read once, so reset returns to that copy and nothing is ever written to the file.
/// </summary>
internal static class LdapDirectoryStoreRegistration
{
    /// <summary>
    /// Builds the entry that controls <paramref name="ldapServer"/>'s directory; the directory itself
    /// publishes one <see cref="StoreChange.Reset"/> or <see cref="StoreChange.Restored"/> per run.
    /// </summary>
    /// <param name="ldapServer">The composed LDAP server.</param>
    /// <returns>The entry.</returns>
    public static ComposedStoreRegistration For(LdapProtocolServer ldapServer) =>
        new(
            StoreKind.Directory,
            _ =>
            {
                ldapServer.ResetDirectory();
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                var snapshot = ldapServer.TakeDirectorySnapshot();
                return Task.FromResult(new TakenSnapshot(
                    StoreKind.Directory,
                    snapshot.ByteCount,
                    _ =>
                    {
                        ldapServer.RestoreDirectory(snapshot);
                        return Task.CompletedTask;
                    }));
            });
}
