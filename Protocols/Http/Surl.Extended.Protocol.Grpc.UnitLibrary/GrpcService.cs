namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// One served service (ADR-0116 decision 1): its fully qualified name, the first segment of
/// <c>:path</c>, and its methods. <see cref="GrpcStreamHandler" /> is given the services it serves, so
/// a new service plugs in without editing the handler.
/// </summary>
/// <param name="Name">The fully qualified service name, e.g. <c>surl.test.Echo</c>.</param>
/// <param name="Methods">The service's methods, each name once.</param>
public sealed record GrpcService(string Name, IReadOnlyList<GrpcMethod> Methods);
