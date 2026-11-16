using Surl.Composition;

namespace Surl.Api;

// One place turns a composition read's outcome into the value or the exception ADR-0164 decision 5 names.
internal static class SurlReadResults
{
    internal static T ValueOrThrow<T>(ComposedReadResult<T> result) =>
        result.Outcome switch
        {
            ComposedReadOutcome.Found => result.Value!,
            ComposedReadOutcome.NotFound => throw new SurlNotFoundException(result.Reason!),
            _ => throw new SurlReadRefusedException(result.Reason!),
        };

    // A read that finishes at once still reports its refusal through the task, as an async member does.
    internal static Task<T> Completed<T>(Func<T> read)
    {
        try
        {
            return Task.FromResult(read());
        }
        catch (Exception exception) when (exception is SurlNotFoundException or SurlReadRefusedException)
        {
            return Task.FromException<T>(exception);
        }
    }

    internal static uint UidOrThrow(long uid, string name) =>
        uid is >= 0 and <= uint.MaxValue ? (uint)uid : throw new SurlReadRefusedException($"{name} must be 0 to {uint.MaxValue}.");
}
