using Surl.HttpMessage;

namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// One row of the "Common REST API error codes" or "Blob Storage error codes" tables the Blob
/// endpoint sends: the code, the status the table gives it, and the fixed first line of its
/// message (ADR-0109, decision 5.1; ADR-0006, section 3).
/// </summary>
/// <param name="Code">The <c>Code</c> element's text and the <c>x-ms-error-code</c> value.</param>
/// <param name="Status">The status the table gives the code.</param>
/// <param name="Message">The <c>Message</c> element's fixed first line.</param>
internal sealed record AzureError(string Code, HttpStatus Status, string Message)
{
    private static readonly HttpStatus PreconditionFailedStatus = new(412, "Precondition Failed");
    private static readonly HttpStatus RangeNotSatisfiableStatus = new(416, "Range Not Satisfiable");
    private static readonly HttpStatus ConflictStatus = new(409, "Conflict");
    private static readonly HttpStatus LengthRequiredStatus = new(411, "Length Required");

    /// <summary>
    /// <c>404 BlobNotFound</c>: no file at the blob name, or the name is a folder.
    /// </summary>
    public static AzureError BlobNotFound { get; } = new("BlobNotFound", HttpStatus.NotFound, "The specified blob does not exist.");

    /// <summary>
    /// <c>404 ContainerNotFound</c>: the container's top-level folder does not exist.
    /// </summary>
    public static AzureError ContainerNotFound { get; } = new("ContainerNotFound", HttpStatus.NotFound, "The specified container does not exist.");

    /// <summary>
    /// <c>400 MissingRequiredHeader</c>: an authenticated request without <c>x-ms-version</c>.
    /// </summary>
    public static AzureError MissingRequiredHeader { get; } = new("MissingRequiredHeader", HttpStatus.BadRequest, "An HTTP header that's mandatory for this request is not specified.");

    /// <summary>
    /// <c>400 InvalidHeaderValue</c>: an <c>x-ms-version</c> or <c>x-ms-date</c> that does not parse.
    /// </summary>
    public static AzureError InvalidHeaderValue { get; } = new("InvalidHeaderValue", HttpStatus.BadRequest, "The value provided for one of the HTTP headers was not in the correct format.");

    /// <summary>
    /// <c>400 UnsupportedHeader</c>: a header asking for a feature the server does not have.
    /// </summary>
    public static AzureError UnsupportedHeader { get; } = new("UnsupportedHeader", HttpStatus.BadRequest, "One of the HTTP headers specified in the request is not supported.");

    /// <summary>
    /// <c>400 InvalidQueryParameterValue</c>: a <c>comp</c>, <c>restype</c>, <c>snapshot</c> or
    /// <c>versionid</c> the server does not serve.
    /// </summary>
    public static AzureError InvalidQueryParameterValue { get; } = new("InvalidQueryParameterValue", HttpStatus.BadRequest, "An invalid value was specified for one of the query parameters in the request URI.");

    /// <summary>
    /// <c>400 InvalidUri</c>: a path that names no blob, with no query that names an operation.
    /// </summary>
    public static AzureError InvalidUri { get; } = new("InvalidUri", HttpStatus.BadRequest, "The requested URI does not represent any resource on the server.");

    /// <summary>
    /// <c>405 UnsupportedHttpVerb</c>: a method the resource does not take.
    /// </summary>
    public static AzureError UnsupportedHttpVerb { get; } = new("UnsupportedHttpVerb", HttpStatus.MethodNotAllowed, "The resource doesn't support the specified HTTP verb.");

    /// <summary>
    /// <c>400 InvalidResourceName</c>: a blob name longer than 1024 characters.
    /// </summary>
    public static AzureError InvalidResourceName { get; } = new("InvalidResourceName", HttpStatus.BadRequest, "The specified resource name contains invalid characters.");

    /// <summary>
    /// <c>400 OutOfRangeInput</c>: <c>x-ms-range-get-content-md5: true</c> on a range over 4 MiB.
    /// </summary>
    public static AzureError OutOfRangeInput { get; } = new("OutOfRangeInput", HttpStatus.BadRequest, "One of the request inputs is out of range.");

