namespace Surl.Conformance;

/// <summary>
/// How one run of an upstream test case against Surl ended (ADR-0080 decision 5).
/// </summary>
public enum UpstreamTestCaseOutcome
{
    /// <summary>The case ran and its verify section passed.</summary>
    Pass,

    /// <summary>The case ran and a verify section differed, or curl was stopped at the case's timeout.</summary>
    Fail,

    /// <summary>The case did not run, for a skip reason.</summary>
    Skip,
}
