namespace Surl.Cli;

/// <summary>Reads an argument of one kind; returns the refusal reason, or null.</summary>
/// <typeparam name="T">The value the argument is read as.</typeparam>
/// <param name="argument">The argument as given.</param>
/// <param name="value">The value, when the argument is accepted.</param>
/// <returns>The refusal reason after <c>option &lt;name&gt;: </c>, or <see langword="null"/>.</returns>
internal delegate string? ReadArgument<T>(string argument, out T value);

/// <summary>
/// One <see cref="OptionArgumentReader"/> read method paired with the argument type it reads
/// (ADR-0046 decision 6), so an option's argument type cannot say it takes what its reader refuses.
/// </summary>
/// <typeparam name="T">The value the argument is read as.</typeparam>
/// <param name="Read">The read method.</param>
/// <param name="Type">The argument type and allowed values the read method accepts.</param>
internal sealed record OptionArgumentReading<T>(ReadArgument<T> Read, OptionArgumentType Type);
