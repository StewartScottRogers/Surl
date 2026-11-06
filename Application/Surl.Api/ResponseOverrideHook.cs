using Surl.Core;
using Surl.Protocol.Abstractions;

namespace Surl.Api;

/// <summary>
/// One response override, bounded (ADR-0124, decision 5): it runs the test's decision off the serving thread
/// with <see cref="SurlServerOptions.HookTimeLimit"/> on the instance's clock, and turns its answer into a fault
/// rule through <c>surl</c>'s own <c>--fault</c> reader, so it can choose only what a fault rule can. A decision
/// that throws, runs past its limit or answers a rule <c>surl</c> refuses or that cannot act at the point is
/// published as a <see cref="HookFailedEvent"/>, and the point is served as if the override were not there.
/// </summary>
/// <param name="decide">The test's decision.</param>
/// <param name="events">Where a failure is published: the instance's event stream.</param>
/// <param name="timeProvider">The clock the limit runs on.</param>
/// <param name="timeLimit">How long a decision may take.</param>
internal sealed class ResponseOverrideHook(
    Func<FaultPoint, CancellationToken, ValueTask<SurlFault?>> decide,
    ISurlEventPublisher events,
    TimeProvider timeProvider,
    TimeSpan timeLimit)
{
    /// <summary>Asks the decision at <paramref name="point"/> and reads its answer; matches <see cref="FaultOverride"/>.</summary>
    /// <param name="overrideId">The override's identifier, named in a <see cref="HookFailedEvent"/>.</param>
    /// <param name="point">The point.</param>
    /// <param name="cancellationToken">The exchange's cancellation.</param>
    /// <returns>The rule to apply, or <see langword="null"/>.</returns>
    public async ValueTask<FaultRule?> DecideAsync(string overrideId, FaultPoint point, CancellationToken cancellationToken)
    {
        var decisionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var limitCancellation = new CancellationTokenSource();
        // Started off the serving thread, so even a decision that blocks before its first await is bounded; StartNew
        // keeps an OperationCanceledException the decision throws as its failure rather than a cancellation.
        var decision = Task.Factory.StartNew(
            () => decide(point, decisionCancellation.Token).AsTask(),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default).Unwrap();
        var limit = Task.Delay(timeLimit, timeProvider, limitCancellation.Token);
        if (await Task.WhenAny(decision, limit) != decision)
        {
            // The decision may not have started yet: its token's source is released only once it has ended.
            await decisionCancellation.CancelAsync();
            ReleaseWhenEnded(decision, decisionCancellation);
            cancellationToken.ThrowIfCancellationRequested();
            return Failed(overrideId, HookFailure.TimedOut, $"The response override ran past its time limit of {timeLimit}.");
        }

        decisionCancellation.Dispose();
        await limitCancellation.CancelAsync();
        return await ReadAnswerAsync(overrideId, decision, point, cancellationToken);
    }

    /// <summary>The rule <paramref name="answer"/> spells at <paramref name="point"/>, read as <c>surl --fault</c> reads it.</summary>
    /// <param name="answer">The decision's answer.</param>
    /// <param name="point">The point it answers.</param>
    /// <returns>The rule.</returns>
    /// <exception cref="ArgumentException"><c>surl</c> refuses the rule, or it cannot act at the point's stage.</exception>
    internal static FaultRule ToRule(SurlFault answer, FaultPoint point)
    {
        // The answer applies to the point it was asked at: its own scope is dropped, and the point's scheme picks
        // an error's reply shape.
        var scoped = answer with
        {
            Listener = null,
            Scheme = answer.Kind == FaultKind.Error ? point.Scheme : null,
            Account = null,
            Path = null,
            Command = null,
            Times = null,
        };
        var rule = SurlServerFaults.Read(scoped, nameof(answer));
        return FaultPlan.StageOf(rule) == point.Stage
            ? rule
            : throw new ArgumentException($"A {rule.Kind} fault cannot act at the {point.Stage} stage.", nameof(answer));
    }

    // Observes a late decision's failure, so it is never an unobserved task exception, and releases its token's source.
    private static void ReleaseWhenEnded(Task decision, CancellationTokenSource decisionCancellation) =>
        decision.ContinueWith(
            late =>
            {
                _ = late.Exception;
                decisionCancellation.Dispose();
            },
            TaskScheduler.Default);

    private async ValueTask<FaultRule?> ReadAnswerAsync(
        string overrideId, Task<SurlFault?> decision, FaultPoint point, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await decision;
            return answer is null ? null : ToRule(answer, point);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failed(overrideId, HookFailure.Threw, exception.Message);
        }
    }

    private FaultRule? Failed(string overrideId, HookFailure failure, string message)
    {
        events.Publish(new HookFailedEvent(overrideId, failure, message));
        return null;
    }
}
