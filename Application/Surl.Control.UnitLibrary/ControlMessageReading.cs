namespace Surl.Control;

/// <summary>
/// Checks <c>POST /v1/messages</c>'s JSON body and turns it into a <see cref="ControlMessage"/>: exactly one of
/// <c>topic</c> and <c>queue</c>, exactly one of <c>payload_text</c> (sent as UTF-8) and <c>payload_base64</c>, and
/// <c>retain</c> only with a topic.
/// </summary>
internal static class ControlMessageReading
{
    /// <summary>The message the body describes.</summary>
    /// <param name="body">The body.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ControlErrorException"><c>400</c> naming what is wrong.</exception>
    public static ControlMessage Read(ControlMessageRequestJson body)
    {
        if (string.IsNullOrEmpty(body.Topic) == string.IsNullOrEmpty(body.Queue))
        {
            throw BadRequest("give exactly one of topic and queue, not empty");
        }

        if ((body.PayloadText is null) == (body.PayloadBase64 is null))
        {
            throw BadRequest("give exactly one of payload_text and payload_base64");
        }

        var isTopic = !string.IsNullOrEmpty(body.Topic);
        if (body.Retain && !isTopic)
        {
            throw BadRequest("retain applies only to a topic");
        }

        return isTopic
            ? new ControlMessage(body.Topic, null, Payload(body), body.Retain)
            : new ControlMessage(null, body.Queue, Payload(body), Retain: false);
    }

    private static byte[] Payload(ControlMessageRequestJson body)
    {
        if (body.PayloadText is not null)
        {
            return System.Text.Encoding.UTF8.GetBytes(body.PayloadText);
        }

        try
        {
            return Convert.FromBase64String(body.PayloadBase64!);
        }
        catch (FormatException)
        {
            throw BadRequest("payload_base64 is not base64");
        }
    }

    private static ControlErrorException BadRequest(string message) => new(ControlError.BadRequest(message));
}
