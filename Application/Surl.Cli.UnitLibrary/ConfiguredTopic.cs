namespace Surl.Cli;

/// <summary>
/// One <c>--topic &lt;filter&gt;[;retain][;qos=&lt;0|1|2&gt;]</c> (ADR-0101 decision 4, ADR-0102 decision 3.1): a
/// canonical topic filter declared to the message bus, with the attributes every matching topic takes. The
/// record holds what was written, percent-decoded; the bus checks the filter's wildcards at start (BL-991).
/// </summary>
/// <param name="Filter">The canonical topic filter, never empty, never holding a control character.</param>
/// <param name="IsRetained">Whether <c>retain</c> was given, so every publish on a matching topic is retained.</param>
/// <param name="MaxQos">The highest QoS the bus carries a matching publish at, 0, 1 or 2; null when <c>qos=</c> was not given.</param>
public sealed record ConfiguredTopic(string Filter, bool IsRetained, int? MaxQos);
