using System.Collections.Concurrent;
using System.Text;

namespace Surl.Conformance;

/// <summary>
/// Collects the stdout and stderr a client had written when its case was stopped at the
/// timeout, and writes the file (<c>upstream-test-case-timeout-output.&lt;platform&gt;.txt</c>)
/// CI uploads beside the results file, so the cause of a timeout can be read (BL-1334).
/// </summary>
internal sealed class UpstreamTestCaseTimeoutOutput
{
    private readonly ConcurrentDictionary<int, (byte[] StandardOutput, string StandardError, string ExchangeNotes)> outputs = new();

    /// <summary>The name of the timeout output file for a pin's platform.</summary>
    public static string FileNameFor(string platform) => $"upstream-test-case-timeout-output.{platform}.txt";

    /// <summary>Records what one timed-out case's client had written.</summary>
    public void Record(int number, byte[] standardOutput, string standardError, string exchangeNotes) =>
        outputs[number] = (standardOutput, standardError, exchangeNotes);

    /// <summary>
    /// One block per recorded case in number order: a header line, the stdout so far, the
    /// stderr so far (which holds curl's trace for a libtest case) and the server halves'
    /// exchange log notes; empty when no case timed out.
    /// </summary>
    public string Format()
    {
        var text = new StringBuilder();
        foreach (var (number, output) in outputs.OrderBy(pair => pair.Key))
        {
            text.Append($"test{number} stdout so far ({output.StandardOutput.Length} bytes):\n")
                .Append(Encoding.Latin1.GetString(output.StandardOutput).ReplaceLineEndings("\n").TrimEnd('\n'))
                .Append($"\ntest{number} stderr so far:\n")
                .Append(output.StandardError.ReplaceLineEndings("\n").TrimEnd('\n'))
                .Append($"\ntest{number} exchange log notes:\n")
                .Append(output.ExchangeNotes.ReplaceLineEndings("\n").TrimEnd('\n'))
                .Append("\n\n");
        }

        return text.ToString();
    }
}
