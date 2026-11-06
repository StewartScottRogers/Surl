namespace Surl.Conformance;

/// <summary>
/// One protocol family's counts when upstream's test cases are played by both clients: how many cases
/// ran, how many each client passed, and how many ended as each of ADR-0089 decision 5's verdicts
/// other than agreement.
/// </summary>
/// <param name="Family">The family (<c>http</c>, <c>ftp</c>, <c>mail</c> ...).</param>
/// <param name="Cases">The cases of the family that were played.</param>
/// <param name="UpstreamPassed">The cases upstream curl passed.</param>
/// <param name="PortPassed">The cases the Curl port passed.</param>
/// <param name="PortDefects">The cases upstream passed and the port failed.</param>
/// <param name="NotComparable">The cases upstream passed and runtests would skip for the port.</param>
public sealed record UpstreamTestCasePortFamilyCount(
    string Family, int Cases, int UpstreamPassed, int PortPassed, int PortDefects, int NotComparable);
