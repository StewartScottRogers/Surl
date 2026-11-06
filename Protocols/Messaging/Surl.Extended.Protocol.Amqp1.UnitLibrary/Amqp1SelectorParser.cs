namespace Surl.Extended.Protocol.Amqp1;

/// <summary>
/// Parses a JMS selector's tokens by recursive descent in Jakarta Messaging 3.1 section 3.8.1's
/// precedence, lowest first: <c>OR</c>; <c>AND</c>; <c>NOT</c>; a comparison, <c>[NOT] BETWEEN</c>,
/// <c>[NOT] IN</c>, <c>[NOT] LIKE ... [ESCAPE ...]</c> or <c>IS [NOT] NULL</c>; <c>+</c> and <c>-</c>;
/// <c>*</c> and <c>/</c>; a unary sign; and a literal, identifier or parenthesised expression.
/// <c>IN</c>, <c>LIKE</c> and <c>IS NULL</c> take an identifier on their left, and <c>IN</c>, <c>LIKE</c>
/// and <c>ESCAPE</c> string literals on their right. Each parenthesised group, <c>NOT</c> and unary sign
/// opens a level; past <see cref="Amqp1Selector.MaxDepth"/> levels the selector is refused.
/// </summary>
internal sealed class Amqp1SelectorParser
{
    private readonly List<Amqp1SelectorToken> tokens;
    private int index;
    private int depth;

    private Amqp1SelectorParser(List<Amqp1SelectorToken> tokens) => this.tokens = tokens;

    private Amqp1SelectorToken Current => tokens[index];

    /// <summary>Parses a selector.</summary>
    /// <param name="text">The selector.</param>
    /// <returns>Its root node; <see langword="null"/> when the selector is empty or white space.</returns>
    /// <exception cref="Amqp1SelectorSyntaxException">The selector is malformed or nested too deep.</exception>
    public static Amqp1SelectorNode? Parse(string text)
    {
        var tokens = Amqp1SelectorTokenizer.Tokenize(text);
        if (tokens[0].Kind == Amqp1SelectorTokenKind.End)
        {
            return null;
        }

        var parser = new Amqp1SelectorParser(tokens);
        var root = parser.ParseOr();
        parser.Expect(Amqp1SelectorTokenKind.End);
        return root;
    }

    private static Amqp1SelectorNode RequireIdentifier(Amqp1SelectorNode node) =>
        node is Amqp1SelectorIdentifierNode ? node : throw new Amqp1SelectorSyntaxException();

    private static Amqp1SelectorNode NegatedWhen(bool negated, Amqp1SelectorNode node) =>
        negated ? new Amqp1SelectorNotNode(node) : node;

    private Amqp1SelectorToken Next() => tokens[index++];

    private bool Accept(Amqp1SelectorTokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        index++;
        return true;
    }

    private void Expect(Amqp1SelectorTokenKind kind)
    {
        if (!Accept(kind))
        {
            throw new Amqp1SelectorSyntaxException();
        }
    }

    private string ExpectString() =>
        Next() is { Kind: Amqp1SelectorTokenKind.Literal, Value: string value }
            ? value
            : throw new Amqp1SelectorSyntaxException();

    private Amqp1SelectorNode Nested(Func<Amqp1SelectorNode> parse)
    {
        if (++depth > Amqp1Selector.MaxDepth)
        {
            throw new Amqp1SelectorSyntaxException(Amqp1SelectorRefusal.TooDeep);
        }

        var node = parse();
        depth--;
        return node;
    }

    private Amqp1SelectorNode ParseOr()
    {
        var left = ParseAnd();
        while (Accept(Amqp1SelectorTokenKind.Or))
        {
            left = new Amqp1SelectorOrNode(left, ParseAnd());
        }

        return left;
    }

    private Amqp1SelectorNode ParseAnd()
    {
        var left = ParseNot();
        while (Accept(Amqp1SelectorTokenKind.And))
        {
            left = new Amqp1SelectorAndNode(left, ParseNot());
        }

        return left;
    }

    private Amqp1SelectorNode ParseNot() =>
        Accept(Amqp1SelectorTokenKind.Not)
            ? Nested(() => new Amqp1SelectorNotNode(ParseNot()))
            : ParsePredicate();

