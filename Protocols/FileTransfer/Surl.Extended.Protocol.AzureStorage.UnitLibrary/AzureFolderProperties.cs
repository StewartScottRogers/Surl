using System.Globalization;
using Surl.HttpMessage;
using Surl.HttpServing;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// Answers the property requests on a folder (ADR-0109, decision 1): Get Container Properties,
/// Filesystem - Get Properties and Path - Get Properties of a directory - <c>200</c> with the
/// folder's <c>ETag</c> (<see cref="AzurePathCreate.DirectoryETag"/>) and <c>Last-Modified</c>, the
/// fields the page names for what the folder is, and no body.
/// </summary>
internal static class AzureFolderProperties
{
    // "Get Container Properties": no lease, no immutability policy, no legal hold.
    private static readonly KeyValuePair<string, string>[] ContainerFields =
    [
        new("x-ms-lease-status", "unlocked"),
        new("x-ms-lease-state", "available"),
        new("x-ms-has-immutability-policy", "false"),
        new("x-ms-has-legal-hold", "false"),
    ];

    // "Filesystem - Get Properties": the hierarchical namespace is on, as the Data Lake endpoint serves it.
    private static readonly KeyValuePair<string, string>[] FilesystemFields = [new("x-ms-namespace-enabled", "true")];

    // "Path - Get Properties" of a directory.
    private static readonly KeyValuePair<string, string>[] DirectoryFields = [new("x-ms-resource-type", "directory")];

    /// <summary>
    /// Get Container Properties of an existing container.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="lastModified">The container folder's last write time.</param>
    /// <returns>The answer.</returns>
    public static HttpAnswer Container(AzureReply reply, DateTimeOffset lastModified) => Answer(reply, lastModified, ContainerFields);

    /// <summary>
    /// Filesystem - Get Properties of an existing filesystem.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="lastModified">The filesystem folder's last write time.</param>
    /// <returns>The answer.</returns>
    public static HttpAnswer Filesystem(AzureReply reply, DateTimeOffset lastModified) => Answer(reply, lastModified, FilesystemFields);

    /// <summary>
    /// Path - Get Properties of an existing directory.
    /// </summary>
    /// <param name="reply">The request's answers.</param>
    /// <param name="lastModified">The directory's last write time.</param>
    /// <returns>The answer.</returns>
    public static HttpAnswer Directory(AzureReply reply, DateTimeOffset lastModified) => Answer(reply, lastModified, DirectoryFields);

    private static HttpAnswer Answer(AzureReply reply, DateTimeOffset lastModified, KeyValuePair<string, string>[] kindFields) =>
        reply.Answer(
            HttpStatus.Ok,
            [
                new("Content-Length", "0"),
                new("ETag", AzurePathCreate.DirectoryETag(lastModified)),
                new("Last-Modified", lastModified.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)),
                .. kindFields,
            ],
            HttpAnswerBody.None);
}
