using Surl.Content;
using Surl.HttpMessage;
using Surl.Protocol.Abstractions;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// Chosen-error faults on the JSON API (ADR-0148, decision 5; ADR-0124 decisions 4 and 9; ADR-0144): each request
/// asks the plan at the command stage, <c>command</c> the Google method id (<c>storage.objects.get</c>) and
/// <c>path</c> the request path, and a rule whose reply is a status from 400 to 599 is answered with the row of
/// that status, else <c>backendError</c>.
/// </summary>
internal static class GcsJsonFaults
{
    /// <summary>
    /// Asks the plan for a chosen error.
    /// </summary>
    /// <param name="context">The exchange: its plan, log and events.</param>
    /// <param name="methodId">The Google method id of the operation.</param>
    /// <param name="path">The request path, percent-decoded.</param>
    /// <param name="caller">Who asks, whose account name the rule may match.</param>
    /// <returns>The error to answer, or <see langword="null"/> when no rule chose one that fits.</returns>
    public static async ValueTask<GcsJsonError?> FindChosenErrorAsync(ExchangeContext context, string methodId, string path, ContentCaller caller)
    {
        var rule = await HttpCommandFaults.FindChosenErrorAsync(context, methodId, path, caller.AccountName);
        if (rule is null)
        {
            return null;
        }

        if (!HttpCommandFaults.TrySplitCode(rule.Reply, out var status, out _) || status is < 400 or > 599)
        {
            context.Log.Note($"{methodId} {path}: fault rule {rule.Id}'s reply does not fit the gs JSON error shape; answered as without it.");

            return null;
        }

        var error = GcsJsonError.Chosen(status, GcsRequestFaults.ReasonPhrases.GetValueOrDefault(status, string.Empty));
        HttpCommandFaults.PublishApplied(context, rule, methodId, path, $"{status} {error.Reason}");

        return error;
    }
}
