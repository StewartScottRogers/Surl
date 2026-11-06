namespace Surl.Cryptography.Srp;

/// <summary>
/// A GnuTLS <c>tpasswd</c> or <c>tpasswd.conf</c> file that cannot be read: a malformed line, a bad
/// SRP base64 number, or a group index the groups file lacks. The host answers it with
/// <c>CouldNotReadFile</c> (37), naming the file and <see cref="LineNumber" /> (ADR-0122 decision 6).
/// </summary>
public sealed class SrpFileFormatException : FormatException
{
    /// <summary>Creates the exception for one line.</summary>
    /// <param name="lineNumber">The one-based number of the line at fault.</param>
    /// <param name="reason">What is wrong with it, in a few words.</param>
    public SrpFileFormatException(int lineNumber, string reason)
        : base($"line {lineNumber}: {reason}")
    {
        LineNumber = lineNumber;
        Reason = reason;
    }

    /// <summary>The one-based number of the line at fault.</summary>
    public int LineNumber { get; }

    /// <summary>What is wrong with the line.</summary>
    public string Reason { get; }
}
