using Surl.Protocol.Abstractions;

namespace Surl.Composition;

/// <summary>
/// Joins the several kinds of upload the composed servers hold (S3's open multipart uploads, Azure
/// Storage's staged blocks) into the one <see cref="StoreKind.Uploads"/> entry an instance has
/// (ADR-0167, decision 3).
/// </summary>
internal static class UploadStoreRegistrations
{
    /// <summary>
    /// Combines <paramref name="registrations"/> into one entry that runs each in order.
    /// </summary>
    /// <param name="registrations">The uploads entries; null ones, for a server not composed, are left out.</param>
    /// <returns>The entry, or <see langword="null"/> when none is given.</returns>
    public static ComposedStoreRegistration? Combine(params ComposedStoreRegistration?[] registrations) =>
        CombineOf(StoreKind.Uploads, registrations);

    /// <summary>
    /// Combines <paramref name="registrations"/> of one <paramref name="kind"/> into one entry that runs each in order.
    /// </summary>
    /// <param name="kind">The store kind every entry controls.</param>
    /// <param name="registrations">The entries; null ones, for a server not composed, are left out.</param>
    /// <returns>The entry, or <see langword="null"/> when none is given.</returns>
    public static ComposedStoreRegistration? CombineOf(StoreKind kind, params ComposedStoreRegistration?[] registrations)
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