    /// <summary>
    /// <c>401 NoAuthenticationInformation</c>: no <c>Authorization</c> with anonymous off, or an
    /// anonymous write ("Authorize with Microsoft Entra ID", "Bearer Challenge").
    /// </summary>
    public static AzureError NoAuthenticationInformation { get; } = new("NoAuthenticationInformation", HttpStatus.Unauthorized, "Server failed to authenticate the request. Please refer to the information in the www-authenticate header.");

    /// <summary>
    /// <c>401 InvalidAuthenticationInfo</c>: a bearer token that is no account.
    /// </summary>
    public static AzureError InvalidBearerToken { get; } = new("InvalidAuthenticationInfo", HttpStatus.Unauthorized, "Server failed to authenticate the request. Please refer to the information in the www-authenticate header.");

    /// <summary>
    /// <c>400 InvalidAuthenticationInfo</c>: an <c>Authorization</c> with a scheme the server does
    /// not serve, or one that does not parse (ADR-0109, decision 3.4).
    /// </summary>
    public static AzureError MalformedAuthorization { get; } = new("InvalidAuthenticationInfo", HttpStatus.BadRequest, "Authentication information is not given in the correct format. Check the value of Authorization header.");

    /// <summary>
    /// <c>403 AuthenticationFailed</c>: a bearer request naming a version before 2017-11-09.
    /// </summary>
    public static AzureError AuthenticationFailed { get; } = new("AuthenticationFailed", HttpStatus.Forbidden, "Server failed to authenticate the request. Make sure the value of Authorization header is formed correctly including the signature.");

    /// <summary>
    /// <c>412 ConditionNotMet</c>: a failed <c>If-Match</c> or <c>If-Unmodified-Since</c>.
    /// </summary>
    public static AzureError ConditionNotMet { get; } = new("ConditionNotMet", PreconditionFailedStatus, "The condition specified using HTTP conditional header(s) is not met.");

    /// <summary>
    /// <c>416 InvalidRange</c>: a single range that names no byte of the blob.
    /// </summary>
    public static AzureError InvalidRange { get; } = new("InvalidRange", RangeNotSatisfiableStatus, "The range specified is invalid for the current size of the resource.");

    /// <summary>
    /// <c>409 InvalidResourceName</c>: a blob name whose folder is a file, or that names a folder
    /// (ADR-0109, decision 2).
    /// </summary>
    public static AzureError ResourceNameConflict { get; } = new("InvalidResourceName", ConflictStatus, "The specified resource name contains invalid characters.");

    /// <summary>
    /// <c>403 AuthorizationPermissionMismatch</c>: a write with uploads off or share access refusing
    /// it, or a listing with listings off (ADR-0109, decisions 2 and 3.4).
    /// </summary>
    public static AzureError AuthorizationPermissionMismatch { get; } = new("AuthorizationPermissionMismatch", HttpStatus.Forbidden, "This request is not authorized to perform this operation using this permission.");

    /// <summary>
    /// <c>411 MissingContentLengthHeader</c>: a Put Blob with neither <c>Content-Length</c> nor a chunked body.
    /// </summary>
    public static AzureError MissingContentLengthHeader { get; } = new("MissingContentLengthHeader", LengthRequiredStatus, "The Content-Length header was not specified.");

    /// <summary>
    /// <c>400 InvalidMd5</c>: a <c>Content-MD5</c> that is not the base64 of 16 bytes.
    /// </summary>
    public static AzureError InvalidMd5 { get; } = new("InvalidMd5", HttpStatus.BadRequest, "The MD5 value specified in the request is invalid. The MD5 value must be 128 bits and Base64-encoded.");

    /// <summary>
    /// <c>400 Md5Mismatch</c>: a <c>Content-MD5</c> that is not the body's MD5.
    /// </summary>
    public static AzureError Md5Mismatch { get; } = new("Md5Mismatch", HttpStatus.BadRequest, "The MD5 value specified in the request did not match the MD5 value calculated by the server.");

    /// <summary>
    /// <c>400 MetadataTooLarge</c>: <c>x-ms-meta-*</c> names and values past 8 KiB (ADR-0109, decision 7).
    /// </summary>
    public static AzureError MetadataTooLarge { get; } = new("MetadataTooLarge", HttpStatus.BadRequest, "The size of the specified metadata exceeds the maximum size permitted.");

