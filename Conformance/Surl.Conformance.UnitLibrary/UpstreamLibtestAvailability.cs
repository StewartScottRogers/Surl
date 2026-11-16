namespace Surl.Conformance;

/// <summary>
/// Whether the platform can run upstream's libtest programs (ADR-0174 decision 4).
/// </summary>
public enum UpstreamLibtestAvailability
{
    /// <summary>The platform pins no libtest entry, as Windows and macOS do not: every libtest case keeps skip reason <c>libtest</c>.</summary>
    NotPinned,

    /// <summary>The platform pins the programs but the file is not on this machine: an admitted case skips as <c>libtest-not-built:&lt;name&gt;</c>.</summary>
    PinnedButAbsent,

    /// <summary>The programs are present and hash to their pins.</summary>
    Present,
}
