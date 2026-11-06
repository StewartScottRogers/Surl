using System.Globalization;
using Surl.Protocol.Abstractions;

namespace Surl.Control;

/// <summary>
/// Turns an event into its JSON (ADR-0124 decision 7): <c>sequence</c>, <c>at</c> and <c>kind</c>, then the
/// record's members in snake_case, enum values in snake_case, and an exchange's bytes as text or base64, at most
/// <see cref="MaxShownBytesPerEvent"/> of them.
/// </summary>
internal static class ControlEventWriter
{
    /// <summary>The most bytes of one event shown.</summary>
    public const int MaxShownBytesPerEvent = 65536;

    /// <summary>The JSON of <paramref name="surlEvent"/>.</summary>
    /// <param name="surlEvent">An event.</param>
    /// <returns>Its JSON shape.</returns>
    public static ControlEventJson ToJson(SurlEvent surlEvent)
    {
        var head = new ControlEventJson
        {
            Sequence = surlEvent.Sequence,
            At = Timestamp(surlEvent.At),
            Kind = ControlEventKinds.NameOf(surlEvent),
        };

        return surlEvent switch
        {
            ExchangeStartedEvent started => head with
            {
                ExchangeId = started.ExchangeId,
                ListenUrl = started.ListenUrl,
                Scheme = started.Scheme,
                Client = started.Client.ToString(),
            },
            ExchangeBytesEvent bytes => WithBytes(head, bytes),
            ExchangeEndedEvent ended => head with
            {
                ExchangeId = ended.ExchangeId,
                ReceivedByteCount = ended.ReceivedByteCount,
                SentByteCount = ended.SentByteCount,
            },
            _ => WithMembersOfAnotherKind(head, surlEvent),
        };
    }

    /// <summary>A time as the endpoint writes it: UTC, round-trip format.</summary>
    /// <param name="at">The time.</param>
    /// <returns>Such as <c>2026-11-05T10:00:00.0000000+00:00</c>.</returns>
    public static string Timestamp(DateTimeOffset at) => at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static ControlEventJson WithMembersOfAnotherKind(ControlEventJson head, SurlEvent surlEvent)
    {
        return surlEvent switch
        {
            LoginJudgedEvent login => WithLogin(head, login),
            StoreChangedEvent change => WithStoreChange(head, change),
            FaultAppliedEvent fault => WithFault(head, fault),
            HookFailedEvent hook => head with
            {
                HookId = hook.HookId,
                Failure = ControlEventKinds.SnakeCase(hook.Failure),
                Message = hook.Message,
            },
            _ => head,
        };
    }

    private static ControlEventJson WithBytes(ControlEventJson head, ExchangeBytesEvent bytes)
    {
        var shown = ControlShownBytes.Show(bytes.Bytes.Span, MaxShownBytesPerEvent);

        return head with
        {
            ExchangeId = bytes.ExchangeId,
            Direction = ControlEventKinds.SnakeCase(bytes.Direction),
            Encoding = shown.Encoding,
            Data = shown.Data,
            ByteCount = bytes.Bytes.Length,
            IsCut = shown.ShownLength < bytes.Bytes.Length,
        };
    }

    private static ControlEventJson WithLogin(ControlEventJson head, LoginJudgedEvent login) => head with
    {
        ExchangeId = login.ExchangeId,
        Scheme = login.Scheme,
        Account = login.Account,
        Mechanism = login.Mechanism,
        IsAccepted = login.IsAccepted,
        IsRefusedByFault = login.IsRefusedByFault,
    };

    private static ControlEventJson WithStoreChange(ControlEventJson head, StoreChangedEvent change) => head with
    {
        ExchangeId = change.ExchangeId,
        Store = ControlEventKinds.SnakeCase(change.Store),
        Change = ControlEventKinds.SnakeCase(change.Change),
        Name = change.Name,
        PreviousName = change.PreviousName,
        ByteCount = change.ByteCount,
    };

    private static ControlEventJson WithFault(ControlEventJson head, FaultAppliedEvent fault) => head with
    {
        ExchangeId = fault.ExchangeId,
        RuleId = fault.RuleId,
        FaultKind = ControlEventKinds.SnakeCase(fault.Kind),
        Stage = ControlEventKinds.SnakeCase(fault.Stage),
        Target = fault.Target,
    };
}