    private Amqp1SelectorNode ParsePredicate()
    {
        var left = ParseAdditive();
        if (Current.Kind is >= Amqp1SelectorTokenKind.Equal and <= Amqp1SelectorTokenKind.GreaterOrEqual)
        {
            return new Amqp1SelectorComparisonNode(Next().Kind, left, ParseAdditive());
        }

        if (Accept(Amqp1SelectorTokenKind.Is))
        {
            return ParseIsNull(left);
        }

        var negated = Accept(Amqp1SelectorTokenKind.Not);
        var predicate = ParseRangeOrMembership(left);
        if (predicate is null)
        {
            return negated ? throw new Amqp1SelectorSyntaxException() : left;
        }

        return NegatedWhen(negated, predicate);
    }

    private Amqp1SelectorNode? ParseRangeOrMembership(Amqp1SelectorNode left)
    {
        if (Accept(Amqp1SelectorTokenKind.Between))
        {
            return ParseBetween(left);
        }

        if (Accept(Amqp1SelectorTokenKind.In))
        {
            return ParseIn(RequireIdentifier(left));
        }

        return Accept(Amqp1SelectorTokenKind.Like) ? ParseLike(RequireIdentifier(left)) : null;
    }

    private Amqp1SelectorNode ParseBetween(Amqp1SelectorNode value)
    {
        var low = ParseAdditive();
        Expect(Amqp1SelectorTokenKind.And);
        var high = ParseAdditive();
        return new Amqp1SelectorAndNode(
            new Amqp1SelectorComparisonNode(Amqp1SelectorTokenKind.GreaterOrEqual, value, low),
            new Amqp1SelectorComparisonNode(Amqp1SelectorTokenKind.LessOrEqual, value, high));
    }

    private Amqp1SelectorNode ParseIn(Amqp1SelectorNode identifier)
    {
        Expect(Amqp1SelectorTokenKind.LeftParenthesis);
        var values = new HashSet<string>(StringComparer.Ordinal);
        do
        {
            values.Add(ExpectString());
        }
        while (Accept(Amqp1SelectorTokenKind.Comma));

        Expect(Amqp1SelectorTokenKind.RightParenthesis);
        return new Amqp1SelectorInNode(identifier, values);
    }

    private Amqp1SelectorNode ParseLike(Amqp1SelectorNode identifier)
    {
        var pattern = ExpectString();
        char? escape = null;
        if (Accept(Amqp1SelectorTokenKind.Escape))
        {
            var escapeText = ExpectString();
            escape = escapeText.Length == 1 ? escapeText[0] : throw new Amqp1SelectorSyntaxException();
        }

        return new Amqp1SelectorLikeNode(identifier, Amqp1SelectorLikePattern.Compile(pattern, escape));
    }

    private Amqp1SelectorNode ParseIsNull(Amqp1SelectorNode left)
    {
        var identifier = RequireIdentifier(left);
        var negated = Accept(Amqp1SelectorTokenKind.Not);
        Expect(Amqp1SelectorTokenKind.Null);
        return NegatedWhen(negated, new Amqp1SelectorIsNullNode(identifier));
    }

    private Amqp1SelectorNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Current.Kind is Amqp1SelectorTokenKind.Plus or Amqp1SelectorTokenKind.Minus)
        {
            left = new Amqp1SelectorArithmeticNode(Next().Kind, left, ParseMultiplicative());
        }

        return left;
    }

    private Amqp1SelectorNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Current.Kind is Amqp1SelectorTokenKind.Multiply or Amqp1SelectorTokenKind.Divide)
        {
            left = new Amqp1SelectorArithmeticNode(Next().Kind, left, ParseUnary());
        }

        return left;
    }

    private Amqp1SelectorNode ParseUnary()
    {
        if (Current.Kind is not (Amqp1SelectorTokenKind.Plus or Amqp1SelectorTokenKind.Minus))
        {
            return ParsePrimary();
        }

        var negates = Next().Kind == Amqp1SelectorTokenKind.Minus;
        return Nested(() => new Amqp1SelectorSignNode(negates, ParseUnary()));
    }

    private Amqp1SelectorNode ParsePrimary()
    {
        var token = Next();
        return token.Kind switch
        {
            Amqp1SelectorTokenKind.Literal => new Amqp1SelectorLiteralNode(token.Value!),
            Amqp1SelectorTokenKind.Identifier => new Amqp1SelectorIdentifierNode((string)token.Value!),
            Amqp1SelectorTokenKind.LeftParenthesis => Nested(ParseParenthesised),
            _ => throw new Amqp1SelectorSyntaxException(),
        };
    }

    private Amqp1SelectorNode ParseParenthesised()
    {
        var inner = ParseOr();
        Expect(Amqp1SelectorTokenKind.RightParenthesis);
        return inner;
    }
}
