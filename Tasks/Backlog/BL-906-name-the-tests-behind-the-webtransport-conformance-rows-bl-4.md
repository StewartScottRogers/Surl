---
id: BL-906
title: Name the tests behind the WebTransport conformance rows BL-460 covers
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-460]
touches: [Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-906 — Name the tests behind the WebTransport conformance rows BL-460 covers

## Goal

Every row of `Documentation/Conformance/Extended/WebTransport.md` that names BL-460 names the tests that
now cover it instead.

## Context

- BL-460 built flow control, the session limit, authentication and the `--max-filesize` close
  (ADR-0117 decisions 4, 5, 7 and 8). Its lane could not edit the matrix: BL-879 held
  `Documentation/Conformance` at the time.
- Rows and their tests, all in `Surl.Extended.Protocol.WebTransport.UnitTests`:
  - `WTH3-3.2-4` (after authentication): `WebTransportConnectAuthenticationTests.RefuseAsync_ConnectWithoutALoginToAnUnservedPath_IsAnswered401BeforeThePathIsJudged`;
  - `WTH3-5.5-1`: `WebTransportApplicationProtocolTests.ServeAsync_Connection_SendsSettingsWithExtendedConnectDatagramsWtEnabledAndTheInitialFlowControlLimits`;
  - `WTH3-5.1-1`: `WebTransportFlowControlSettingsTests.IsOnFor_ClientSettings_IsOnWhenAnyInitialLimitIsNonZero`, `WebTransportSessionTests.ServeAsync_FlowControlCapsulesWithFlowControlOff_AreIgnored`;
  - `WTH3-5.1-2`: `WebTransportConnectHandlerTests.ServeSessionAsync_SecondSessionWithoutFlowControl_IsResetRequestRejected`;
  - `WTH3-5.2-1`: `WebTransportConnectHandlerTests.ServeSessionAsync_SeventeenthSessionWithFlowControl_IsResetRequestRejected`;
  - `WTH3-5.6-2`: `WebTransportSessionTests.ServeStreamAsync_HalfTheDataWindowRead_RaisesWtMaxDataToTheBytesReadPlusTheWindow`, `..._HalfTheDataWindowReadNear_MaxFilesize_RaisesWtMaxDataNoFurtherThanIt`, `..._ClientStreamFinished_RaisesWtMaxStreamsByTheStreamClosed`, and `WebTransportReceiveLimitsTests`;
  - `WTH3-5.6-3`: `WebTransportSessionTests.ServeStreamAsync_ClientStreamPastWtMaxStreams_EndsTheSessionWithFlowControlError`, `..._PayloadPastWtMaxData_EndsTheSessionWithFlowControlError`;
  - `WTH3-5.6-4`: `WebTransportSessionTests.ServeAsync_ClientLimitThatDoesNotRaiseItsLast_EndsTheSessionWithFlowControlError`, `WebTransportFlowControlTests`;
  - `WTH3-5.6-5`: `WebTransportSessionTests.ServeStreamAsync_ClientsWtMaxDataReached_EchoesUpToItSendsWtDataBlockedAndResumesWhenRaised`, `..._ClientsUnidirectionalWtMaxStreamsReached_SendsWtStreamsBlockedAndAnswersWhenRaised`, `WebTransportSendCreditTests`;
  - `WTH3-5.4-1`: `WebTransportSessionTests.ServeAsync_CapsuleHttp3Prohibits_ResetsTheConnectStreamWithMessageError`;
  - `WTH3-6-4` (code 1) and `Surl-8-1`: `WebTransportSessionTests.ServeStreamAsync_PayloadReachesMaxFilesize_ClosesTheSessionWithCode1ThenFin`, `ServeAsync_DatagramsReachMaxFilesize_CloseTheSessionWithCode1`;
  - `Surl-7-1` to `Surl-7-3`: `WebTransportConnectAuthenticationTests` (the refusal delay is the policy's, `Surl.Authentication`);
  - `Surl-8-2`: `WebTransportSessionTests.ServeAsync_CapsulePastTheMessageLimit_EndsTheSessionOnTheResetSurlHttp3Sent`;
  - `Surl-8-3`: the stream half is `WebTransportSession.EchoChunkLength`; the datagram half is BL-905.

## Acceptance criteria

- [ ] No row of `Documentation/Conformance/Extended/WebTransport.md` names BL-460; each names the tests above, and `Surl-8-3`'s datagram half names BL-905.

## Notes

## Log

- 2026-11-05: Created.
