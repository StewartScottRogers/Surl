namespace Surl.Conformance;

/// <summary>
/// One step of a libcurl WebSocket driver's script: a <c>curl_ws_send</c> of a payload with its
/// <c>CURLWS_*</c> flags, or a <c>curl_ws_recv</c>.
/// </summary>
/// <param name="IsReceive">Whether the step is a <c>curl_ws_recv</c>; otherwise it is a send.</param>
/// <param name="Flags">The send's <c>CURLWS_*</c> bits (<see cref="LibcurlWebSocketFlags"/>); 0 for a receive.</param>
/// <param name="Payload">The send's payload; empty for a receive.</param>
public sealed record LibcurlWebSocketStep(bool IsReceive, uint Flags, byte[] Payload);
