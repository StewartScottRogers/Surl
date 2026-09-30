namespace Surl.Protocol.Ssh;

/// <summary>
/// The packet types of SFTP version 3 (draft-ietf-secsh-filexfer-02, section 3), with the names
/// the verbose notes use for them (ADR-0054, decision 13).
/// </summary>
internal static class SftpPacketType
{
    /// <summary><c>SSH_FXP_INIT</c>: the client's first packet, carrying its version.</summary>
    public const byte Init = 1;

    /// <summary><c>SSH_FXP_VERSION</c>: the server's answer to <c>INIT</c>.</summary>
    public const byte Version = 2;

    /// <summary><c>SSH_FXP_OPEN</c>.</summary>
    public const byte Open = 3;

    /// <summary><c>SSH_FXP_CLOSE</c>.</summary>
    public const byte Close = 4;

    /// <summary><c>SSH_FXP_READ</c>.</summary>
    public const byte Read = 5;

    /// <summary><c>SSH_FXP_WRITE</c>.</summary>
    public const byte Write = 6;

    /// <summary><c>SSH_FXP_LSTAT</c>.</summary>
    public const byte LinkStat = 7;

    /// <summary><c>SSH_FXP_FSTAT</c>.</summary>
    public const byte HandleStat = 8;

    /// <summary><c>SSH_FXP_SETSTAT</c>.</summary>
    public const byte SetStat = 9;

    /// <summary><c>SSH_FXP_FSETSTAT</c>.</summary>
    public const byte HandleSetStat = 10;

    /// <summary><c>SSH_FXP_OPENDIR</c>.</summary>
    public const byte OpenDirectory = 11;

    /// <summary><c>SSH_FXP_READDIR</c>.</summary>
    public const byte ReadDirectory = 12;

    /// <summary><c>SSH_FXP_REMOVE</c>.</summary>
    public const byte Remove = 13;

    /// <summary><c>SSH_FXP_MKDIR</c>.</summary>
    public const byte MakeDirectory = 14;

    /// <summary><c>SSH_FXP_RMDIR</c>.</summary>
    public const byte RemoveDirectory = 15;

    /// <summary><c>SSH_FXP_REALPATH</c>.</summary>
    public const byte RealPath = 16;

    /// <summary><c>SSH_FXP_STAT</c>.</summary>
    public const byte Stat = 17;

    /// <summary><c>SSH_FXP_RENAME</c>.</summary>
    public const byte Rename = 18;

    /// <summary><c>SSH_FXP_READLINK</c>.</summary>
    public const byte ReadLink = 19;

    /// <summary><c>SSH_FXP_SYMLINK</c>.</summary>
    public const byte SymbolicLink = 20;

    /// <summary><c>SSH_FXP_STATUS</c>.</summary>
    public const byte Status = 101;

    /// <summary><c>SSH_FXP_HANDLE</c>.</summary>
    public const byte Handle = 102;

    /// <summary><c>SSH_FXP_DATA</c>.</summary>
    public const byte Data = 103;

    /// <summary><c>SSH_FXP_NAME</c>.</summary>
    public const byte Name = 104;

    /// <summary><c>SSH_FXP_ATTRS</c>.</summary>
    public const byte Attributes = 105;

    /// <summary><c>SSH_FXP_EXTENDED</c>.</summary>
    public const byte Extended = 200;

    private static readonly string?[] RequestNames =
    [
        null, "INIT", "VERSION", "OPEN", "CLOSE", "READ", "WRITE", "LSTAT", "FSTAT", "SETSTAT", "FSETSTAT",
        "OPENDIR", "READDIR", "REMOVE", "MKDIR", "RMDIR", "REALPATH", "STAT", "RENAME", "READLINK", "SYMLINK",
    ];

    /// <summary>
    /// The name a note gives a packet type: <c>OPEN</c> for 3, <c>EXTENDED</c> for 200, and
    /// <c>type &lt;n&gt;</c> for a type draft-02 does not define.
    /// </summary>
    /// <param name="type">The packet type.</param>
    /// <returns>The name.</returns>
    public static string NameOf(byte type) => type switch
    {
        Extended => "EXTENDED",
        < 21 when RequestNames[type] is { } name => name,
        _ => $"type {type}",
    };
}
