namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// The JMS selector recogniser (ADR-0146 decision 2.2; Jakarta Messaging 3.1 section 3.8.1): each
/// construct, three-valued logic, and the refusals.
/// </summary>
[TestClass]
public sealed class Amqp1SelectorTests
{
    private static readonly UnitTestAmqp1SelectorMessage Message = new()
    {
        ApplicationProperties =
        {
            ["s"] = "abc",
            ["quote"] = "it's",
            ["n"] = 5L,
            ["d"] = 2.5,
            ["b"] = true,
            ["f"] = false,
            ["pattern"] = "a_%c",
        },
    };

    [TestMethod]
    [DataRow("s = 'abc'", "TRUE", DisplayName = "string literal")]
    [DataRow("quote = 'it''s'", "TRUE", DisplayName = "'' in a string")]
    [DataRow("s = ''", "FALSE", DisplayName = "empty string literal")]
    [DataRow("n = 5", "TRUE", DisplayName = "exact numeric literal")]
    [DataRow("n = 5L", "TRUE", DisplayName = "exact numeric literal with L")]
    [DataRow("n = 5l", "TRUE", DisplayName = "exact numeric literal with l")]
    [DataRow("n = 0x5", "TRUE", DisplayName = "hexadecimal literal")]
    [DataRow("n = 0X5L", "TRUE", DisplayName = "hexadecimal literal with L")]
    [DataRow("n = 05", "TRUE", DisplayName = "octal literal")]
    [DataRow("n = 0", "FALSE", DisplayName = "zero")]
    [DataRow("d = 2.5", "TRUE", DisplayName = "approximate literal")]
    [DataRow("d = .25E1", "TRUE", DisplayName = "approximate literal with leading point and exponent")]
    [DataRow("d = 25e-1", "TRUE", DisplayName = "exponent with sign")]
    [DataRow("d = 0.025E+2", "TRUE", DisplayName = "exponent with plus")]
    [DataRow("n = 5.", "TRUE", DisplayName = "trailing point")]
    [DataRow("d = 2.5F", "TRUE", DisplayName = "F suffix")]
    [DataRow("n = 5d", "TRUE", DisplayName = "d suffix on an integer")]
    [DataRow("b = TRUE", "TRUE", DisplayName = "TRUE literal")]
    [DataRow("f = false", "TRUE", DisplayName = "false literal in lower case")]
    [DataRow("b", "TRUE", DisplayName = "boolean identifier alone")]
    [DataRow("TrUe", "TRUE", DisplayName = "boolean literal alone, mixed case")]
    [DataRow("n", "NULL", DisplayName = "a number alone is not a boolean")]
    public void Evaluate_Literals_HaveTheirValues(string selector, string expected) =>
        AssertEvaluates(selector, expected);

    [TestMethod]
    [DataRow("NOT b", "FALSE", DisplayName = "NOT")]
    [DataRow("not not b", "TRUE", DisplayName = "NOT NOT, lower case")]
    [DataRow("b AND f", "FALSE", DisplayName = "AND")]
    [DataRow("b and b", "TRUE", DisplayName = "and")]
    [DataRow("f OR b", "TRUE", DisplayName = "OR")]
    [DataRow("f Or f", "FALSE", DisplayName = "Or")]
    [DataRow("b OR b AND f", "TRUE", DisplayName = "AND binds tighter than OR")]
    [DataRow("(b OR b) AND f", "FALSE", DisplayName = "parentheses")]
    [DataRow("NOT f AND f", "FALSE", DisplayName = "NOT binds tighter than AND")]
    [DataRow("NOT (f AND f)", "TRUE", DisplayName = "NOT of a group")]
    [DataRow("NOT n = 4", "TRUE", DisplayName = "comparison binds tighter than NOT")]
    public void Evaluate_Logic_FollowsPrecedence(string selector, string expected) =>
        AssertEvaluates(selector, expected);

