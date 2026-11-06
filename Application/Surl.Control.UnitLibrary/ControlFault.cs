namespace Surl.Control;

/// <summary>One fault rule as <c>GET /v1/faults</c> lists it.</summary>
/// <param name="FaultId">The rule's ID, such as <c>f3</c>.</param>
/// <param name="Item">The rule as one <c>--fault</c> item.</param>
/// <param name="Remaining">How many more times the rule applies, or <see langword="null"/> when it always applies.</param>
public sealed record ControlFault(string FaultId, string Item, int? Remaining);
