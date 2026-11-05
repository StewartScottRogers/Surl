using System.IO.Compression;

namespace Surl.Extended.Protocol.Grpc;

/// <summary>
/// The request encodings the server reads (ADR-0116 decision 6): <c>identity</c>, <c>gzip</c> and
/// <c>deflate</c> (the zlib format), chosen by the call's <c>grpc-encoding</c>.
/// </summary>
public enum GrpcMessageEncoding
{
    /// <summary>No compression; also what a call without <c>grpc-encoding</c> uses.</summary>
    Identity,

    /// <summary>gzip (RFC 1952), read with <see cref="GZipStream" />.</summary>
    Gzip,

    /// <summary>deflate in the zlib format (RFC 1950), read with <see cref="ZLibStream" />.</summary>
    Deflate,
}
