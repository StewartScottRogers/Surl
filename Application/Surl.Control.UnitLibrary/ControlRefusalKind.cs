namespace Surl.Control;

/// <summary>Why an <see cref="IControlTarget"/> refused a request, and so which status the endpoint answers.</summary>
public enum ControlRefusalKind
{
    /// <summary>The request is malformed, such as a fault item the reader refuses: <c>400 bad_request</c>.</summary>
    BadRequest,

    /// <summary>The request names something that does not exist: <c>404 not_found</c>.</summary>
    NotFound,

    /// <summary>
    /// The request cannot be carried out in the instance's state, such as a store on disk asked to reset or a
    /// snapshot over its bound: <c>409 conflict</c>.
    /// </summary>
    Conflict,
}
