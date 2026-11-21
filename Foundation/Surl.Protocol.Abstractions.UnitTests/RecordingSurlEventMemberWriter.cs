namespace Surl.Protocol.Abstractions;

/// <summary>
/// An <see cref="ISurlEventMemberWriter"/> that keeps each member as a name and its text, in the order written.
/// </summary>
internal sealed class RecordingSurlEventMemberWriter : ISurlEventMemberWriter
{
    public List<(string Name, string? Value)> Members { get; } = [];

    public void Text(string name, string? value) => Members.Add((name, value));

    public void TextList(string name, IReadOnlyList<string>? values) => Members.Add((name, values is null ? null : string.Join("|", values)));

    public void Number(string name, long? value) => Members.Add((name, value?.ToString()));

    public void Flag(string name, bool value) => Members.Add((name, value ? "true" : "false"));

    public void Choice(string name, Enum value) => Members.Add((name, value.ToString()));

    public void Bytes(string name, ReadOnlyMemory<byte> value) => Members.Add((name, Convert.ToHexString(value.Span)));

    public string? ValueOf(string name) => Members.Single(member => member.Name == name).Value;
}