    /// <summary>
    /// <c>400 InvalidMetadata</c>: an <c>x-ms-meta-*</c> name that is not a C# identifier (ADR-0109, decision 7).
    /// </summary>
    public static AzureError InvalidMetadata { get; } = new("InvalidMetadata", HttpStatus.BadRequest, "The metadata specified is invalid. It has characters that are not permitted.");

    /// <summary>
    /// <c>413 RequestBodyTooLarge</c>: a Put Blob body past <c>--max-filesize</c> (ADR-0109, decision 7).
    /// </summary>
    public static AzureError RequestBodyTooLarge { get; } = new("RequestBodyTooLarge", HttpStatus.ContentTooLarge, "The size of the request body exceeds the maximum size permitted.");

    /// <summary>
    /// <c>400 InvalidInput</c>: a body the server cannot read, or one cut short.
    /// </summary>
    public static AzureError InvalidInput { get; } = new("InvalidInput", HttpStatus.BadRequest, "One of the request inputs is not valid.");

    /// <summary>
    /// <c>400 OutOfRangeQueryParameterValue</c>: a List Blobs <c>maxresults</c> of 0 or below.
    /// </summary>
    public static AzureError OutOfRangeQueryParameterValue { get; } = new("OutOfRangeQueryParameterValue", HttpStatus.BadRequest, "A query parameter specified in the request URI is outside the permissible range.");

    /// <summary>
    /// <c>404 PathNotFound</c> (Data Lake): no file or directory at the path, or a Path - List
    /// <c>directory</c> that does not exist.
    /// </summary>
    public static AzureError PathNotFound { get; } = new("PathNotFound", HttpStatus.NotFound, "The specified path does not exist.");

    /// <summary>
    /// <c>404 FilesystemNotFound</c> (Data Lake): the filesystem's top-level folder does not exist.
    /// </summary>
    public static AzureError FilesystemNotFound { get; } = new("FilesystemNotFound", HttpStatus.NotFound, "The specified filesystem does not exist.");

    /// <summary>
    /// <c>400 InvalidFlushPosition</c> (Data Lake): a flush whose position the staged appends do not
    /// reach without a gap or an overlap (ADR-0109, decision 5.5).
    /// </summary>
    public static AzureError InvalidFlushPosition { get; } = new("InvalidFlushPosition", HttpStatus.BadRequest, "The uploaded data is not contiguous or the position query parameter value is not equal to the length of the file after appending the uploaded data.");

    /// <summary>
    /// <c>409 ResourceTypeMismatch</c> (Data Lake): a read, append or flush of a directory.
    /// </summary>
    public static AzureError ResourceTypeMismatch { get; } = new("ResourceTypeMismatch", ConflictStatus, "The resource type specified in the request does not match the type of the resource.");

    /// <summary>
    /// <c>409 PathConflict</c> (Data Lake): a path a file stands in the way of, or a file created
    /// over a directory or a directory over a file (ADR-0109, decision 2).
    /// </summary>
    public static AzureError PathConflict { get; } = new("PathConflict", ConflictStatus, "The specified path, or an element of the path, exists and its resource type is invalid for this operation.");

    /// <summary>
    /// <c>409 DirectoryNotEmpty</c> (Data Lake): Path - Delete of a non-empty directory without
    /// <c>recursive=true</c>.
    /// </summary>
    public static AzureError DirectoryNotEmpty { get; } = new("DirectoryNotEmpty", ConflictStatus, "The recursive query parameter value must be true to delete a non-empty directory.");

    /// <summary>
    /// <c>400 ContentLengthMustBeZero</c> (Data Lake): a flush with a body.
    /// </summary>
    public static AzureError ContentLengthMustBeZero { get; } = new("ContentLengthMustBeZero", HttpStatus.BadRequest, "The Content-Length request header must be zero.");

    /// <summary>
    /// <c>400 MissingRequiredQueryParameter</c>: a request without a parameter its operation needs,
    /// such as <c>position</c> or <c>recursive</c> (ADR-0109, decision 4).
    /// </summary>
    public static AzureError MissingRequiredQueryParameter { get; } = new("MissingRequiredQueryParameter", HttpStatus.BadRequest, "A query parameter that's mandatory for this request is not specified.");

