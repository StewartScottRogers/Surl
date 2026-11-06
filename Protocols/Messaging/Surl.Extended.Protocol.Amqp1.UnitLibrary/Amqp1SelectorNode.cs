namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// One node of a parsed JMS selector. Evaluating it yields <see langword="null"/> (SQL's NULL, unknown),
/// a <c>bool</c>, a <c>long</c>, a <c>double</c> or a <c>string</c>; it never throws.
/// </summary>
internal abstract class Amqp1SelectorNode
{
    /// <summary>The node's value for a message.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The value; <see langword="null"/> when unknown.</returns>
    public abstract object? Evaluate(IAmqp1SelectorMessage message);
}

/// <summary>A literal: a string, a <c>long</c>, a <c>double</c> or a <c>bool</c>.</summary>
/// <param name="value">The literal's value.</param>
internal sealed class Amqp1SelectorLiteralNode(object value) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) => value;
}

/// <summary>An identifier: a JMS header field or an application property (<see cref="Amqp1SelectorIdentifiers"/>).</summary>
/// <param name="name">The identifier, matched exactly.</param>
internal sealed class Amqp1SelectorIdentifierNode(string name) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) => Amqp1SelectorIdentifiers.Read(name, message);
}

/// <summary><c>NOT</c>: TRUE and FALSE swap, NULL stays NULL.</summary>
/// <param name="operand">The negated expression.</param>
internal sealed class Amqp1SelectorNotNode(Amqp1SelectorNode operand) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) =>
        Amqp1SelectorValues.AsLogic(operand.Evaluate(message)) is bool value ? !value : null;
}

/// <summary><c>AND</c>: FALSE when either side is FALSE, TRUE when both are TRUE, otherwise NULL.</summary>
/// <param name="left">The left operand.</param>
/// <param name="right">The right operand.</param>
internal sealed class Amqp1SelectorAndNode(Amqp1SelectorNode left, Amqp1SelectorNode right) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message)
    {
        var leftValue = Amqp1SelectorValues.AsLogic(left.Evaluate(message));
        if (leftValue == false)
        {
            return false;
        }

        var rightValue = Amqp1SelectorValues.AsLogic(right.Evaluate(message));
        return rightValue == false ? false : leftValue & rightValue;
    }
}

/// <summary><c>OR</c>: TRUE when either side is TRUE, FALSE when both are FALSE, otherwise NULL.</summary>
/// <param name="left">The left operand.</param>
/// <param name="right">The right operand.</param>
internal sealed class Amqp1SelectorOrNode(Amqp1SelectorNode left, Amqp1SelectorNode right) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message)
    {
        var leftValue = Amqp1SelectorValues.AsLogic(left.Evaluate(message));
        if (leftValue == true)
        {
            return true;
        }

        var rightValue = Amqp1SelectorValues.AsLogic(right.Evaluate(message));
        return rightValue == true ? true : leftValue | rightValue;
    }
}

/// <summary>A comparison: <c>=</c>, <c>&lt;&gt;</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c> or <c>&gt;=</c>.</summary>
/// <param name="comparison">The comparison operator's token kind.</param>
/// <param name="left">The left operand.</param>
/// <param name="right">The right operand.</param>
internal sealed class Amqp1SelectorComparisonNode(
    Amqp1SelectorTokenKind comparison,
    Amqp1SelectorNode left,
    Amqp1SelectorNode right) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) =>
        Amqp1SelectorValues.Compare(comparison, left.Evaluate(message), right.Evaluate(message));
}

/// <summary>Binary arithmetic: <c>+</c>, <c>-</c>, <c>*</c> or <c>/</c>.</summary>
/// <param name="operation">The operator's token kind.</param>
/// <param name="left">The left operand.</param>
/// <param name="right">The right operand.</param>
internal sealed class Amqp1SelectorArithmeticNode(
    Amqp1SelectorTokenKind operation,
    Amqp1SelectorNode left,
    Amqp1SelectorNode right) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) =>
        Amqp1SelectorValues.Calculate(operation, left.Evaluate(message), right.Evaluate(message));
}

/// <summary>A unary sign: <c>-</c> negates a number, <c>+</c> keeps it; anything else is NULL.</summary>
/// <param name="negates">Whether the sign is <c>-</c>.</param>
/// <param name="operand">The signed expression.</param>
internal sealed class Amqp1SelectorSignNode(bool negates, Amqp1SelectorNode operand) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) =>
        Amqp1SelectorValues.Sign(negates, operand.Evaluate(message));
}

/// <summary><c>identifier IN ('a', 'b')</c>: NULL when the identifier is NULL or not a string.</summary>
/// <param name="identifier">The identifier.</param>
/// <param name="values">The string literals listed.</param>
internal sealed class Amqp1SelectorInNode(Amqp1SelectorNode identifier, IReadOnlySet<string> values) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) =>
        identifier.Evaluate(message) is string value ? values.Contains(value) : null;
}

/// <summary><c>identifier LIKE 'pattern'</c>: NULL when the identifier is NULL or not a string.</summary>
/// <param name="identifier">The identifier.</param>
/// <param name="pattern">The compiled pattern.</param>
internal sealed class Amqp1SelectorLikeNode(Amqp1SelectorNode identifier, Amqp1SelectorLikePattern pattern) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) =>
        identifier.Evaluate(message) is string value ? pattern.Matches(value) : null;
}

/// <summary><c>identifier IS NULL</c>: TRUE when the identifier is NULL, never NULL itself.</summary>
/// <param name="identifier">The identifier.</param>
internal sealed class Amqp1SelectorIsNullNode(Amqp1SelectorNode identifier) : Amqp1SelectorNode
{
    /// <inheritdoc/>
    public override object? Evaluate(IAmqp1SelectorMessage message) => identifier.Evaluate(message) is null;
}
