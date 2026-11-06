namespace Surl.Extended.Protocol.Amqp1;

/// <summary>Why a JMS selector was refused (ADR-0146 decision 2.2).</summary>
public enum Amqp1SelectorRefusal
{
    /// <summary>The selector was not refused.</summary>
    None,

    /// <summary>The selector is longer than <see cref="Amqp1Selector.MaxLength"/> characters.</summary>
    TooLong,

    /// <summary>The selector nests deeper than <see cref="Amqp1Selector.MaxDepth"/> levels.</summary>
    TooDeep,

    /// <summary>The selector does not parse as Jakarta Messaging 3.1 section 3.8.1 has it.</summary>
    Malformed,
}
