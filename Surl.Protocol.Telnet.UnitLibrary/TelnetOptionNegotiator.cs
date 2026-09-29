using System.Buffers;
using static Surl.Protocol.Telnet.TelnetCodes;

namespace Surl.Protocol.Telnet;

/// <summary>
/// Keeps the state of every TELNET option on both sides and answers each <c>WILL</c>,
/// <c>WONT</c>, <c>DO</c> and <c>DONT</c> the client sends (RFC 854, RFC 855, and RFC 1143's
/// rule that a request for the state an option is already in is not answered, so
/// negotiation never loops).
/// </summary>
/// <remarks>
/// <para>
/// The server performs <c>SUPPRESS-GO-AHEAD</c> and <c>BINARY</c>, offering the first itself.
/// It asks the client to perform <c>TERMINAL-TYPE</c>, <c>X-DISPLAY-LOCATION</c>,
/// <c>NEW-ENVIRON</c> and <c>NAWS</c>, and agrees when the client offers those or
/// <c>SUPPRESS-GO-AHEAD</c> or <c>BINARY</c>. Every other option is refused: <c>DO</c> with
/// <c>WONT</c>, <c>WILL</c> with <c>DONT</c> (RFC 855).
/// </para>
/// <para>
/// The server never asks to turn an option off, so each side of an option is in one of three
/// states: off, on, or asked for and not yet answered.
/// </para>
/// </remarks>
internal sealed class TelnetOptionNegotiator
{
    private static readonly byte[] OptionsTheServerPerforms = [BinaryOption, SuppressGoAheadOption];
    private static readonly byte[] OptionsTheServerOffers = [SuppressGoAheadOption];

    private static readonly byte[] OptionsTheClientMayPerform =
        [BinaryOption, SuppressGoAheadOption, TerminalTypeOption, WindowSizeOption, DisplayLocationOption, NewEnvironmentOption];

    private static readonly byte[] OptionsTheServerAsksFor =
        [TerminalTypeOption, DisplayLocationOption, NewEnvironmentOption, WindowSizeOption];

    private readonly OptionState[] serverOptions = new OptionState[256];
    private readonly OptionState[] clientOptions = new OptionState[256];

    private enum OptionState
    {
        Off,
        On,
        Asked,
    }

    /// <summary>
    /// Writes the server's opening offers and requests: <c>WILL SUPPRESS-GO-AHEAD</c>, then
    /// <c>DO</c> <c>TERMINAL-TYPE</c>, <c>X-DISPLAY-LOCATION</c>, <c>NEW-ENVIRON</c> and <c>NAWS</c>.
    /// </summary>
    /// <param name="outbound">Where the bytes go.</param>
    public void WriteOpening(IBufferWriter<byte> outbound)
    {
        foreach (var option in OptionsTheServerOffers)
        {
            serverOptions[option] = OptionState.Asked;
            WriteNegotiation(outbound, Will, option);
        }

        foreach (var option in OptionsTheServerAsksFor)
        {
            clientOptions[option] = OptionState.Asked;
            WriteNegotiation(outbound, Do, option);
        }
    }

    /// <summary>
    /// Answers one negotiation from the client.
    /// </summary>
    /// <param name="verb"><c>WILL</c>, <c>WONT</c>, <c>DO</c> or <c>DONT</c>.</param>
    /// <param name="option">The option it names.</param>
    /// <param name="outbound">Where the answer, if any, goes.</param>
    /// <returns>What the negotiation did to the client's side of the option.</returns>
    public TelnetClientOptionChange Answer(byte verb, byte option, IBufferWriter<byte> outbound) => verb switch
    {
        Will => AnswerOffer(clientOptions, OptionsTheClientMayPerform, option, Do, Dont, outbound)
            ? TelnetClientOptionChange.TurnedOn
            : TelnetClientOptionChange.None,
        Wont => AnswerRefusal(clientOptions, option, Dont, outbound)
            ? TelnetClientOptionChange.TurnedOff
            : TelnetClientOptionChange.None,
        Do => AnswerOfferIgnoringChange(serverOptions, OptionsTheServerPerforms, option, Will, Wont, outbound),
        _ => AnswerRefusalIgnoringChange(serverOptions, option, Wont, outbound),
    };

    private static TelnetClientOptionChange AnswerOfferIgnoringChange(
        OptionState[] states, byte[] supported, byte option, byte agree, byte refuse, IBufferWriter<byte> outbound)
    {
        AnswerOffer(states, supported, option, agree, refuse, outbound);

        return TelnetClientOptionChange.None;
    }

    private static TelnetClientOptionChange AnswerRefusalIgnoringChange(
        OptionState[] states, byte option, byte confirm, IBufferWriter<byte> outbound)
    {
        AnswerRefusal(states, option, confirm, outbound);

        return TelnetClientOptionChange.None;
    }

    private static bool AnswerOffer(
        OptionState[] states, byte[] supported, byte option, byte agree, byte refuse, IBufferWriter<byte> outbound)
    {
        var state = states[option];
        if (state == OptionState.On)
        {
            return false;
        }

        if (state == OptionState.Off && !supported.Contains(option))
        {
            WriteNegotiation(outbound, refuse, option);

            return false;
        }

        if (state == OptionState.Off)
        {
            WriteNegotiation(outbound, agree, option);
        }

        states[option] = OptionState.On;

        return true;
    }

    private static bool AnswerRefusal(OptionState[] states, byte option, byte confirm, IBufferWriter<byte> outbound)
    {
        var state = states[option];
        states[option] = OptionState.Off;
        if (state == OptionState.On)
        {
            WriteNegotiation(outbound, confirm, option);
        }

        return state != OptionState.Off;
    }

    private static void WriteNegotiation(IBufferWriter<byte> outbound, byte verb, byte option) =>
        outbound.Write([InterpretAsCommand, verb, option]);
}
