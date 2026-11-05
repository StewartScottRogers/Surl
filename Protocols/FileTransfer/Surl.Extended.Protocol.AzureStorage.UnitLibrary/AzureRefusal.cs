namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// An error with the elements its code carries beside <c>Code</c> and <c>Message</c>, such as
/// <c>HeaderName</c> (ADR-0109, decision 5.1).
/// </summary>
/// <param name="Error">The error.</param>
/// <param name="Elements">The extra elements, in the order they are written.</param>
internal sealed record AzureRefusal(AzureError Error, IReadOnlyList<AzureErrorElement> Elements)
{
    /// <summary>
    /// The error with no extra element.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal Of(AzureError error) => new(error, []);

    /// <summary>
    /// The error with <c>HeaderName</c> naming <paramref name="headerName"/>.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="headerName">The header's name, as the server spells it.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal ForHeader(AzureError error, string headerName) =>
        new(error, [AzureErrorElement.Fixed("HeaderName", headerName)]);

    /// <summary>
    /// <c>InvalidHeaderValue</c> with <c>HeaderName</c> and the client's <c>HeaderValue</c>.
    /// </summary>
    /// <param name="headerName">The header's name, as the server spells it.</param>
    /// <param name="headerValue">The value the client sent.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal InvalidHeaderValue(string headerName, string headerValue) =>
        new(AzureError.InvalidHeaderValue, [AzureErrorElement.Fixed("HeaderName", headerName), AzureErrorElement.Echoed("HeaderValue", headerValue)]);

    /// <summary>
    /// <c>InvalidQueryParameterValue</c> with <c>QueryParameterName</c>, the client's
    /// <c>QueryParameterValue</c> and a fixed <c>Reason</c>.
    /// </summary>
    /// <param name="parameterName">The parameter's name, as the server spells it.</param>
    /// <param name="parameterValue">The value the client sent, percent-decoded.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal InvalidQueryParameterValue(string parameterName, string parameterValue) =>
        InvalidQueryParameterValue(parameterName, parameterValue, "This operation is not served by this server.");

    /// <summary>
    /// <c>InvalidQueryParameterValue</c> with <c>QueryParameterName</c>, the client's
    /// <c>QueryParameterValue</c> and the fixed <paramref name="reason"/>.
    /// </summary>
    /// <param name="parameterName">The parameter's name, as the server spells it.</param>
    /// <param name="parameterValue">The value the client sent, percent-decoded.</param>
    /// <param name="reason">The fixed sentence naming what is wrong with the value.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal InvalidQueryParameterValue(string parameterName, string parameterValue, string reason) =>
        new(
            AzureError.InvalidQueryParameterValue,
            [
                AzureErrorElement.Fixed("QueryParameterName", parameterName),
                AzureErrorElement.Echoed("QueryParameterValue", parameterValue),
                AzureErrorElement.Fixed("Reason", reason),
            ]);

    /// <summary>
    /// <c>OutOfRangeQueryParameterValue</c> with <c>QueryParameterName</c>, the client's
    /// <c>QueryParameterValue</c> and the <c>MinimumAllowed</c> value.
    /// </summary>
    /// <param name="parameterName">The parameter's name, as the server spells it.</param>
    /// <param name="parameterValue">The value the client sent, percent-decoded.</param>
    /// <param name="minimumAllowed">The least value the parameter takes.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal OutOfRangeQueryParameterValue(string parameterName, string parameterValue, string minimumAllowed) =>
        new(
            AzureError.OutOfRangeQueryParameterValue,
            [
                AzureErrorElement.Fixed("QueryParameterName", parameterName),
                AzureErrorElement.Echoed("QueryParameterValue", parameterValue),
                AzureErrorElement.Fixed("MinimumAllowed", minimumAllowed),
            ]);

    /// <summary>
    /// <c>MissingRequiredQueryParameter</c> with <c>QueryParameterName</c>.
    /// </summary>
    /// <param name="parameterName">The parameter's name, as the server spells it.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal MissingRequiredQueryParameter(string parameterName) =>
        new(AzureError.MissingRequiredQueryParameter, [AzureErrorElement.Fixed("QueryParameterName", parameterName)]);

    /// <summary>
    /// <c>AuthenticationFailed</c> with its fixed <c>AuthenticationErrorDetail</c> (ADR-0109, decision 3.4).
    /// </summary>
    /// <param name="detail">The fixed sentence naming the cause.</param>
    /// <returns>The refusal.</returns>
    public static AzureRefusal AuthenticationFailed(string detail) =>
        new(AzureError.AuthenticationFailed, [AzureErrorElement.Fixed("AuthenticationErrorDetail", detail)]);
}
