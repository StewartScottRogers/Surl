namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// The STOMP versions the broker speaks (ADR-0118 decision 1), in ascending order so the highest
/// common one compares greatest.
/// </summary>
public enum StompVersion
{
    /// <summary>STOMP 1.0: no header escapes, no heart-beating, no NACK.</summary>
    V1_0,

    /// <summary>STOMP 1.1: the <c>\n</c>, <c>\c</c> and <c>\\</c> header escapes, heart-beating and NACK.</summary>
    V1_1,

    /// <summary>STOMP 1.2: adds the <c>\r</c> header escape and the MESSAGE <c>ack</c> header.</summary>
    V1_2,
}
