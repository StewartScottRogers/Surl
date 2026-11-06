namespace Surl.Extended.Protocol.Mqtt5;

/// <summary>
/// One step of an enhanced authentication (section 4.12).
/// </summary>
/// <param name="Outcome">Where the step left the authentication.</param>
/// <param name="Data">The Authentication Data the broker sends: the challenge, or with acceptance the method's last message.</param>
/// <param name="Account">The account an accepted login matched; <see langword="null"/> when it was refused or accepted unchecked.</param>
internal sealed record Mqtt5AuthenticationStep(Mqtt5AuthenticationOutcome Outcome, byte[] Data, string? Account);
