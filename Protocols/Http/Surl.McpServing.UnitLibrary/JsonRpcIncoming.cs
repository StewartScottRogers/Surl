namespace Surl.McpServing;

/// <summary>
/// One message read from the client: a <see cref="JsonRpcRequest" />, a <see cref="JsonRpcNotification" />,
/// a <see cref="JsonRpcClientResponse" />, or a <see cref="JsonRpcInvalidMessage" /> the server answers with an error.
/// </summary>
public abstract class JsonRpcIncoming
{
    private protected JsonRpcIncoming()
    {
    }
}
