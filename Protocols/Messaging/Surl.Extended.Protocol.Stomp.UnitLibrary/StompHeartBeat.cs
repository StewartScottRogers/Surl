using System.Globalization;

namespace Surl.Extended.Protocol.Stomp;

/// <summary>
/// A <c>heart-beat</c> header's two intervals in milliseconds (the specification's
/// "Heart-beating"; ADR-0118 decision 7).
/// </summary>
/// <param name="CanSendEvery">The first number: how often its sender can send a heart-beat; 0 for never.</param>
/// <param name="WantsEvery">The second number: how often its sender wants one; 0 for never.</param>
internal readonly record struct StompHeartBeat(long CanSendEvery, long WantsEvery)
{
    /// <summary>
    /// Reads a <c>heart-beat</c> header: absent means <c>0,0</c>; anything but two non-negative
    /// decimal integers separated by a comma is false.
    /// </summary>
    /// <param name="value">The header's value, or <see langword="null"/>.</param>
    /// <param name="heartBeat">The intervals, or <c>0,0</c> when false is returned.</param>
    public static bool TryParse(string? value, out StompHeartBeat heartBeat)
    {
        heartBeat = default;
        if (value is null)
        {
            return true;
        }

        var parts = value.Split(',');
        if (parts.Length == 2 && TryParseInterval(parts[0], out var canSend) && TryParseInterval(parts[1], out var wants))
        {
            heartBeat = new StompHeartBeat(canSend, wants);
            return true;
        }

        return false;
    }

    /// <summary>The header's value, <c>x,y</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{CanSendEvery},{WantsEvery}");

    private static bool TryParseInterval(string text, out long milliseconds) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds);
}
