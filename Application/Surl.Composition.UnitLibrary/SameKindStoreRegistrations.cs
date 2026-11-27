using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the registrations of one <see cref="StoreKind"/> into the one entry an instance has for it,
/// since <see cref="ComposedStores"/> finds a store's control by kind (ADR-0167, decision 3): the
/// <see cref="StoreKind.Uploads"/> entry (the uploads of the S3, Azure Storage, Google Cloud Storage
/// and OCI registry servers) and the <see cref="StoreKind.Sessions"/> entry (MQTT 5 sessions, OCI
/// registry tokens, and the MCP server's OAuth state and HTTP sessions).
/// </summary>
internal static class SameKindStoreRegistrations
{
    /// <summary>
    /// Combines <paramref name="registrations"/> into one entry whose reset, snapshot and restore run
    /// each in the order given; one that throws stops those after it.
    /// </summary>
    /// <param name="kind">The combined entry's store kind, which every registration is expected to
    /// have; this is not checked.</param>
    /// <param name="registrations">The registrations; null ones, for a server not composed, are left out.</param>
    /// <returns><see langword="null"/> when none is given; the only one given, unchanged; otherwise a
    /// new entry whose snapshot holds the sum of their bytes. Each is offered the same free bytes, and
    /// the sum is not bounded here.</returns>
    public static ComposedStoreRegistration? Combine(StoreKind kind, params ComposedStoreRegistration?[] registrations)
    {
        ComposedStoreRegistration[] present = [.. registrations.OfType<ComposedStoreRegistration>()];

        return present.Length switch
        {
            0 => null,
            1 => present[0],
            _ => new ComposedStoreRegistration(
                kind,
                async cancellationToken =>
                {
                    foreach (var registration in present)
                    {
                        await registration.ResetAsync(cancellationToken);
                    }
                },
                async (freeBytes, cancellationToken) => await TakeAllAsync(kind, present, freeBytes, cancellationToken)),
        };
    }

    private static async Task<TakenSnapshot> TakeAllAsync(StoreKind kind, ComposedStoreRegistration[] present, long freeBytes, CancellationToken cancellationToken)
    {
        List<TakenSnapshot> taken = [];
        foreach (var registration in present)
        {
            taken.Add(await registration.TakeSnapshotAsync(freeBytes, cancellationToken));
        }

        return new TakenSnapshot(
            kind,
            taken.Sum(snapshot => snapshot.ByteCount),
            async restoreCancellation =>
            {
                foreach (var snapshot in taken)
                {
                    await snapshot.RestoreAsync(restoreCancellation);
                }
            });
    }
}
