namespace Surl.Extended.Protocol.S3;

/// <summary>
/// An error with the extra elements its <c>Error</c> body carries, such as <c>ArgumentName</c>.
/// </summary>
/// <param name="Error">The error.</param>
/// <param name="ExtraElements">The elements the code carries beside the common ones.</param>
internal sealed record S3Refusal(S3Error Error, KeyValuePair<string, string>[] ExtraElements);
