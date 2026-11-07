# STOMP requirement matrix

The server-side requirements `Surl.Extended.Protocol.Stomp` answers, for the `stomp` and `stomps`
schemes (ADR-0083 decision 9). Written from the STOMP Protocol Specification, Version 1.2
(<https://stomp.github.io/stomp-specification-1.2.html>), with the 1.1 and 1.0 specifications for
what differs, read 2026-11-05, the specifications
[ADR-0118](../../Planning/Decisions/ADR-0118-how-the-stomp-broker-answers-stomp-and-stomps.md)
decides by, never from the Curl port's client matrix. Each row binds the server; "Covered by" names
the task that proves it, until that task replaces its ID with the named test, or says why the row
needs no test.

The specification has no numbered sections, so IDs are `Stomp:STOMP12-<section>-<n>` with the
section's name, and `Stomp:Surl-<Decision>-<n>` for a rule ADR-0118 adds where the specification
leaves the server a choice. Every MUST, MUST NOT, SHOULD and SHOULD NOT that binds a server is a
row, and each MAY the server takes.

## Frames ("STOMP Frames", "Augmented BNF", "Value Encoding", "Repeated Header Entries")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-Frames-1 | MUST | A frame is read as a command, EOL, `name:value` header lines, an empty line, the body and a NUL; every frame the server writes has that layout, with LF line ends. | `StompFrameReaderTests.ReadFrameAsync_SpecificationSendExample_ReadsAndWritesByteForByte`, `StompFrameWriterTests.Encode_SpecificationConnectedExample_IsByteForByte` |
| Stomp:STOMP12-Frames-2 | MUST | An EOL is LF or CR LF, in every version. | `StompFrameReaderTests.ReadFrameAsync_CrLfLineEndsAndHeartBeatEols_AreAccepted` |
| Stomp:STOMP12-Frames-3 | MAY | Any number of EOLs between frames (heart-beats) are skipped. | `StompFrameReaderTests.ReadFrameAsync_CrLfLineEndsAndHeartBeatEols_AreAccepted` |
| Stomp:STOMP12-Frames-4 | MUST | A frame is read however its bytes are split across reads, and pipelined frames are each answered in order. | `StompFrameReaderTests.ReadFrameAsync_FrameSplitByteByByte_IsReadWhole`, `StompFrameReaderTests.ReadFrameAsync_BodyLargerThanTheBuffer_IsReadWhole` |
| Stomp:STOMP12-Frames-5 | MUST | Commands are matched case-sensitively; an unknown command, or CONNECTED, MESSAGE, RECEIPT or ERROR from a client, is ERROR `Unknown command`, then close. | `StompServerConnectionTests.ServeAsync_UnknownCommand_IsUnknownCommandWithTheReceiptEscapedByVersion` |
| Stomp:STOMP12-Frames-6 | MUST NOT | A client frame other than SEND carries no body; one that does is ERROR `Malformed frame`, then close. | `StompServerConnectionTests.ServeAsync_ConnectWithABody_IsMalformedFrameAndClosed`, `StompServerConnectionTests.ServeAsync_DisconnectWithABody_IsMalformedFrameAndClosed`; the routing frames: `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:STOMP12-Encoding-1 | MUST | Commands and headers are UTF-8; invalid UTF-8 is ERROR `Malformed frame`, then close. | `StompFrameReaderTests.ReadFrameAsync_InvalidUtf8_IsMalformedFrame` |
| Stomp:STOMP12-Encoding-2 | MUST | Outside CONNECT and CONNECTED, `\r`, `\n`, `\c` and `\\` are decoded in 1.2 (`\r` not in 1.1, nothing in 1.0), and the server escapes what it writes the same way. | `StompFrameReaderTests.ReadFrameAsync_HeaderWithEachEscape_IsDecoded`, `StompHeaderEscapingTests.TryDecode_EachEscapeIn12_IsDecoded`, `StompHeaderEscapingTests.TryDecode_CarriageReturnEscapeIn11_IsRefused`, `StompFrameReaderTests.ReadFrameAsync_BeforeAVersion_NothingIsUnescaped`, `StompHeaderEscapingTests.Encode_EachSpecialCharacter_IsEscapedAsTheVersionCan` |
| Stomp:STOMP12-Encoding-3 | MUST | An undefined escape sequence is a fatal error: ERROR `Malformed frame`, then close. | `StompFrameReaderTests.ReadFrameAsync_UndefinedEscape_IsMalformedFrame`, `StompHeaderEscapingTests.TryDecode_UndefinedEscape_IsRefused` |
| Stomp:STOMP12-Encoding-4 | MUST NOT | CONNECT and CONNECTED headers are not escaped or unescaped; a colon after the first in a CONNECT header is part of its value. | `StompFrameReaderTests.ReadFrameAsync_ConnectHeaders_AreNotUnescaped`, `StompServerConnectionTests.ServeAsync_ConnectHeaders_AreReadUnescaped` |
| Stomp:STOMP12-Repeated-1 | MUST | Of repeated header entries only the first is used, and later ones are not passed to the bus. | `StompFrameReaderTests.ReadFrameAsync_RepeatedHeader_KeepsTheFirstValue` |
| Stomp:STOMP12-ContentLength-1 | MUST | With `content-length`, that many octets are the body, NULs included, and a NUL must follow. | `StompFrameReaderTests.ReadFrameAsync_BodyWithNulAndContentLength_IsReadByContentLength` |
| Stomp:STOMP12-ContentLength-2 | MUST | Without `content-length`, the body ends at the first NUL. | `StompFrameReaderTests.ReadFrameAsync_SpecificationSendExample_ReadsAndWritesByteForByte` |
| Stomp:STOMP12-ContentLength-3 | SHOULD | Every MESSAGE the server writes carries `content-length`. | `StompServerConnectionTests.Send_ToATopic_IsDeliveredAsMessageWithTheSpecificationsHeaders` |
| Stomp:STOMP12-ContentLength-4 | MUST | A `content-length` that is not a non-negative decimal integer, or no NUL after the body, is ERROR `Malformed frame`, then close. | `StompFrameReaderTests.ReadFrameAsync_MalformedFrame_IsMalformedFrame` |

## Connecting ("CONNECT or STOMP Frame", "CONNECTED Frame", "Protocol Negotiation")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-Negotiation-1 | MUST | The server chooses the highest of 1.0, 1.1 and 1.2 in `accept-version` (`accept-version:1.0,1.1,1.2` gets `version:1.2`). | `StompVersionNegotiationTests.ChooseHighest_AcceptVersion_IsTheHighestCommonVersion`, `StompServerConnectionTests.ServeAsync_ConnectAcceptingEveryVersion_IsAnswered12` |
| Stomp:STOMP12-Negotiation-2 | MUST | No `accept-version` header negotiates 1.0. | `StompVersionNegotiationTests.ChooseHighest_NoAcceptVersion_Is10`, `StompServerConnectionTests.ServeAsync_ConnectWithoutAcceptVersion_IsAnswered10WithoutHeartBeat` |
| Stomp:STOMP12-Negotiation-3 | MUST | No common version is ERROR with `version:1.0,1.1,1.2`, `content-type:text/plain` and the body `Supported protocol versions are 1.0 1.1 1.2`, then close, before any login is judged. | `StompServerConnectionTests.ServeAsync_NoCommonVersion_IsVersionErrorAndClosed`, `StompErrorsTests.UnsupportedVersionFrame_IsTheAdrsFrame`, `StompFrameWriterTests.Encode_SpecificationVersionErrorExample_IsByteForByte` |
| Stomp:STOMP12-Connect-1 | MUST | A `STOMP` frame is served as CONNECT when it accepts 1.1 or 1.2; negotiating 1.0 it is the no-common-version ERROR. | `StompServerConnectionTests.ServeAsync_StompFrameAccepting11_IsAnswered11` |
| Stomp:STOMP12-Connected-1 | MUST | CONNECTED carries `version`, `heart-beat` (not in 1.0), `session` (32 random lower-case hex digits) and `server:surl`, in that order, unescaped, with no body. | `StompServerConnectionTests.ServeAsync_ConnectAcceptingEveryVersion_IsAnswered12`, `StompServerConnectionTests.ServeAsync_ConnectWithoutAcceptVersion_IsAnswered10WithoutHeartBeat`, `StompServerConnectionTests.ServeAsync_Session_Is32LowerCaseHexDigitsDrawnPerConnection` |
| Stomp:STOMP12-Connected-2 | MUST NOT | `server` never carries surl's version (ADR-0006 section 3). | `StompServerConnectionTests.ServeAsync_ConnectAcceptingEveryVersion_IsAnswered12` |
| Stomp:STOMP12-Connect-2 | MUST | Any frame before CONNECTED is ERROR `Not connected`, then close; a second CONNECT or STOMP is ERROR `Already connected`, then close. | `StompServerConnectionTests.ServeAsync_FrameBeforeConnect_IsNotConnectedAndClosed`, `StompServerConnectionTests.ServeAsync_SecondConnect_IsAlreadyConnectedAndClosed` |
| Stomp:Surl-Host-1 | MAY | Any `host` value, or none, is accepted for the one virtual host and never echoed. | `StompServerConnectionTests.ServeAsync_ConnectAcceptingEveryVersion_IsAnswered12` |
| Stomp:Surl-Connect-1 | MUST | No complete CONNECT by `--head-timeout` from accept closes with no bytes. | `StompServerConnectionTests.ServeAsync_NoConnectWithinTheHeadTimeout_IsClosedWithNoBytes` |
| Stomp:Surl-Version-1 | MUST | 1.0 sessions take SUBSCRIBE without `id` (its destination is its id), ACK by `message-id`, no NACK (ERROR `Unknown command`), no `client-individual` and no heart-beating; 1.1 sessions ACK and NACK by `message-id` and `subscription`. | `StompServerConnectionTests.Session10_SubscribesByDestinationAcksByMessageIdAndHasNoNack`, `StompServerConnectionTests.AckAndNack_In11_NameTheMessageByMessageId`, `StompServerConnectionTests.SubscribeIn10_ClientIndividual_IsInvalidAckMode`, `StompServerConnectionTests.Message_In10_LeavesOutHeadersItCannotWriteUnescaped` |

## Logins ("CONNECT or STOMP Frame"; ADR-0032)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:Surl-Login-1 | MUST | Without `--allow-anonymous`, `login` and `passcode` (empty when absent) are judged by `IAuthenticationPolicy`; accepted goes on to CONNECTED. | `StompServerConnectionTests.ServeAsync_LoginWithoutPasscode_IsJudgedWithAnEmptyPassword` |
| Stomp:Surl-Login-2 | MUST | No `login`, refused credentials, or a login refused as plain text on `stomp` is ERROR `Authentication failed` after the refusal delay, then close, naming neither account nor cause. | `StompServerConnectionTests.ServeAsync_BadCredentials_IsAuthenticationFailedAndClosed`, `StompServerConnectionTests.ServeAsync_RefusedPlaintext_IsAuthenticationFailedAndClosed` |
| Stomp:Surl-Login-3 | MUST | Under `--allow-anonymous`, CONNECT without `login` is served, and a `login` given is accepted unchecked. | `StompServerConnectionTests.ServeAsync_AcceptedUncheckedWithoutLogin_IsServedAsAnonymous` |

## Destinations and messages ("SEND", "SUBSCRIBE", "UNSUBSCRIBE", "MESSAGE"; ADR-0100 decision 3.2)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-Send-1 | REQUIRED | A SEND without `destination` is ERROR `Missing destination header`, then close. | `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:STOMP12-Send-2 | MUST | SEND publishes one bus message at QoS 1: the body exact, `content-type` as the content type, other non-STOMP headers as string headers in order. | `StompServerConnectionTests.Send_ToATopic_IsDeliveredAsMessageWithTheSpecificationsHeaders` |
| Stomp:STOMP12-Subscribe-1 | REQUIRED | A SUBSCRIBE without `destination`, or without `id` in 1.1 or 1.2, is ERROR `Missing destination header` or `Missing id header`, then close. | `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:STOMP12-Subscribe-2 | MUST | A SUBSCRIBE reusing a live `id` on the connection is ERROR `Duplicate subscription id`, then close. | `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:STOMP12-Unsubscribe-1 | MUST | UNSUBSCRIBE ends the subscription named by `id`, its unacknowledged queue messages going back to the queue; an unknown `id` is ignored. | `StompServerConnectionTests.Unsubscribe_PutsUnacknowledgedQueueMessagesBackAndEndsDelivery`, `StompServerConnectionTests.Ack_NamingNoOutstandingMessage_IsIgnored` |
| Stomp:STOMP12-Message-1 | MUST | MESSAGE carries `destination`, `message-id` and `subscription`, then `ack` (1.2, client modes only), `content-type` when known and `content-length`, then the bus headers, the body exact. | `StompServerConnectionTests.Send_ToATopic_IsDeliveredAsMessageWithTheSpecificationsHeaders` |
| Stomp:STOMP12-Message-2 | MUST | `message-id` is unique on the connection; a redelivered message gets a new one. | `StompServerConnectionTests.Unacknowledged_AfterTheSubscriberDisconnects_IsRedeliveredWithANewMessageId` |
| Stomp:STOMP12-Message-3 | MUST NOT | MESSAGE never carries a bus header named like a STOMP header, one holding U+0000, or in 1.0 one it cannot write unescaped. | `StompServerConnectionTests.Message_BusHeaders_AreWrittenEscapedButNeverStompsOwnOrOnesHoldingNul`, `StompServerConnectionTests.Message_In10_LeavesOutHeadersItCannotWriteUnescaped` |
| Stomp:Surl-Destination-1 | MUST | `/topic/<t>` is the bus topic `<t>`, delivered to every matching live subscription of any protocol; a SUBSCRIBE may hold `+` and `#`, a SEND may not. | `StompServerConnectionTests.Send_ToATopic_IsDeliveredAsMessageWithTheSpecificationsHeaders`, `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:Surl-Destination-2 | MUST | A message published on the bus under an MQTT name is delivered as MESSAGE on the mapped `/topic/` destination. | `StompServerConnectionTests.Message_PublishedOnTheBusUnderAnMqttName_IsDeliveredOnTheMappedTopicDestination`, `StompServerConnectionTests.Message_OnATopicTheSchemesAliasHides_IsNotDeliveredAndTheAliasedTopicIs` |
| Stomp:Surl-Destination-3 | MUST | `/queue/<q>` is the bus queue `<q>`, created durable on first use, each message to one subscription round robin, kept until consumed. | `StompServerConnectionTests.Send_ToAQueue_IsKeptUntilConsumedAndGoesToOneSubscriptionRoundRobin` |
| Stomp:Surl-Destination-4 | MUST | Any other destination, an empty name, or a non-canonical topic is ERROR `Unknown destination`, then close. | `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:Surl-Destination-5 | MUST NOT | A topic keeps nothing for STOMP: a message sent while no STOMP subscriber is subscribed is never delivered to a later one. | `StompServerConnectionTests.Send_ToATopicNobodySubscribes_IsNotDeliveredToALaterSubscriber` |
| Stomp:Surl-Access-1 | MUST | A SEND or SUBSCRIBE refused by `--topic-access` is ERROR `Access refused`, then close, the same whether or not the destination exists. | `StompServerConnectionTests.Frame_RefusedByTopicAccess_IsAccessRefusedAndCreatesNothing` |

## Acknowledgement ("SUBSCRIBE ack Header", "ACK", "NACK")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-Ack-1 | MUST | Under `auto` a message is acknowledged when written. | `StompServerConnectionTests.Send_ToAQueue_IsKeptUntilConsumedAndGoesToOneSubscriptionRoundRobin` |
| Stomp:STOMP12-Ack-2 | MUST | Under `client` an ACK or NACK settles the named message and every earlier unsettled one of its subscription. | `StompServerConnectionTests.Ack_UnderClient_AcknowledgesTheNamedMessageAndEveryEarlierOne`, `StompServerConnectionTests.Nack_UnderClient_IsCumulativeAndATopicMessageIsDropped` |
| Stomp:STOMP12-Ack-3 | MUST | Under `client-individual` an ACK or NACK settles the named message only. | `StompServerConnectionTests.Ack_UnderClientIndividual_AcknowledgesTheNamedMessageOnly` |
| Stomp:STOMP12-Ack-4 | MUST | An `ack` value outside the version's modes is ERROR `Invalid ack mode`, then close; ACK or NACK without its id header is ERROR `Missing id header`, then close. | `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:STOMP12-Nack-1 | MUST | A NACK of a queue message puts it back at its place, redelivered to a subscription of the queue; of a topic message drops it. | `StompServerConnectionTests.Nack_OfAQueueMessage_PutsItBackForRedelivery`, `StompServerConnectionTests.Nack_UnderClient_IsCumulativeAndATopicMessageIsDropped` |
| Stomp:Surl-Ack-1 | MUST | Unacknowledged queue messages are redelivered when their subscription ends by UNSUBSCRIBE, DISCONNECT or the connection's end. | `StompServerConnectionTests.Unsubscribe_PutsUnacknowledgedQueueMessagesBackAndEndsDelivery`, `StompServerConnectionTests.Unacknowledged_AfterTheSubscriberDisconnects_IsRedeliveredWithANewMessageId` |
| Stomp:Surl-Ack-2 | MAY | An ACK or NACK naming no outstanding message of the connection is ignored. | `StompServerConnectionTests.Ack_NamingNoOutstandingMessage_IsIgnored` |

## Transactions, receipts and disconnecting ("BEGIN", "COMMIT", "ABORT", "Header receipt", "RECEIPT", "DISCONNECT")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-Transaction-1 | MUST | SEND, ACK and NACK in a transaction take effect in order on COMMIT and are discarded on ABORT. | `StompServerConnectionTests.Transaction_SendsAreDeliveredOnlyOnCommitAndDroppedOnAbort`, `StompServerConnectionTests.Transaction_AcksTakeEffectOnCommitAndAreDiscardedOnAbort` |
| Stomp:STOMP12-Transaction-2 | MUST | A transaction still open when the connection ends is aborted. | `StompServerConnectionTests.Transaction_StillOpenWhenTheConnectionEnds_IsAborted` |
| Stomp:STOMP12-Transaction-3 | REQUIRED | BEGIN, COMMIT and ABORT without `transaction` are ERROR `Missing transaction header`; a reused open name ERROR `Duplicate transaction`; an unknown one ERROR `Unknown transaction`; each then close. | `StompServerConnectionTests.Frame_BreakingARoutingRule_IsAnsweredErrorAndClosed` |
| Stomp:Surl-Transaction-1 | MUST | More than 100 open transactions, or held bodies past `--max-filesize`, is ERROR `Resource limit exceeded`, then close. | `StompServerConnectionTests.Begin_PastTheOpenTransactionBound_IsResourceLimitExceeded`, `StompServerConnectionTests.Transaction_HoldingMoreThanTheUploadLimit_IsResourceLimitExceeded` |
| Stomp:STOMP12-Receipt-1 | MUST | A client frame other than CONNECT and STOMP carrying `receipt` is answered RECEIPT with that `receipt-id` after it took effect. | `StompServerConnectionTests.Send_ToATopic_IsDeliveredAsMessageWithTheSpecificationsHeaders`, `StompServerConnectionTests.Transaction_SendsAreDeliveredOnlyOnCommitAndDroppedOnAbort` |
| Stomp:STOMP12-Disconnect-1 | MUST | DISCONNECT with `receipt` finishes earlier frames, writes the RECEIPT, then closes; without, closes with no bytes; nothing after it is read. | `StompServerConnectionTests.ServeAsync_DisconnectWithReceipt_IsAnsweredReceiptAndClosed`, `StompServerConnectionTests.ServeAsync_DisconnectWithoutReceipt_IsClosedWithNoBytes` |

## ERROR ("ERROR", "Connection Lingering")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-Error-1 | SHOULD | ERROR carries `message` with one of ADR-0118's fixed texts, and `receipt-id` when the frame at fault carried `receipt`. | `StompServerConnectionTests.Frame_AtFaultCarryingAReceipt_IsAnsweredErrorWithItsReceiptId`, `StompErrorsTests.Frame_WithReceipt_CarriesReceiptId` |
| Stomp:STOMP12-Error-2 | MUST | The server closes the connection after every ERROR. | `StompServerConnectionTests.ServeAsync_InvalidHeartBeat_IsInvalidHeartBeatAndClosed`, `StompServerConnectionTests.ServeAsync_FrameBeforeConnect_IsNotConnectedAndClosed` |
| Stomp:STOMP12-Lingering-1 | SHOULD | After an ERROR the server lingers up to 1 s, discarding what the client sends, then closes gracefully. | `StompServerConnectionTests.ServeAsync_ClientStillSendingAfterAnError_IsReadForOneSecondThenClosed` |
| Stomp:STOMP12-Error-3 | MUST NOT | No ERROR names an account, a destination, a path, an exception or a version; only the client's `receipt` is echoed. | `StompErrorsTests.Frame_WithReceipt_CarriesReceiptId`, `StompServerConnectionTests.ServeAsync_UnknownCommand_IsUnknownCommandWithTheReceiptEscapedByVersion` |

## Heart-beating ("Heart-beating")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-HeartBeat-1 | MUST | CONNECTED offers `heart-beat:1000,<sy>`, `sy` a quarter of `--idle-timeout` (30000 when off). | `StompServerOptionsTests.HeartBeatOffer_Default_IsOneSecondAndAQuarterOfTheIdleTimeout`, `StompServerOptionsTests.HeartBeatWanted_IdleTimeoutOff_IsThirtySeconds`, `StompServerConnectionTests.ServeAsync_HeartBeatWanted_FollowsTheIdleTimeout` |
| Stomp:STOMP12-HeartBeat-2 | MUST | A `heart-beat` that is not two non-negative integers separated by a comma is ERROR `Invalid heart-beat`, then close. | `StompHeartBeatTests.TryParse_NotTwoNonNegativeIntegers_IsRefused`, `StompServerConnectionTests.ServeAsync_InvalidHeartBeat_IsInvalidHeartBeatAndClosed` |
| Stomp:STOMP12-HeartBeat-3 | MUST | When the client's `cy` is not 0, the server sends an EOL every `max(1000, cy)` ms in which it wrote nothing else. | `StompServerConnectionTests.HeartBeat_ClientWantingOne_GetsAnEolEveryIntervalInWhichNothingElseWasWritten` |
| Stomp:STOMP12-HeartBeat-4 | MAY | When the client's `cx` is not 0, nothing from the client for twice `max(cx, sy)` ms closes the connection with no bytes, timed on the injected `TimeProvider`. | `StompServerConnectionTests.HeartBeat_ClientSilentPastTheTolerance_IsClosedWithNoBytes` |

## TLS and limits ("Size Limits"; ADR-0006; ADR-0010)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Stomp:STOMP12-Size-1 | SHOULD | A command or header line over `--max-line` is ERROR `Header line too long`, then close. | `StompFrameReaderTests.ReadFrameAsync_LineOverTheLimit_IsHeaderLineTooLong`, `StompServerConnectionTests.ServeAsync_HeaderLineOverTheLineLimit_IsHeaderLineTooLongAndClosed` |
| Stomp:STOMP12-Size-2 | SHOULD | More than 128 headers in a frame is ERROR `Too many headers`, then close. | `StompFrameReaderTests.ReadFrameAsync_MoreThan128Headers_IsTooManyHeaders`, `StompFrameReaderTests.ReadFrameAsync_128Headers_IsRead` |
| Stomp:STOMP12-Size-3 | SHOULD | A body over the smaller of `--max-message` and `--max-filesize` is ERROR `Frame too large`, then close, judged from `content-length` before it is read. | `StompFrameReaderTests.ReadFrameAsync_BodyOverTheLimit_IsFrameTooLarge`, `StompServerConnectionTests.ServeAsync_FrameOverTheSizeLimit_IsFrameTooLargeAndClosed`; the bus's message bound: `StompServerConnectionTests.Send_OverTheBusMessageBound_IsFrameTooLarge` |
| Stomp:Surl-Limit-1 | MUST | Past 1000 subscriptions a connection, ERROR `Too many subscriptions`, then close. | `StompServerConnectionTests.Subscribe_PastTheBusSubscriptionBound_IsTooManySubscriptions` |
| Stomp:Surl-Limit-2 | MUST | A full bus store is ERROR `Resource limit exceeded`, then close. | `StompServerConnectionTests.Frame_PastTheBusQueueBound_IsResourceLimitExceeded`, `StompServerConnectionTests.Send_ToAFullQueue_IsResourceLimitExceeded` |
| Stomp:Surl-Limit-3 | MUST | 64 MiB of frames not yet taken by the transport is ERROR `Resource limit exceeded` (best effort), then close. | `StompServerConnectionTests.Delivery_PastThePendingBound_IsResourceLimitExceededAndClosed`, `StompOutboxTests.Enqueue_PastTheBoundWhileTheWriterWaits_OverflowsAndEndsTheWriting` |
| Stomp:Surl-Limit-4 | MUST | A connection past `--max-connections` or `--max-connections-per-address` is ERROR `Too many connections`, then close. | `StompServerTests.WriteRefusalAsync_AnyRefusal_IsTooManyConnectionsAndClosed` |
| Stomp:Surl-Tls-1 | MUST | `stomps` is TLS 1.2 or 1.3 from the first byte, through the listener seam. | n/a: `ImplicitTlsSchemeServer` (ADR-0010) wraps the broker, composed and tested by BL-533; the broker itself never sees TLS |
| Stomp:Surl-Tls-2 | MUST NOT | `stomp` never upgrades to TLS. | n/a: STOMP has no STARTTLS, so the broker has no code path that could |
