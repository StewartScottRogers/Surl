using System.Buffers;
using System.Text.Unicode;

namespace Surl.Cli;

/// <summary>
/// Reads one <c>--shared-folder &lt;name&gt;=&lt;member&gt;,...</c> argument (ADR-0101 decisions 3 to 5, ADR-0102
/// decision 1.6) into a <see cref="ConfiguredSharedFolder"/>: the item by <see cref="Shape"/>, the name a mailbox
/// name (<see cref="CheckFolderName"/>) other than <c>INBOX</c>, then a member named twice refused with row 14's
/// text. A name given twice is <see cref="ConfiguredMailConflicts"/>' to check; whether each member names an
/// account or group, and <c>@everyone</c>, are <c>Surl.Composition</c>'s (BL-994).
/// </summary>
internal static class ConfiguredSharedFolderReader
{
    /// <summary>The longest mailbox name, in UTF-8 bytes: the mail store's bound (ADR-0050 decision 3).</summary>
    public const int MaxFolderNameBytes = 1024;

    /// <summary>A folder name past <see cref="MaxFolderNameBytes"/> (ADR-0101 decision 5 row 10).</summary>
    public const string FolderNameTooLong = "a folder name is longer than 1024 bytes";

    /// <summary>A folder name holding a lone UTF-16 surrogate, which no UTF-8 mailbox name can (ADR-0101 decision 5 row 10).</summary>
    public const string FolderNameNotUtf8 = "a folder name is not UTF-8";

    /// <summary>The <c>--shared-folder</c> item: a name, <c>=</c> and a member list, with no keys or flags.</summary>
    public static readonly ConfigurationItemShape Shape = new(
        "<name>=<member>,...",
        '=',
        ConfigurationItemValueKind.Members,
        new Dictionary<string, ConfigurationItemAttributeKind>(StringComparer.Ordinal),
        []);

    /// <summary><see cref="Read"/> with the item's argument type (ADR-0046 decision 6).</summary>
    public static readonly OptionArgumentReading<ConfiguredSharedFolder> Reading = new(Read, Shape.Reading.Type);

    /// <summary>A shared folder named <c>INBOX</c> in any case, which every owner has already (ADR-0102 decision 1.6).</summary>
    /// <param name="name">The name as written.</param>
    /// <returns><c>shared folder &lt;name&gt; cannot be INBOX</c>.</returns>
    public static string SharedFolderIsInbox(string name) => $"shared folder {name} cannot be INBOX";

    /// <summary>A shared folder name an earlier <c>--shared-folder</c> already gave (ADR-0101 decision 5 row 14).</summary>
    /// <param name="name">The folder's name.</param>
    /// <returns><c>shared folder &lt;name&gt; is given twice</c>.</returns>
    public static string SharedFolderGivenTwice(string name) => $"shared folder {name} is given twice";

    /// <summary>
    /// Checks a decoded mailbox name, already non-empty and free of control characters, as the mail store's
    /// ADR-0050 decision 3 rules do: at most <see cref="MaxFolderNameBytes"/> UTF-8 bytes, and UTF-16 that
    /// converts to UTF-8. <c>folders=</c> of <c>--mailbox</c> shares it.
    /// </summary>
    /// <param name="name">The name, decoded.</param>
    /// <returns>The refusal reason, or <see langword="null"/>.</returns>
    public static string? CheckFolderName(string name)
    {
        Span<byte> utf8 = stackalloc byte[MaxFolderNameBytes];
        return Utf8.FromUtf16(name, utf8, out _, out _, replaceInvalidSequences: false) switch
        {
            OperationStatus.Done => null,
            OperationStatus.DestinationTooSmall => FolderNameTooLong,
            _ => FolderNameNotUtf8,
        };
    }

    /// <summary>Reads one <c>--shared-folder</c> argument.</summary>
    /// <param name="argument">The argument as given.</param>
    /// <param name="value">The shared folder, when accepted; what was read before the refusal otherwise.</param>
    /// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
    public static string? Read(string argument, out ConfiguredSharedFolder value)
    {
        var failure = ConfigurationItemReader.Read(Shape, argument, out var item);
        value = new(item.Name, item.Members);
        if (failure is not null)
        {
            return failure;
        }

        if (string.Equals(item.Name, "INBOX", StringComparison.OrdinalIgnoreCase))
        {
            return SharedFolderIsInbox(item.Name);
        }

        var seen = new HashSet<ConfiguredMember>();
        var repeated = item.Members.FirstOrDefault(member => !seen.Add(member));
        return CheckFolderName(item.Name) ?? (repeated is null ? null : ConfiguredGroupReader.MemberGivenTwice(repeated.AsWritten));
    }
}