    /// <summary>
    /// <c>503 ServerBusy</c>: a connection past <c>--max-connections</c> or
    /// <c>--max-connections-per-address</c> (ADR-0109, decision 7).
    /// </summary>
    public static AzureError ServerBusy { get; } = new("ServerBusy", HttpStatus.ServiceUnavailable, "The server is currently unable to receive requests. Please retry your request.");

    /// <summary>
    /// <c>400 InvalidBlockId</c>: a Put Block whose <c>blockid</c> is not base64 of at most 64
    /// bytes, or not as long as the blob's other block IDs ("Put Block").
    /// </summary>
    public static AzureError InvalidBlockId { get; } = new("InvalidBlockId", HttpStatus.BadRequest, "The specified block ID is invalid. The block ID must be Base64-encoded.");

    /// <summary>
    /// <c>400 InvalidBlockList</c>: a Put Block List naming a block the blob does not hold in the
    /// list it is looked for in ("Put Block List").
    /// </summary>
    public static AzureError InvalidBlockList { get; } = new("InvalidBlockList", HttpStatus.BadRequest, "The specified block list is invalid.");

    /// <summary>
    /// <c>409 BlockCountExceedsLimit</c>: a Put Block past 100000 uncommitted blocks (ADR-0109, decision 7).
    /// </summary>
    public static AzureError BlockCountExceedsLimit { get; } = new("BlockCountExceedsLimit", ConflictStatus, "The uncommitted block count cannot exceed the maximum limit of 100,000 blocks.");

    /// <summary>
    /// <c>400 BlockListTooLong</c>: a Put Block List past 50000 entries (ADR-0109, decision 7).
    /// </summary>
    public static AzureError BlockListTooLong { get; } = new("BlockListTooLong", HttpStatus.BadRequest, "The block list may not contain more than 50,000 blocks.");

    /// <summary>
    /// <c>400 InvalidXmlDocument</c>: a Put Block List body that is not a <c>BlockList</c>
    /// document, or is past 4 MiB (ADR-0109, decision 7).
    /// </summary>
    public static AzureError InvalidXmlDocument { get; } = new("InvalidXmlDocument", HttpStatus.BadRequest, "XML specified is not syntactically valid.");

    /// <summary>
    /// <c>404 SourcePathNotFound</c> (Data Lake): a rename whose <c>x-ms-rename-source</c> names no
    /// file or directory the caller may read ("Path - Create").
    /// </summary>
    public static AzureError SourcePathNotFound { get; } = new("SourcePathNotFound", HttpStatus.NotFound, "The source path for a rename operation does not exist.");

    /// <summary>
    /// <c>400 InvalidSourceUri</c> (Data Lake): an <c>x-ms-rename-source</c> that names no path in a
    /// filesystem ("Path - Create").
    /// </summary>
    public static AzureError InvalidSourceUri { get; } = new("InvalidSourceUri", HttpStatus.BadRequest, "The source URI is invalid.");

    /// <summary>
    /// <c>400 InvalidRenameSourcePath</c> (Data Lake): a directory renamed into itself ("Path - Create").
    /// </summary>
    public static AzureError InvalidRenameSourcePath { get; } = new("InvalidRenameSourcePath", HttpStatus.BadRequest, "The source directory cannot be the same as the destination directory, nor can the destination be a subdirectory of the source directory.");

    /// <summary>
    /// <c>404 RenameDestinationParentPathNotFound</c> (Data Lake): a rename whose destination's
    /// parent directory does not exist ("Path - Create").
    /// </summary>
    public static AzureError RenameDestinationParentPathNotFound { get; } = new("RenameDestinationParentPathNotFound", HttpStatus.NotFound, "The parent directory of the destination path does not exist.");

    /// <summary>
    /// <c>409 PathAlreadyExists</c> (Data Lake): a rename onto a directory, onto a file when the
    /// source is a directory, or onto any path with <c>If-None-Match: *</c> ("Path - Create").
    /// </summary>
    public static AzureError PathAlreadyExists { get; } = new("PathAlreadyExists", ConflictStatus, "The specified path already exists.");
}
