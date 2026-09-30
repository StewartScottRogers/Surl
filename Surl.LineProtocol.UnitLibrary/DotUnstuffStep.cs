namespace Surl.LineProtocol;

/// <summary>
/// What one <see cref="DotUnstuffer.Unstuff"/> call did.
/// </summary>
/// <param name="Consumed">How many input bytes were consumed.</param>
/// <param name="Written">How many output bytes were written.</param>
/// <param name="Ended">Whether the terminator was reached; the input past it was not consumed.</param>
internal readonly record struct DotUnstuffStep(int Consumed, int Written, bool Ended);
