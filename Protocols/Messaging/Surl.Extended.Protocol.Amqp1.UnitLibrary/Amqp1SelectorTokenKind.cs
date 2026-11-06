namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// The kinds of token a JMS selector is made of (Jakarta Messaging 3.1 section 3.8.1.1). The six
/// comparison operators run from <see cref="Equal"/> to <see cref="GreaterOrEqual"/> in order.
/// </summary>
internal enum Amqp1SelectorTokenKind
{
    /// <summary>The end of the selector.</summary>
    End,

    /// <summary>A literal: a string, an exact or approximate number, <c>TRUE</c> or <c>FALSE</c>.</summary>
    Literal,

    /// <summary>An identifier.</summary>
    Identifier,

    /// <summary><c>NOT</c>.</summary>
    Not,

    /// <summary><c>AND</c>.</summary>
    And,

    /// <summary><c>OR</c>.</summary>
    Or,

    /// <summary><c>BETWEEN</c>.</summary>
    Between,

    /// <summary><c>LIKE</c>.</summary>
    Like,

    /// <summary><c>IN</c>.</summary>
    In,

    /// <summary><c>IS</c>.</summary>
    Is,

    /// <summary><c>NULL</c>.</summary>
    Null,

    /// <summary><c>ESCAPE</c>.</summary>
    Escape,

    /// <summary><c>(</c>.</summary>
    LeftParenthesis,

    /// <summary><c>)</c>.</summary>
    RightParenthesis,

    /// <summary><c>,</c>.</summary>
    Comma,

    /// <summary><c>=</c>.</summary>
    Equal,

    /// <summary><c>&lt;&gt;</c>.</summary>
    NotEqual,

    /// <summary><c>&lt;</c>.</summary>
    Less,

    /// <summary><c>&lt;=</c>.</summary>
    LessOrEqual,

    /// <summary><c>&gt;</c>.</summary>
    Greater,

    /// <summary><c>&gt;=</c>.</summary>
    GreaterOrEqual,

    /// <summary><c>+</c>.</summary>
    Plus,

    /// <summary><c>-</c>.</summary>
    Minus,

    /// <summary><c>*</c>.</summary>
    Multiply,

    /// <summary><c>/</c>.</summary>
    Divide,
}
