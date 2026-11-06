namespace Surl.Conformance;

/// <summary>
/// What <see cref="PinnedCurlPortBesideUpstreamCurl.MeasureAsync"/> found: the verdict, and what each
/// run's own <c>surl</c> and recording relay showed, so a test can check what was compared.
/// </summary>
/// <param name="Verdict">The verdict.</param>
/// <param name="UpstreamSurl">Upstream curl's run as its <c>surl</c> and relay saw it.</param>
/// <param name="PortSurl">The Curl port's run as its <c>surl</c> and relay saw it.</param>
internal sealed record PinnedCurlPortMeasurement(CurlPortVerdict Verdict, CurlPortSurlSide UpstreamSurl, CurlPortSurlSide PortSurl);