    [TestMethod]
    [DataRow("n = 5", "TRUE")]
    [DataRow("n <> 5", "FALSE")]
    [DataRow("n < 6", "TRUE")]
    [DataRow("n < 5", "FALSE")]
    [DataRow("n <= 5", "TRUE")]
    [DataRow("n <= 4", "FALSE")]
    [DataRow("n > 4", "TRUE")]
    [DataRow("n > 5", "FALSE")]
    [DataRow("n >= 5", "TRUE")]
    [DataRow("n >= 6", "FALSE")]
    [DataRow("d > n", "FALSE", DisplayName = "approximate against exact")]
    [DataRow("d < n", "TRUE", DisplayName = "approximate against exact, less")]
    [DataRow("n = 5.0", "TRUE", DisplayName = "exact equals approximate")]
    [DataRow("s = 'abc'", "TRUE", DisplayName = "string =")]
    [DataRow("s <> 'abd'", "TRUE", DisplayName = "string <>")]
    [DataRow("s <> 'abc'", "FALSE", DisplayName = "string <> itself")]
    [DataRow("s < 'abd'", "FALSE", DisplayName = "strings only compare with = and <>")]
    [DataRow("b = TRUE", "TRUE", DisplayName = "boolean =")]
    [DataRow("b <> f", "TRUE", DisplayName = "boolean <>")]
    [DataRow("b > f", "FALSE", DisplayName = "booleans only compare with = and <>")]
    [DataRow("s = 5", "FALSE", DisplayName = "unlike types are FALSE")]
    [DataRow("s <> 5", "FALSE", DisplayName = "unlike types are FALSE for <> too")]
    [DataRow("b = 'TRUE'", "FALSE", DisplayName = "boolean against string")]
    [DataRow("0.0 / 0.0 = 0.0 / 0.0", "FALSE", DisplayName = "NaN equals nothing")]
    [DataRow("0.0 / 0.0 <> 1", "TRUE", DisplayName = "NaN differs from everything")]
    [DataRow("1 < 0.0 / 0.0", "FALSE", DisplayName = "NaN on the right")]
    public void Evaluate_Comparisons_CompareLikeTypes(string selector, string expected) =>
        AssertEvaluates(selector, expected);

    [TestMethod]
    [DataRow("n + 1 = 6", "TRUE")]
    [DataRow("n - 1 = 4", "TRUE")]
    [DataRow("n * 2 = 10", "TRUE")]
    [DataRow("n / 2 = 2", "TRUE", DisplayName = "exact division truncates")]
    [DataRow("n / -1 = -5", "TRUE", DisplayName = "dividing by minus one")]
    [DataRow("-9223372036854775807 - 1 = (-9223372036854775807 - 1) / -1", "TRUE", DisplayName = "long minimum over minus one wraps")]
    [DataRow("n / 0 = 1", "NULL", DisplayName = "exact division by zero is NULL")]
    [DataRow("d + 1 = 3.5", "TRUE")]
    [DataRow("d - n = -2.5", "TRUE")]
    [DataRow("n * d = 12.5", "TRUE")]
    [DataRow("n / 2.0 = d", "TRUE", DisplayName = "approximate division")]
    [DataRow("1 / 0.0 > 1", "TRUE", DisplayName = "approximate division by zero is infinity")]
    [DataRow("-n = -5", "TRUE", DisplayName = "unary minus")]
    [DataRow("+n = 5", "TRUE", DisplayName = "unary plus")]
    [DataRow("- -n = 5", "TRUE", DisplayName = "unary minus twice")]
    [DataRow("-d = -2.5", "TRUE", DisplayName = "unary minus on approximate")]
    [DataRow("+d = 2.5", "TRUE", DisplayName = "unary plus on approximate")]
    [DataRow("-s = 1", "NULL", DisplayName = "unary minus on a string")]
    [DataRow("2 + 3 * 4 = 14", "TRUE", DisplayName = "* binds tighter than +")]
    [DataRow("(2 + 3) * 4 = 20", "TRUE", DisplayName = "parentheses in arithmetic")]
    [DataRow("10 - 4 - 3 = 3", "TRUE", DisplayName = "- is left associative")]
    [DataRow("s + 1 = 1", "NULL", DisplayName = "arithmetic on a string is NULL")]
    [DataRow("1 + s = 1", "NULL", DisplayName = "arithmetic with a string on the right is NULL")]
    [DataRow("missing + 1 = 1", "NULL", DisplayName = "arithmetic on NULL is NULL")]
    public void Evaluate_Arithmetic_Calculates(string selector, string expected) =>
        AssertEvaluates(selector, expected);

