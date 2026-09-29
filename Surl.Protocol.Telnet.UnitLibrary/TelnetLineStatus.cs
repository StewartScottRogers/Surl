namespace Surl.Protocol.Telnet;

/// <summary>
/// What a data byte did to the line being collected (<see cref="TelnetLineAssembler.Take"/>).
/// </summary>
internal enum TelnetLineStatus
{
    /// <summary>The line goes on.</summary>
    PartWay,

    /// <summary>The byte ended the line, now in <see cref="TelnetLineAssembler.CompletedLine"/>.</summary>
    Completed,

    /// <summary>The line is longer than the line limit.</summary>
    TooLong,
}
