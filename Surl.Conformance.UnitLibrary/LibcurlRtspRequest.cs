namespace Surl.Conformance;

/// <summary>
/// The RTSP requests libcurl's <c>CURLOPT_RTSP_REQUEST</c> takes, each with the value of its
/// <c>RTSPREQ_*</c> constant in libcurl 8.21.0's <c>include/curl/curl.h</c>, so
/// <c>Run-LibcurlRtspScript.cs</c> passes a member straight to <c>curl_easy_setopt</c>.
/// </summary>
public enum LibcurlRtspRequest
{
    /// <summary><c>RTSPREQ_OPTIONS</c>: an <c>OPTIONS</c> request.</summary>
    Options = 1,

    /// <summary><c>RTSPREQ_DESCRIBE</c>: a <c>DESCRIBE</c> request.</summary>
    Describe = 2,

    /// <summary><c>RTSPREQ_ANNOUNCE</c>: an <c>ANNOUNCE</c> request, carrying the body.</summary>
    Announce = 3,

    /// <summary><c>RTSPREQ_SETUP</c>: a <c>SETUP</c> request with <c>CURLOPT_RTSP_TRANSPORT</c>.</summary>
    Setup = 4,

    /// <summary><c>RTSPREQ_PLAY</c>: a <c>PLAY</c> request.</summary>
    Play = 5,

    /// <summary><c>RTSPREQ_PAUSE</c>: a <c>PAUSE</c> request.</summary>
    Pause = 6,

    /// <summary><c>RTSPREQ_TEARDOWN</c>: a <c>TEARDOWN</c> request.</summary>
    Teardown = 7,

    /// <summary><c>RTSPREQ_GET_PARAMETER</c>: a <c>GET_PARAMETER</c> request, with or without a body.</summary>
    GetParameter = 8,

    /// <summary><c>RTSPREQ_SET_PARAMETER</c>: a <c>SET_PARAMETER</c> request, carrying the body.</summary>
    SetParameter = 9,

    /// <summary><c>RTSPREQ_RECORD</c>: a <c>RECORD</c> request.</summary>
    Record = 10,

    /// <summary><c>RTSPREQ_RECEIVE</c>: no request; libcurl reads interleaved data from the connection.</summary>
    Receive = 11,
}