    [TestMethod]
    [DataRow("n BETWEEN 1 AND 5", "TRUE")]
    [DataRow("n between 6 and 9", "FALSE")]
    [DataRow("n NOT BETWEEN 6 AND 9", "TRUE")]
    [DataRow("n not between 1 and 5", "FALSE")]
    [DataRow("d BETWEEN n - 3 AND n", "TRUE", DisplayName = "arithmetic bounds")]
    [DataRow("missing BETWEEN 1 AND 5", "NULL")]
    [DataRow("missing NOT BETWEEN 1 AND 5", "NULL")]
    [DataRow("s IN ('x', 'abc')", "TRUE")]
    [DataRow("s in ('x')", "FALSE")]
    [DataRow("s NOT IN ('x', 'y')", "TRUE")]
    [DataRow("s Not In ('abc')", "FALSE")]
    [DataRow("missing IN ('x')", "NULL")]
    [DataRow("missing NOT IN ('x')", "NULL")]
    [DataRow("n IN ('5')", "NULL", DisplayName = "IN on a number is NULL")]
    [DataRow("s LIKE 'abc'", "TRUE")]
    [DataRow("s LIKE 'a%'", "TRUE")]
    [DataRow("s LIKE '%c'", "TRUE")]
    [DataRow("s LIKE '%b%'", "TRUE")]
    [DataRow("s LIKE 'a_c'", "TRUE")]
    [DataRow("s LIKE '_'", "FALSE")]
    [DataRow("s LIKE 'ab'", "FALSE")]
    [DataRow("s LIKE 'abcd'", "FALSE")]
    [DataRow("s LIKE '%%c%%'", "TRUE", DisplayName = "runs of %")]
    [DataRow("s LIKE '%x%'", "FALSE")]
    [DataRow("s like 'b%'", "FALSE", DisplayName = "no % to fall back on")]
    [DataRow("s LIKE 'a%c%c'", "FALSE", DisplayName = "backtracking runs out")]
    [DataRow("s NOT LIKE 'x%'", "TRUE")]
    [DataRow("s not like 'a%'", "FALSE")]
    [DataRow("pattern LIKE 'a!_!%c' ESCAPE '!'", "TRUE", DisplayName = "ESCAPE makes _ and % literal")]
    [DataRow("s LIKE 'a!_c' escape '!'", "FALSE", DisplayName = "an escaped _ is not a wildcard")]
    [DataRow("s LIKE '!abc' ESCAPE '!'", "TRUE", DisplayName = "escaping an ordinary character")]
    [DataRow("missing LIKE 'a%'", "NULL")]
    [DataRow("missing NOT LIKE 'a%'", "NULL")]
    [DataRow("n LIKE '5'", "NULL", DisplayName = "LIKE on a number is NULL")]
    [DataRow("missing IS NULL", "TRUE")]
    [DataRow("s IS NULL", "FALSE")]
    [DataRow("missing IS NOT NULL", "FALSE")]
    [DataRow("s is not null", "TRUE")]
    public void Evaluate_Predicates_AndTheirNotForms(string selector, string expected) =>
        AssertEvaluates(selector, expected);

    [TestMethod]
    [DataRow("missing = 1", "NULL", DisplayName = "a missing identifier is NULL")]
    [DataRow("1 = missing", "NULL", DisplayName = "NULL on the right")]
    [DataRow("missing", "NULL", DisplayName = "a missing identifier alone")]
    [DataRow("missing = 1 AND f", "FALSE", DisplayName = "NULL AND FALSE is FALSE")]
    [DataRow("f AND missing = 1", "FALSE", DisplayName = "FALSE AND NULL is FALSE")]
    [DataRow("missing = 1 AND b", "NULL", DisplayName = "NULL AND TRUE is NULL")]
    [DataRow("b AND missing = 1", "NULL", DisplayName = "TRUE AND NULL is NULL")]
    [DataRow("missing = 1 OR b", "TRUE", DisplayName = "NULL OR TRUE is TRUE")]
    [DataRow("b OR missing = 1", "TRUE", DisplayName = "TRUE OR NULL is TRUE")]
    [DataRow("missing = 1 OR f", "NULL", DisplayName = "NULL OR FALSE is NULL")]
    [DataRow("NOT missing = 1", "NULL", DisplayName = "NOT NULL is NULL")]
    [DataRow("n AND b", "NULL", DisplayName = "a number in logic is NULL")]
    public void Evaluate_ThreeValuedLogic(string selector, string expected) =>
        AssertEvaluates(selector, expected);

