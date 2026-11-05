namespace Surl.Extended.Protocol.Amqp1.UnitTests;

/// <summary>
/// A node directory that records each request and answers with the resolution a test gives; by
/// default every node is found, a dynamic one named <see cref="DynamicAddress"/>.
/// </summary>
internal sealed class UnitTestAmqp1NodeDirectory(Func<Amqp1NodeRequest, Amqp1NodeResolution>? resolve = null) : IAmqp1NodeDirectory
{
    public const string DynamicAddress = "/queue/surl-dynamic-00112233445566778899aabbccddeeff";

    public List<Amqp1NodeRequest> Requests { get; } = [];

    public Amqp1NodeResolution Resolve(Amqp1NodeRequest request)
    {
        Requests.Add(request);
        return resolve is null ? Amqp1NodeResolution.Found(request.CanonicalAddress ?? DynamicAddress) : resolve(request);
    }
}
