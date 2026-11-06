namespace Surl.Extended.Protocol.AzureStorage;

/// <summary>
/// The Data Lake endpoint operations the server serves (ADR-0109, decision 1).
/// </summary>
internal enum AzureDataLakeOperation
{
    /// <summary>
    /// Path - Read: <c>GET /&lt;filesystem&gt;/&lt;path&gt;</c>, or its head with <c>HEAD</c>.
    /// </summary>
    ReadPath,

    /// <summary>
    /// Path - Create: <c>PUT /&lt;filesystem&gt;/&lt;path&gt;?resource=file|directory</c>.
    /// </summary>
    CreatePath,

    /// <summary>
    /// Path - Update: <c>PATCH /&lt;filesystem&gt;/&lt;path&gt;?action=append|flush&amp;position=&lt;n&gt;</c>.
    /// </summary>
    UpdatePath,

    /// <summary>
    /// Path - Delete: <c>DELETE /&lt;filesystem&gt;/&lt;path&gt;</c>.
    /// </summary>
    DeletePath,

    /// <summary>
    /// Path - List: <c>GET /&lt;filesystem&gt;?resource=filesystem&amp;recursive=&lt;bool&gt;</c>.
    /// </summary>
    ListPaths,

    /// <summary>
    /// Filesystem - List: <c>GET /?resource=account</c>.
    /// </summary>
    ListFilesystems,

    /// <summary>
    /// Filesystem - Get Properties: <c>HEAD /&lt;filesystem&gt;?resource=filesystem</c>.
    /// </summary>
    GetFilesystemProperties,

    /// <summary>
    /// Path - Create with <c>x-ms-rename-source</c>: <c>PUT /&lt;filesystem&gt;/&lt;path&gt;</c>, a rename.
    /// </summary>
    RenamePath,

    /// <summary>
    /// Path - Update: <c>PATCH /&lt;filesystem&gt;/&lt;path&gt;?action=setProperties</c>.
    /// </summary>
    SetPathProperties,
}
