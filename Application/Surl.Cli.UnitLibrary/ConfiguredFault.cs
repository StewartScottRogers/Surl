using Surl.Protocol.Abstractions;

namespace Surl.Cli;

/// <summary>
/// One <c>--fault &lt;kind&gt;[;&lt;key&gt;=&lt;value&gt;]...</c> (ADR-0124 decision 4): a rule that makes the
/// server misbehave on purpose, as written and checked, without its rule ID. <c>Surl.Composition</c>
/// adds each to the instance's fault plan at start (BL-803). A scope left <see langword="null"/>
/// matches anything; a parameter left <see langword="null"/> was not given.
/// </summary>
/// <param name="Kind">What the rule makes the server do: the item's head.</param>
public sealed record ConfiguredFault(FaultKind Kind)
{
    /// <summary>The listen URL the rule is scoped to (<c>listener</c>), as written.</summary>
    public string? Listener { get; init; }

    /// <summary>The scheme the rule is scoped to (<c>scheme</c>), lower-cased.</summary>
    public string? Scheme { get; init; }

    /// <summary>The account the rule is scoped to (<c>account</c>).</summary>
    public string? Account { get; init; }

    /// <summary>The path, mailbox, topic, queue, selector or DN the rule is scoped to (<c>path</c>).</summary>
    public string? Path { get; init; }

    /// <summary>The verb the rule is scoped to (<c>command</c>), as written; matched case-insensitively.</summary>
    public string? Command { get; init; }

    /// <summary>
    /// The stage a <see cref="FaultKind.Delay"/> or <see cref="FaultKind.Drop"/> acts at (<c>stage</c>):
    /// <see cref="FaultStage.Accept"/> or <see cref="FaultStage.Command"/>; null when not given, which is accept.
    /// </summary>
    public FaultStage? Stage { get; init; }

    /// <summary>How long a <see cref="FaultKind.Delay"/> waits (<c>ms</c>), 0 to 600000 milliseconds.</summary>
    public int? DelayMilliseconds { get; init; }

    /// <summary>How many bytes a <see cref="FaultKind.Drop"/> lets through before it drops (<c>after-bytes</c>).</summary>
    public long? AfterBytes { get; init; }

    /// <summary>The reply an <see cref="FaultKind.Error"/> sends (<c>reply</c>), checked against its scheme's error shape.</summary>
    public string? Reply { get; init; }

    /// <summary>How many times the rule applies (<c>times</c>), 1 to 1000000; null for always.</summary>
    public int? Times { get; init; }
}
