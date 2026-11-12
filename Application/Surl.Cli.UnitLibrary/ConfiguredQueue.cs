namespace Surl.Cli;

/// <summary>
/// One <c>--queue &lt;name&gt;[;durable]</c> (ADR-0101 decision 4, ADR-0102 decision 3.2): a named queue the
/// message bus creates at start. The record holds what was written, percent-decoded.
/// </summary>
/// <param name="Name">The queue's name, never empty, never holding a control character.</param>
/// <param name="IsDurable">Whether <c>durable</c> was given, so the queue keeps its messages across starts.</param>
public sealed record ConfiguredQueue(string Name, bool IsDurable);
