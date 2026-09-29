namespace Surl.Conformance;

/// <summary>
/// What a pinned upstream curl build is used for, read from the <c>role</c> field of its
/// entry in <c>UpstreamCurlBuilds.json</c>.
/// </summary>
public enum UpstreamCurlBuildRole
{
    /// <summary>
    /// The reference release every conformance measurement uses unless a case needs a
    /// protocol only a supplementary build has. An entry with no <c>role</c> is a reference
    /// build.
    /// </summary>
    Reference,

    /// <summary>
    /// A build pinned only for the protocols the reference build lacks; never picked unless
    /// asked for by role.
    /// </summary>
    Supplementary,
}
