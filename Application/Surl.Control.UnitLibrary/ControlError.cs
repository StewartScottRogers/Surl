using Surl.HttpMessage;

namespace Surl.Control;

/// <summary>
/// An error answer (ADR-0124 decision 7): its status, its <c>error</c> code and its <c>message</c>, and whether the
/// connection closes after it.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Code">The <c>error</c> code, such as <c>bad_request</c>.</param>
/// <param name="Message">The <c>message</c>: never a token or a password.</param>
/// <param name="ClosesConnection">Whether the server reads no more requests on the connection.</param>
internal sealed record ControlError(HttpStatus Status, string Code, string Message, bool ClosesConnection = false)
{
    /// <summary><c>201 Created</c>.</summary>
    public static readonly HttpStatus Created = new(201, "Created");

    /// <summary><c>204 No Content</c>.</summary>
    public static readonly HttpStatus NoContent = new(204, "No Content");

    /// <summary><c>409 Conflict</c>.</summary>
    public static readonly HttpStatus ConflictStatus = new(409, "Conflict");

    /// <summary><c>415 Unsupported Media Type</c>.</summary>
    public static readonly HttpStatus UnsupportedMediaTypeStatus = new(415, "Unsupported Media Type");

    /// <summary>A malformed query, body, JSON or fault item.</summary>
    /// <param name="message">What is wrong.</param>
    /// <returns><c>400 bad_request</c>.</returns>
    public static ControlError BadRequest(string message) => new(HttpStatus.BadRequest, "bad_request", message);

    /// <summary>A request without the token.</summary>
    /// <returns><c>401 unauthorized</c>, closing the connection.</returns>
    public static ControlError Unauthorized() =>
        new(HttpStatus.Unauthorized, "unauthorized", "the request needs the control token as Authorization: Bearer <token>", ClosesConnection: true);

    /// <summary>A peer that is not loopback.</summary>
    /// <returns><c>403 forbidden</c>, closing the connection.</returns>
    public static ControlError Forbidden() =>
        new(HttpStatus.Forbidden, "forbidden", "the control endpoint answers only a loopback peer", ClosesConnection: true);

    /// <summary>An unknown path, fault, snapshot, file or account.</summary>
    /// <param name="message">What was not found.</param>
    /// <returns><c>404 not_found</c>.</returns>
    public static ControlError NotFound(string message) => new(HttpStatus.NotFound, "not_found", message);

    /// <summary>A known path asked with a method it does not take.</summary>
    /// <param name="message">Which methods it takes.</param>
    /// <returns><c>405 method_not_allowed</c>.</returns>
    public static ControlError MethodNotAllowed(string message) => new(HttpStatus.MethodNotAllowed, "method_not_allowed", message);

    /// <summary>A request the instance's state does not allow.</summary>
    /// <param name="message">Why.</param>
    /// <returns><c>409 conflict</c>.</returns>
    public static ControlError Conflict(string message) => new(ConflictStatus, "conflict", message);

    /// <summary>A body past the bound.</summary>
    /// <returns><c>413 too_large</c>, closing the connection.</returns>
    public static ControlError TooLarge() =>
        new(HttpStatus.ContentTooLarge, "too_large", $"a request body is at most {ControlEndpoint.MaxBodyBytes} bytes", ClosesConnection: true);

    /// <summary>A body of a media type the request does not take.</summary>
    /// <param name="expected">The media type it takes.</param>
    /// <returns><c>415 unsupported_media_type</c>.</returns>
    public static ControlError UnsupportedMediaType(string expected) =>
        new(UnsupportedMediaTypeStatus, "unsupported_media_type", $"the body must be {expected}");

    /// <summary>The answer to a target's refusal.</summary>
    /// <param name="refusal">The refusal.</param>
    /// <returns>Its status and message.</returns>
    public static ControlError Of(ControlRefusalException refusal) => refusal.Kind switch
    {
        ControlRefusalKind.NotFound => NotFound(refusal.Message),
        ControlRefusalKind.Conflict => Conflict(refusal.Message),
        _ => BadRequest(refusal.Message),
    };
}
