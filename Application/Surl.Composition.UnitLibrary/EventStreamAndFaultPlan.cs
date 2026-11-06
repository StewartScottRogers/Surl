using Surl.Core;

namespace Surl.Composition;

/// <summary>
/// The one event stream and the one fault plan a start composes for its instance and hands to every
/// store, the policy, every server and the serving engine (ADR-0124, decisions 1 and 4).
/// </summary>
/// <param name="Events">The instance's event stream.</param>
/// <param name="Faults">The instance's fault plan.</param>
internal sealed record EventStreamAndFaultPlan(SurlEventStream Events, FaultPlan Faults);
