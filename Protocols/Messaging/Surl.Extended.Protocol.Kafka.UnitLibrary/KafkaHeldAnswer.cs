using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.Kafka;

/// <summary>
/// Waits for a held JoinGroup or SyncGroup answer (ADR-0145 decision 9): while it is held the
/// exchange's idle clock is paused, so a join phase may outlast <c>--idle-timeout</c>, and it is
/// restarted once the answer is ready. The hold ends early when the exchange is cancelled.
/// </summary>
internal static class KafkaHeldAnswer
{
    /// <summary>Waits for <paramref name="answer"/>, the idle clock paused while it is not yet ready.</summary>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <param name="answer">The answer, perhaps already ready.</param>
    /// <param name="context">The exchange's context: its idle clock and cancellation token.</param>
    /// <returns>The answer.</returns>
    public static async ValueTask<T> WaitAsync<T>(Task<T> answer, ExchangeContext context)
    {
        if (answer.IsCompleted)
        {
            return await answer;
        }

        context.IdleClock.Pause();
        try
        {
            return await answer.WaitAsync(context.CancellationToken);
        }
        finally
        {
            context.IdleClock.Restart();
        }
    }
}
