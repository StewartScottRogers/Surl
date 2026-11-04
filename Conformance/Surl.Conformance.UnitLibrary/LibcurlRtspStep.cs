namespace Surl.Conformance;

/// <summary>
/// One step of a <see cref="LibcurlRtspScript"/>: an RTSP request to perform, or an option to set
/// on the easy handle the requests share, which stays set for every later request, as libcurl's own
/// options do.
/// </summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Request">The request a <see cref="LibcurlRtspStepKind.Request"/> step performs; <see cref="LibcurlRtspRequest.Options"/> otherwise.</param>
/// <param name="Value">The bytes a text or body step sets; empty otherwise.</param>
/// <param name="Number">The number a <see cref="LibcurlRtspStepKind.ClientCSeq"/> step sets; 0 otherwise.</param>
public sealed record LibcurlRtspStep(LibcurlRtspStepKind Kind, LibcurlRtspRequest Request, byte[] Value, int Number);