    [TestMethod]
    public void Selects_OnlyWhenTrue()
    {
        Assert.IsTrue(Parsed("b").Selects(Message));
        Assert.IsFalse(Parsed("f").Selects(Message));
        Assert.IsFalse(Parsed("missing = 1").Selects(Message), "a NULL result does not select");
        Assert.IsFalse(Parsed("NOT missing = 1").Selects(Message), "nor does its NOT");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   \t\r\n")]
    public void Parse_EmptySelector_SelectsEverything(string? selector)
    {
        var parsed = Amqp1Selector.Parse(selector);

        Assert.IsTrue(parsed.IsParsed);
        Assert.AreEqual(Amqp1SelectorRefusal.None, parsed.Refusal);
        Assert.IsTrue(parsed.Selector!.SelectsEverything);
        Assert.AreEqual(selector ?? string.Empty, parsed.Selector.Text);
        Assert.IsTrue(parsed.Selector.Selects(new UnitTestAmqp1SelectorMessage()));
    }

    [TestMethod]
    public void Parse_Selector_KeepsItsText()
    {
        var selector = Parsed("b");

        Assert.AreEqual("b", selector.Text);
        Assert.IsFalse(selector.SelectsEverything);
    }

    [TestMethod]
    public void Parse_SelectorOf4096Characters_IsAccepted()
    {
        var selector = "b" + new string(' ', Amqp1Selector.MaxLength - 1);

        Assert.IsTrue(Amqp1Selector.Parse(selector).IsParsed);
    }

    [TestMethod]
    public void Parse_SelectorOver4096Characters_IsRefusedTooLong()
    {
        var parsed = Amqp1Selector.Parse("b" + new string(' ', Amqp1Selector.MaxLength));

        Assert.IsFalse(parsed.IsParsed);
        Assert.IsNull(parsed.Selector);
        Assert.AreEqual(Amqp1SelectorRefusal.TooLong, parsed.Refusal);
    }

    [TestMethod]
    [DataRow("(", ")", DisplayName = "parentheses")]
    [DataRow("NOT ", "", DisplayName = "NOT")]
    [DataRow("-", "", DisplayName = "unary minus")]
    [DataRow("+", "", DisplayName = "unary plus")]
    public void Parse_Nesting_Is32LevelsAtMost(string open, string close)
    {
        string Nest(int levels, string inner) =>
            string.Concat(Enumerable.Repeat(open, levels)) + inner + string.Concat(Enumerable.Repeat(close, levels));

        var inner = open == "NOT " ? "b" : "n";
        var tail = open == "NOT " ? string.Empty : " = 5";
        var deepest = Amqp1Selector.Parse(Nest(Amqp1Selector.MaxDepth, inner) + tail);
        var tooDeep = Amqp1Selector.Parse(Nest(Amqp1Selector.MaxDepth + 1, inner) + tail);

        Assert.IsTrue(deepest.IsParsed);
        Assert.AreEqual(Amqp1SelectorRefusal.TooDeep, tooDeep.Refusal);
        Assert.IsNull(tooDeep.Selector);
    }

    [TestMethod]
    [DataRow("'abc", DisplayName = "unterminated string")]
    [DataRow("s = 'it''", DisplayName = "unterminated after a quote pair")]
    [DataRow("n = 0x", DisplayName = "hexadecimal without digits")]
    [DataRow("n = 0x10000000000000000", DisplayName = "hexadecimal past 64 bits")]
    [DataRow("n = 9223372036854775808", DisplayName = "decimal past long")]
    [DataRow("n = 08", DisplayName = "octal with 8")]
    [DataRow("n = 01777777777777777777777", DisplayName = "octal past long")]
    [DataRow("n = 1e", DisplayName = "exponent without digits")]
    [DataRow("n = 1.5L", DisplayName = "L on an approximate number")]
    [DataRow("n = 12abc", DisplayName = "number run into a word")]
    [DataRow("n = 1.2.3", DisplayName = "two decimal points")]
    [DataRow("n = .", DisplayName = "a lone point")]
    [DataRow("n != 5", DisplayName = "!= is not an operator")]
    [DataRow("n == 5", DisplayName = "== is not an operator")]
    [DataRow("s = \"abc\"", DisplayName = "double quotes")]
    [DataRow("n = 5 5", DisplayName = "trailing token")]
    [DataRow("n =", DisplayName = "missing operand")]
    [DataRow("n = )", DisplayName = "operand is a parenthesis")]
    [DataRow("(b", DisplayName = "unclosed parenthesis")]
    [DataRow("b)", DisplayName = "stray parenthesis")]
    [DataRow("AND b", DisplayName = "leading AND")]
    [DataRow("b OR", DisplayName = "trailing OR")]
    [DataRow("n NOT 5", DisplayName = "NOT without a predicate")]
    [DataRow("n NOT = 5", DisplayName = "NOT before a comparison")]
    [DataRow("n BETWEEN 1 OR 5", DisplayName = "BETWEEN without AND")]
    [DataRow("1 IN ('a')", DisplayName = "IN on a literal")]
    [DataRow("s IN 'a'", DisplayName = "IN without parentheses")]
    [DataRow("s IN ('a' 'b')", DisplayName = "IN without a comma")]
    [DataRow("s IN ('a',)", DisplayName = "IN with a trailing comma")]
    [DataRow("s IN (1)", DisplayName = "IN with a number")]
    [DataRow("s IN ()", DisplayName = "IN with nothing")]
    [DataRow("'a' LIKE 'a'", DisplayName = "LIKE on a literal")]
    [DataRow("s LIKE n", DisplayName = "LIKE with an identifier pattern")]
    [DataRow("s LIKE 'a' ESCAPE '!!'", DisplayName = "two-character escape")]
    [DataRow("s LIKE 'a' ESCAPE ''", DisplayName = "empty escape")]
    [DataRow("s LIKE 'a!' ESCAPE '!'", DisplayName = "pattern ending with the escape")]
    [DataRow("5 IS NULL", DisplayName = "IS NULL on a literal")]
    [DataRow("s IS 5", DisplayName = "IS without NULL")]
    [DataRow("s IS NOT", DisplayName = "IS NOT without NULL")]
    [DataRow("s = NULL", DisplayName = "NULL is no literal")]
    [DataRow("ESCAPE = 1", DisplayName = "a keyword is no identifier")]
    public void Parse_MalformedSelector_IsRefusedMalformed(string selector)
    {
        var parsed = Amqp1Selector.Parse(selector);

        Assert.IsFalse(parsed.IsParsed);
        Assert.IsNull(parsed.Selector);
        Assert.AreEqual(Amqp1SelectorRefusal.Malformed, parsed.Refusal);
    }

    [TestMethod]
    [DataRow("$x_1 = 1", DisplayName = "$ and _ in identifiers")]
    [DataRow("_x = 1", DisplayName = "identifier starting with _")]
    [DataRow("é = 1", DisplayName = "non-ASCII letter")]
    [DataRow("n=5 AND(n>=1)AND(s<>'x')", DisplayName = "few spaces")]
    [DataRow("n = 0xffffffffffffffff", DisplayName = "sixteen hexadecimal digits")]
    [DataRow("n\t=\r\n5", DisplayName = "tab and line breaks are white space")]
    public void Parse_WellFormedSelector_Parses(string selector)
    {
        Assert.IsTrue(Amqp1Selector.Parse(selector).IsParsed);
    }

    [TestMethod]
    public void Evaluate_IdentifiersAreCaseSensitive()
    {
        AssertEvaluates("S = 'abc'", "NULL");
    }

    [TestMethod]
    public void Evaluate_NullMessage_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Parsed("b").Evaluate(null!));
    }

    internal static Amqp1Selector Parsed(string selector)
    {
        var parsed = Amqp1Selector.Parse(selector);
        Assert.IsTrue(parsed.IsParsed, $"'{selector}' was refused: {parsed.Refusal}");
        return parsed.Selector!;
    }

    internal static void AssertEvaluates(string selector, string expected, IAmqp1SelectorMessage? message = null)
    {
        var value = Parsed(selector).Evaluate(message ?? Message);
        var actual = value switch { true => "TRUE", false => "FALSE", null => "NULL" };
        Assert.AreEqual(expected, actual, selector);
    }
}
