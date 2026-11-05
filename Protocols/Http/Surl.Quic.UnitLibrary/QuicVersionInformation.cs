namespace Surl.Quic;

/// <summary>
/// The <c>version_information</c> transport parameter (RFC 9368 section 3): the version the
/// sender chose for the connection and the versions it is willing to use.
/// </summary>
/// <param name="ChosenVersion">The version the sender's packets on this connection use.</param>
/// <param name="AvailableVersions">The versions the sender supports, in its order of preference.</param>
public sealed record QuicVersionInformation(uint ChosenVersion, IReadOnlyList<uint> AvailableVersions);
