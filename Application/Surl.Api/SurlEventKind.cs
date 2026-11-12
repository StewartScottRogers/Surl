using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// The kinds of event a <see cref="SurlServer"/> keeps for <see cref="SurlServer.FindEvents"/>, one per
/// <see cref="SurlEvent"/> record it keeps; exchange bytes are kept on <see cref="SurlServer.Exchanges"/>
/// instead, so they have no kind here (ADR-0124 decision 1, ADR-0143).
/// </summary>
public enum SurlEventKind
{
    /// <summary>An <see cref="ExchangeStartedEvent"/>: a listener accepted a client.</summary>
    ExchangeStarted,

    /// <summary>An <see cref="ExchangeEndedEvent"/>: an exchange's connection closed.</summary>
    ExchangeEnded,

    /// <summary>A <see cref="LoginJudgedEvent"/>: a login was accepted or refused.</summary>
    LoginJudged,

    /// <summary>A <see cref="StoreChangedEvent"/>: a store gained, changed or lost something.</summary>
    StoreChanged,

    /// <summary>A <see cref="FaultAppliedEvent"/>: a fault rule acted.</summary>
    FaultApplied,

    /// <summary>A <see cref="HookFailedEvent"/>: a hook threw or ran out of time.</summary>
    HookFailed,

    /// <summary>A <see cref="ConsumerGroupChangedEvent"/>: a consumer group's membership or generation changed.</summary>
    ConsumerGroupChanged,
}
