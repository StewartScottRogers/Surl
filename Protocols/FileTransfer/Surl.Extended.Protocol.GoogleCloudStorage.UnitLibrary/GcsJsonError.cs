using Surl.HttpMessage;

namespace Surl.Extended.Protocol.GoogleCloudStorage;

/// <summary>
/// One row of the JSON API's errors ("HTTP status and error codes", JSON; ADR-0148, decision 5): the status, the
/// <c>reason</c> and Surl's fixed <c>message</c> (ADR-0006, section 3), which never holds a client byte.
/// </summary>
/// <param name="Status">The status the page gives the reason.</param>
/// <param name="Reason">The <c>reason</c> of the one entry in <c>errors</c>, such as <c>notFound</c>.</param>
/// <param name="Message">The fixed <c>message</c>.</param>
internal sealed record GcsJsonError(HttpStatus Status, string Reason, string Message)
{
    /// <summary>
    /// <c>304 notModified</c>: a matching <c>If-None-Match</c>, an unmodified <c>If-Modified-Since</c> or
    /// <c>ifGenerationNotMatch</c>; sent without a body.
    /// </summary>
    public static GcsJsonError NotModified { get; } = new(new HttpStatus(304, "Not Modified"), "notModified", "Not Modified");

    /// <summary>
    /// <c>400 invalid</c>: an over-long name.
    /// </summary>
    public static GcsJsonError Invalid { get; } = new(HttpStatus.BadRequest, "invalid", "Invalid request.");

    /// <summary>
    /// <c>400 invalidParameter</c>: a malformed query value.
    /// </summary>
    public static GcsJsonError InvalidParameter { get; } = new(HttpStatus.BadRequest, "invalidParameter", "Invalid parameter.");

    /// <summary>
    /// <c>400 required</c>: <c>project</c> missing from <c>buckets.list</c>.
    /// </summary>
    public static GcsJsonError Required { get; } = new(HttpStatus.BadRequest, "required", "Required parameter missing.");

    /// <summary>
    /// <c>400 invalid</c> for an <c>Authorization</c> with a scheme other than Bearer (decision 4).
    /// </summary>
    public static GcsJsonError InvalidAuthentication { get; } = new(HttpStatus.BadRequest, "invalid", "The provided authentication header is invalid.");

    /// <summary>
    /// <c>401 required</c>: no credential with anonymous off, or an anonymous write.
    /// </summary>
    public static GcsJsonError AuthenticationRequired { get; } = new(HttpStatus.Unauthorized, "required", "Authentication required.");

    /// <summary>
    /// <c>401 authError</c>: a Bearer token that is no account, or sent over plain text.
    /// </summary>
    public static GcsJsonError InvalidCredentials { get; } = new(HttpStatus.Unauthorized, "authError", "Invalid Credentials");

    /// <summary>
    /// <c>403 forbidden</c>: a write with <c>--allow-uploads</c> off or forbidden by share access, or a listing
    /// with <c>--list-directories</c> off.
    /// </summary>
    public static GcsJsonError Forbidden { get; } = new(HttpStatus.Forbidden, "forbidden", "Access denied.");

    /// <summary>
    /// <c>404 notFound</c>: no such bucket or object, or one the caller may not read.
    /// </summary>
    public static GcsJsonError NotFound { get; } = new(HttpStatus.NotFound, "notFound", "Not Found");

    /// <summary>
    /// <c>412 conditionNotMet</c>: a failing generation, metageneration or <c>If-*</c> condition.
    /// </summary>
    public static GcsJsonError ConditionNotMet { get; } = new(new HttpStatus(412, "Precondition Failed"), "conditionNotMet", "Precondition Failed");

    /// <summary>
    /// <c>416 requestedRangeNotSatisfiable</c>: an unsatisfiable <c>Range</c> on <c>alt=media</c>.
    /// </summary>
    public static GcsJsonError RangeNotSatisfiable { get; } = new(new HttpStatus(416, "Range Not Satisfiable"), "requestedRangeNotSatisfiable", "Requested range not satisfiable.");

    /// <summary>
    /// <c>501 notImplemented</c>: every JSON request decision 2 does not list.
    /// </summary>
    public static GcsJsonError NotImplemented { get; } = new(HttpStatus.NotImplemented, "notImplemented", "Not implemented.");

    /// <summary>
    /// <c>503 backendError</c>: the row a chosen <c>503</c> is answered with.
    /// </summary>
    public static GcsJsonError BackendError { get; } = new(HttpStatus.ServiceUnavailable, "backendError", "Please reduce your request rate.");

    private static readonly GcsJsonError[] Rows =
    [
        Invalid, AuthenticationRequired, Forbidden, NotFound, ConditionNotMet, RangeNotSatisfiable, NotImplemented, BackendError,
    ];

    /// <summary>
    /// The <c>domain</c> of the error: <c>usageLimits</c> for a <c>429</c> or <c>503</c>, else <c>global</c>.
    /// </summary>
    public string Domain => Status.Code is 429 or 503 ? "usageLimits" : "global";

    /// <summary>
    /// Whether the answer has a body: every error but <c>304</c>.
    /// </summary>
    public bool HasBody => Status.Code != 304;

    /// <summary>
    /// The row a chosen error status is answered with: the row whose status matches, else
    /// <c>backendError</c> with that status.
    /// </summary>
    /// <param name="status">The status a fault rule chose, from 400 to 599.</param>
    /// <param name="reasonPhrase">The reason phrase for the status line.</param>
    /// <returns>The row.</returns>
    public static GcsJsonError Chosen(int status, string reasonPhrase) =>
        Rows.FirstOrDefault(row => row.Status.Code == status)
        ?? new GcsJsonError(new HttpStatus(status, reasonPhrase), "backendError", "Backend Error.");
}
