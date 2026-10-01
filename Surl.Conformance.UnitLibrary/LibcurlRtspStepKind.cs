namespace Surl.Conformance;

/// <summary>
/// What one step of a <see cref="LibcurlRtspScript"/> does to libcurl's easy handle.
/// </summary>
public enum LibcurlRtspStepKind
{
    /// <summary>Sets <c>CURLOPT_RTSP_REQUEST</c> to the step's request and runs <c>curl_easy_perform</c>.</summary>
    Request,

    /// <summary>Sets <c>CURLOPT_RTSP_STREAM_URI</c> to the step's text.</summary>
    StreamUri,

    /// <summary>Sets <c>CURLOPT_RTSP_TRANSPORT</c> to the step's text.</summary>
    Transport,

    /// <summary>Sets <c>CURLOPT_RTSP_SESSION_ID</c> to the step's text.</summary>
    SessionId,

    /// <summary>Sets <c>CURLOPT_RTSP_CLIENT_CSEQ</c> to the step's number.</summary>
    ClientCSeq,

    /// <summary>Sends the step's bytes as the request body through <c>CURLOPT_POSTFIELDSIZE</c> and <c>CURLOPT_COPYPOSTFIELDS</c>.</summary>
    PostFields,

    /// <summary>Sends the step's bytes as the request body through <c>CURLOPT_UPLOAD</c>, <c>CURLOPT_INFILESIZE</c> and <c>CURLOPT_READFUNCTION</c>.</summary>
    Upload,

    /// <summary>Sends no request body: <c>CURLOPT_UPLOAD</c> 0, <c>CURLOPT_POSTFIELDSIZE</c> -1 and <c>CURLOPT_POSTFIELDS</c> NULL.</summary>
    NoBody,
}
