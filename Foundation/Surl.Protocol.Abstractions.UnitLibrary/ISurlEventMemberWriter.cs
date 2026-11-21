namespace Surl.Protocol.Abstractions;

/// <summary>
/// Where a <see cref="SurlEvent"/> writes its own members (ADR-0171), so a host builds its output without
/// switching on the event's type. Each host supplies a writer that builds its JSON; names arrive as the
/// member's property name and the writer spells them as its format needs.
/// </summary>
public interface ISurlEventMemberWriter
{
    /// <summary>
    /// Writes a text member; a <see langword="null"/> value means the member has no value.
    /// </summary>
    /// <param name="name">The member's property name.</param>
    /// <param name="value">The text, or <see langword="null"/>.</param>
    void Text(string name, string? value);

    /// <summary>
    /// Writes a member that is a list of text values; a <see langword="null"/> list means the member has no value.
    /// </summary>
    /// <param name="name">The member's property name.</param>
    /// <param name="values">The texts in order, or <see langword="null"/>.</param>
    void TextList(string name, IReadOnlyList<string>? values);

    /// <summary>
    /// Writes a whole-number member; a <see langword="null"/> value means the member has no value.
    /// </summary>
    /// <param name="name">The member's property name.</param>
    /// <param name="value">The number, or <see langword="null"/>.</param>
    void Number(string name, long? value);

    /// <summary>
    /// Writes a true-or-false member.
    /// </summary>
    /// <param name="name">The member's property name.</param>
    /// <param name="value">The flag.</param>
    void Flag(string name, bool value);

    /// <summary>
    /// Writes a member that is one value of an enumeration.
    /// </summary>
    /// <param name="name">The member's property name.</param>
    /// <param name="value">The enumeration value; the writer spells it by name.</param>
    void Choice(string name, Enum value);

    /// <summary>
    /// Writes a member that is bytes; the writer bounds how many it keeps.
    /// </summary>
    /// <param name="name">The member's property name.</param>
    /// <param name="value">The bytes.</param>
    void Bytes(string name, ReadOnlyMemory<byte> value);
}
