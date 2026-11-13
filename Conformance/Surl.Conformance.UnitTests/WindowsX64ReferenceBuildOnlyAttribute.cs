using System.Runtime.InteropServices;

namespace Surl.Conformance;

/// <summary>
/// Runs a test only where the pinned reference build is the Windows x64 build - the Schannel,
/// WinCNG libssh2 and SSPI build <c>UpstreamCurlBuilds.json</c> pins for <c>win-x64</c> - and
/// skips it everywhere else, including <c>win-arm64</c>, whose pin is a statically linked
/// OpenSSL build with none of those quirks (BL-361, ADR-0151, BL-1108).
/// </summary>
/// <remarks>The gate is the pinned platform, not <c>OperatingSystem.IsWindows()</c>.</remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class WindowsX64ReferenceBuildOnlyAttribute() : ConditionBaseAttribute(ConditionMode.Include)
{
    /// <inheritdoc />
    public override bool IsConditionMet => UpstreamCurlLocator.CurrentPlatform == "win-x64";

    /// <inheritdoc />
    public override string GroupName => nameof(WindowsX64ReferenceBuildOnlyAttribute);
}
