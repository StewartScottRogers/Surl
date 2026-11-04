namespace Surl.Protocol.Mqtt;

/// <summary>
/// How <see cref="MqttConnectJudge"/> judged a <c>CONNECT</c>, with the login it carried.
/// </summary>
/// <param name="Verdict">
/// <see cref="MqttConnectVerdict.LoginToCheck"/> when the packet is well formed, otherwise the
/// refusal it earned.
/// </param>
/// <param name="UserName">The user name, or <see langword="null"/> when the connect flags announce none.</param>
/// <param name="Password">The password bytes as sent, or <see langword="null"/> when the connect flags announce none.</param>
internal sealed record MqttConnectJudgement(
    MqttConnectVerdict Verdict,
    string? UserName = null,
    ReadOnlyMemory<byte>? Password = null);
