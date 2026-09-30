using System.Text;

namespace Surl.Output;

/// <summary>
/// A writer that writes a string one character at a time and yields between them, so
/// two unsynchronised writers would mix their characters.
/// </summary>
internal sealed class CharByCharWriter : TextWriter
{
    private readonly StringBuilder text = new();
    private readonly Lock textLock = new();

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        lock (textLock)
        {
            text.Append(value);
        }

        Thread.Yield();
    }

    public override void Write(string? value)
    {
        foreach (var character in value ?? string.Empty)
        {
            Write(character);
        }
    }

    public override string ToString()
    {
        lock (textLock)
        {
            return text.ToString();
        }
    }
}
