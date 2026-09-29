using Surl.Content;

namespace Surl.Protocol.Dict;

/// <summary>
/// The file that holds one headword's definition.
/// </summary>
/// <param name="Mapping">Where the content store found the file.</param>
/// <param name="Status">The file's length and modification time when it was found.</param>
internal sealed record DictDefinitionFile(ContentPathMapping Mapping, ContentFileStatus Status);
