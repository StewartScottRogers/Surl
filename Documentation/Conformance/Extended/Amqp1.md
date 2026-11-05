# Amqp1 requirement matrix

The server-side requirements `Surl.Extended.Protocol.Amqp1` answers, for the `amqp1` and `amqp1s`
schemes (ADR-0083 decision 9). Written from OASIS AMQP Version 1.0 (29 October 2012,
<https://docs.oasis-open.org/amqp/core/v1.0/os/amqp-core-overview-v1.0-os.html>), RFC 4616 and
RFC 4505, the specifications
[ADR-0107](../../Planning/Decisions/ADR-0107-how-the-amqp-1-0-container-answers-amqp1-and-amqp1s.md)
decides by, never from the Curl port's client matrices. Each row binds the container; "Covered by"
names the task that proves it, until that task replaces its ID with the named test, or says why the
row needs no test.

IDs are `Amqp1:Types-<section>-<n>` for Part 1, `Amqp1:Core-<section>-<n>` for Part 2,
`Amqp1:Messaging-<section>-<n>` for Part 3, `Amqp1:Security-<section>-<n>` for Part 5,
`Amqp1:RFC<n>-<n>` for an RFC, and `Amqp1:Surl-<Decision>-<n>` for a rule ADR-0107 adds where the
specification leaves the container a choice. Part 4 (transactions) is BL-707's.

## Types and encodings (Part 1, sections 1.2 to 1.6)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Types-1.6-1 | MUST | Every fixed-width primitive is read and written in each encoding Part 1 section 1.6 gives it: null, boolean (`0x56`, `0x41`, `0x42`), ubyte, ushort, uint (`0x70`, `0x52`, `0x43`), ulong (`0x80`, `0x53`, `0x44`), byte, short, int (`0x71`, `0x54`), long (`0x81`, `0x55`), float, double, decimal32, decimal64, decimal128, char, timestamp and uuid, big-endian. | BL-510 |
| Amqp1:Types-1.6-2 | MUST | Variable-width primitives are read in both widths: vbin8 and vbin32, str8-utf8 and str32-utf8, sym8 and sym32. | BL-510 |
| Amqp1:Types-1.6-3 | MUST | Compound types are read in every encoding: list0, list8 and list32, map8 and map32 (an even element count), array8 and array32 (one constructor for every element). | BL-510 |
| Amqp1:Types-1.6-4 | MUST | A size or count that runs past its enclosing value or frame is a decode error, never read past. | BL-510 |
| Amqp1:Types-1.6-5 | MUST | A string is valid UTF-8 and a symbol is ASCII; one that is not is a decode error. | BL-510 |
| Amqp1:Types-1.4-1 | MUST | A composite value is a described list of its fields in order; trailing fields may be left out and a missing field is read as null, taking its default. | BL-510 |
| Amqp1:Types-1.5-1 | MUST | A descriptor is read as a symbol (`amqp:open:list`) or a ulong (`0x0000000000000010`), and both name the same type. | BL-510 |
| Amqp1:Types-1.2-1 | MUST | A field marked multiple is read as a single value or an array of them. | BL-510 |
| Amqp1:Types-1.2-2 | MUST | A described value whose descriptor the container does not know is read and skipped where the field allows any value (message annotations, application properties, properties maps). | BL-510 |
| Amqp1:Surl-Encode-1 | MUST | The container writes the smallest encoding of each value (`0x40` for null, `0x41`/`0x42` for booleans, `0x43`/`0x44` for zero, small forms when they fit, list0 for an empty list) and leaves trailing null fields out. | BL-510 |

## Version negotiation and headers (Part 2 section 2.2; Part 5 sections 5.1, 5.2, 5.3.1)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Core-2.2-1 | MUST | The container reads the client's 8-byte protocol header before writing anything, and answers it with a header of its own. | BL-511 |
| Amqp1:Core-2.2-2 | MUST | A header the container does not accept (another version, another protocol id, not `AMQP`) is answered with the header it would accept, `AMQP 3 1 0 0`, and the outgoing stream is closed with no frame. | BL-511 |
| Amqp1:Surl-Headers-1 | MUST | `AMQP 0 1 0 0` without SASL is answered `AMQP 3 1 0 0` and closed unless `--allow-anonymous` is given, when it is answered `AMQP 0 1 0 0` and the connection is anonymous. | BL-511 |
| Amqp1:Security-5.3.1-1 | MUST | `AMQP 3 1 0 0` is answered with the same header and the SASL layer follows. | BL-511 |
| Amqp1:Security-5.3.1-2 | MUST | After a successful outcome the client's `AMQP 0 1 0 0` is answered with `AMQP 0 1 0 0`; another header there is answered `AMQP 0 1 0 0` and closed. | BL-511 |
| Amqp1:Security-5.2-1 | MUST | `AMQP 2 1 0 0` on `amqp1` with a certificate is answered `AMQP 2 1 0 0` and followed by the server side of a TLS handshake, after which the headers are negotiated afresh. | BL-511 |
| Amqp1:Surl-Headers-2 | MUST | `AMQP 2 1 0 0` on `amqp1` without a certificate, on `amqp1s`, or inside a TLS layer is answered `AMQP 3 1 0 0` and closed. | BL-511 |
| Amqp1:Core-2.4.1-1 | MUST | Frames pipelined after a header, before its answer, are read in order and not lost; bytes after a TLS header are discarded before the handshake. | BL-511 |

## SASL (Part 5 section 5.3; RFC 4616; RFC 4505)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Security-5.3.1-3 | MUST | SASL frames are type `0x01`, at most 512 bytes; a larger frame, an AMQP frame or an out-of-order SASL frame during the SASL layer closes the connection with no bytes. | BL-511 |
| Amqp1:Security-5.3.3.1-1 | MUST | The container's first SASL frame is `sasl-mechanisms`, listing the policy's mechanisms for the scheme and TLS state, then `ANONYMOUS` only under `--allow-anonymous`. | BL-511 |
| Amqp1:Security-5.3.3.2-1 | MUST | `sasl-init` naming a mechanism not offered is answered with outcome code 1 (`auth`). | BL-511 |
| Amqp1:Security-5.3.3.3-1 | MUST | A mechanism needing more steps is run with `sasl-challenge` and `sasl-response` until the policy decides. | BL-511 |
| Amqp1:Security-5.3.3.5-1 | MUST | An accepted login is answered with outcome code 0 (`ok`), with `additional-data` when the mechanism's last server message needs it. | BL-511 |
| Amqp1:Security-5.3.3.5-2 | MUST | A refused login is answered with outcome code 1 after the policy's delay, naming neither the account nor the cause, and the connection is closed. | BL-511 |
| Amqp1:Security-5.3.3.6-1 | MUST NOT | Codes 2, 3 and 4 are never sent (ADR-0107 decision 2). | n/a: a code that is never sent has no case; the outcome tests above pin every code that is |
| Amqp1:Security-5.3.3.5-3 | MUST | A SASL frame after the outcome closes the connection with no bytes. | BL-511 |
| Amqp1:RFC4616-1 | MUST | `PLAIN`'s message is `[authzid] NUL authcid NUL passwd`; an `authzid` other than empty or the `authcid` is refused with code 1. | BL-511 |
| Amqp1:RFC4505-1 | MUST | `ANONYMOUS` is accepted only under `--allow-anonymous`, its trace not checked; otherwise it is not offered and is refused with code 1. | BL-511 |
| Amqp1:Surl-Sasl-1 | MUST | The login, or none, is the caller ADR-0102's topic access judges; under `--allow-anonymous` an offered mechanism is accepted unchecked. | BL-511 |

## Framing and connections (Part 2 sections 2.3, 2.4, 2.7.1, 2.7.9)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Core-2.3.1-1 | MUST | A frame is a 4-byte size (the whole frame), DOFF in 4-byte words (at least 2), a type byte and a 2-byte channel; a size below 8 or a DOFF below 2 or past the frame closes the connection with `amqp:connection:framing-error`. | BL-510 |
| Amqp1:Core-2.3.2-1 | MUST | An AMQP frame with no body is read and ignored except as activity for the idle time-out. | `Amqp1ContainerConnectionTests.ServeAsync_EmptyFramesBeforeOpen_AreSkipped`, `Amqp1ContainerConnectionTests.ServeAsync_ClientGoneAfterOpen_EndsWithNoFurtherBytes` |
| Amqp1:Core-2.4.1-2 | MUST | After the AMQP header the first frame both ways is `open`; another frame before it, or a second `open`, closes the connection with `amqp:connection:framing-error`. | `Amqp1ContainerConnectionTests.ServeAsync_FrameOtherThanOpenBeforeOpen_IsAnsweredWithOpenThenCloseWithFramingError`, `Amqp1ContainerConnectionTests.ServeAsync_SecondOpen_ClosesWithFramingError` |
| Amqp1:Core-2.7.1-1 | MUST | The container's `open` carries `container-id` `surl-` and 32 hex digits, `max-frame-size` the smaller of 65536 and `--max-message`, `channel-max` 63, `idle-time-out` half of `--idle-timeout`, and `properties` `product` = `surl`. | `Amqp1ContainerConnectionTests.ServeAsync_Open_IsAnsweredWithTheContainersOpen`, `Amqp1ContainerConnectionTests.ServeAsync_IdleTimeoutOff_AdvertisesNoIdleTimeOut`, `Amqp1ContainerOptionsTests` |
| Amqp1:Core-2.7.1-2 | MUST | A client `max-frame-size` below 512 closes the connection with `amqp:invalid-field`. | `Amqp1ContainerConnectionTests.ServeAsync_OpenWithMaxFrameSizeBelow512_ClosesWithInvalidField` |
| Amqp1:Core-2.4.1-3 | MUST NOT | The container writes no frame larger than the client's `max-frame-size`. | BL-513 (it splits transfers to the client's `max-frame-size`; BL-512 writes no performative beyond its own fields, except that an answering `attach` echoes the client's other terminus) |
| Amqp1:Core-2.4.1-4 | MUST | A frame larger than the container's `max-frame-size` (512 before `open`) closes the connection with `amqp:connection:framing-error`, its body not read. | `Amqp1ContainerConnectionTests.ServeAsync_MalformedFrameAfterOpen_ClosesWithFramingError`; before `open`, `Amqp1ContainerConnectionTests.ServeAsync_NoOpen_ClosesWithNoBytes` (no bytes, ADR-0107 decision 7) |
| Amqp1:Core-2.4.3-1 | MUST | A client `close` is answered with `close` and the connection ends; its error, if any, is noted. | `Amqp1ContainerConnectionTests.ServeAsync_Close_IsAnsweredWithClose`, `Amqp1ContainerConnectionTests.ServeAsync_CloseWithAnError_IsAnsweredWithCloseAndNoted` |
| Amqp1:Core-2.4.5-1 | MUST | When the client's `open` carries a nonzero `idle-time-out`, the container writes an empty frame whenever it has written nothing for half of it, at most every 100 ms, on the injected `TimeProvider`. | `Amqp1ContainerConnectionTests.ServeAsync_ClientIdleTimeOut_IsKeptWithEmptyFramesAtHalfOfIt`, `Amqp1ContainerConnectionTests.ServeAsync_ClientIdleTimeOutZero_SendsNoEmptyFrames` |
| Amqp1:Core-2.4.5-2 | SHOULD | A client silent for `--idle-timeout` is sent `close` with `amqp:resource-limit-exceeded` and the connection ends. | `Amqp1ContainerConnectionTests.ServeAsync_SilentPeer_IsClosedAfterTheContainersIdleTimeout` |
| Amqp1:Core-2.8.15-1 | MUST | A performative missing a mandatory field, or with a field of the wrong type, closes the connection with `amqp:decode-error`. | `Amqp1ContainerConnectionTests.ServeAsync_UnreadableFirstFrame_IsAnsweredWithOpenThenClose`, `Amqp1ContainerConnectionTests.ServeAsync_UndecodableFrameAfterOpen_ClosesWithItsCondition` |

## Sessions (Part 2 sections 2.5, 2.7.2, 2.7.8)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Core-2.5.1-1 | MUST | A `begin` without `remote-channel` is answered with `begin` carrying `remote-channel`, `next-outgoing-id` 0, windows of 2147483647 and `handle-max` 255, on the same channel when free. | `Amqp1ContainerConnectionTests.ServeAsync_BeginAndAttachAsSender_IsAnsweredWithAReceivingLinkToTheQueue`, `Amqp1ContainerConnectionTests.ServeAsync_BeginOnAChannelAboveTheClientsChannelMax_IsAnsweredOnTheLowestFreeChannel` |
| Amqp1:Core-2.5.1-2 | MUST | A `begin` on a channel above `channel-max` or already in use, or a frame other than `begin` on a channel with no session, closes the connection with `amqp:connection:framing-error`. | `Amqp1ContainerConnectionTests.ServeAsync_BadBegin_ClosesWithFramingError`, `Amqp1ContainerConnectionTests.ServeAsync_BeginWithNoChannelFree_ClosesWithFramingError`, `Amqp1ContainerConnectionTests.ServeAsync_FrameOnAChannelWithNoSession_ClosesWithFramingError` |
| Amqp1:Core-2.5.1-3 | MUST | A client `end` is answered with `end`, and the session's links end with it. | `Amqp1ContainerConnectionTests.ServeAsync_End_IsAnsweredWithEndAndFreesTheChannel` |
| Amqp1:Core-2.5.6-1 | MUST NOT | The container writes no more transfers than the client's session `incoming-window` allows, and resumes on the client's `flow`. | BL-513 |

## Links and nodes (Part 2 sections 2.6.3 to 2.6.6, 2.7.3, 2.7.7; Part 3 section 3.5)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Core-2.6.3-1 | MUST | An `attach` is answered with `attach` in the opposite role, the same name, a handle of the container's, the address echoed, `initial-delivery-count` 0 when the container sends, and `max-message-size` `--max-filesize` when it receives. | `Amqp1ContainerConnectionTests.ServeAsync_BeginAndAttachAsSender_IsAnsweredWithAReceivingLinkToTheQueue`, `Amqp1ContainerConnectionTests.ServeAsync_AttachAsReceiver_IsAnsweredWithASendingLinkFromTheNode`, `Amqp1ContainerConnectionTests.ServeAsync_SecondLink_GetsTheNextFreeHandle` |
| Amqp1:Core-2.6.3-2 | MUST | A refused link is answered with an `attach` whose source (or target) is null, then a `detach` with `closed` true and the error. | `Amqp1ContainerConnectionTests.ServeAsync_AttachRefusedByTheDirectory_IsAnsweredWithANullTerminusThenAClosingDetach` |
| Amqp1:Core-2.7.3-1 | MUST | An `attach` on a handle in use, or above `handle-max`, ends the session with `amqp:session:handle-in-use`. | `Amqp1ContainerConnectionTests.ServeAsync_AttachWithABadHandle_EndsTheSessionWithHandleInUse` |
| Amqp1:Core-2.7.5-1 | MUST | A `flow`, `transfer`, `disposition` or `detach` on a handle not attached ends the session with `amqp:session:unattached-handle`. | `Amqp1ContainerConnectionTests.ServeAsync_TransferOnAnUnattachedHandle_EndsTheSessionWithUnattachedHandle`, `Amqp1ContainerConnectionTests.ServeAsync_FlowOnAnUnattachedHandle_EndsTheSessionWithUnattachedHandle`, `Amqp1ContainerConnectionTests.ServeAsync_DetachOfAnUnattachedHandle_EndsTheSessionWithUnattachedHandle`; `disposition` names delivery ids, not a handle, and is BL-513's |
| Amqp1:Core-2.6.6-1 | MUST | A client `detach` is answered with `detach` carrying the same `closed`; the link ends either way, and its unsettled queue deliveries return to the queue. | `Amqp1ContainerConnectionTests.ServeAsync_Detach_IsAnsweredWithADetachOfTheSameClosed`; returning unsettled deliveries is BL-513's |
| Amqp1:Surl-Nodes-1 | MUST | An address loses one leading `/`; `topic/<t>` is topic or filter `<t>`, `queue/<q>` queue `<q>`, and any other non-empty address the queue of that name. | `Amqp1NodeAddressTests` |
| Amqp1:Surl-Nodes-2 | MUST | A queue not yet on the bus is declared durable, not exclusive, not auto-deleted, by the first attach naming it. | BL-513 (its bus-backed `IAmqp1NodeDirectory` declares the queue) |
| Amqp1:Messaging-3.5.3-1 | MUST | A source or target with `dynamic` true gets a new queue, exclusive and auto-deleted, its address returned as `/queue/surl-dynamic-<hex>`. | `Amqp1ContainerConnectionTests.ServeAsync_AttachAsReceiverForADynamicNode_IsAnsweredWithTheNodesAddress`, `Amqp1ContainerConnectionTests.ServeAsync_AttachAsSenderForADynamicNode_IsAnsweredWithTheNodesAddress`; the queue's flags are BL-513's `IAmqp1NodeDirectory` |
| Amqp1:Surl-Nodes-3 | MUST | No address, an address that maps to no valid name, or a wildcard in a target is refused with `amqp:not-found`. | `Amqp1ContainerConnectionTests.ServeAsync_AttachAsSenderToAnAddressNamingNoNode_IsRefusedWithNotFound` |
| Amqp1:Surl-Nodes-4 | MUST | An attach topic access denies is refused with `amqp:unauthorized-access`, the same whether or not the node exists. | BL-513 |
| Amqp1:Surl-Nodes-5 | MUST | An attach the bus refuses (`TooManyQueues`, `TooManySubscriptions`) is refused with `amqp:resource-limit-exceeded`. | BL-513 |
| Amqp1:Surl-Nodes-6 | MUST | A `coordinator` target is refused with `amqp:not-implemented` until BL-707's decision is built. | `Amqp1ContainerConnectionTests.ServeAsync_AttachToACoordinator_IsRefusedWithNotImplemented` |
| Amqp1:Messaging-3.5.3-2 | MUST | The container's source states `distribution-mode` `move` for a queue and `copy` for a topic, `durable` `none` for a topic, and leaves out a `filter` it does not apply. | `Amqp1ContainerConnectionTests.ServeAsync_AttachAsReceiver_IsAnsweredWithASendingLinkFromTheNode` |
| Amqp1:Core-2.6.13-1 | MUST | A client's `unsettled` map is ignored and the container's attach carries none: links are not resumed. | `Amqp1ContainerConnectionTests.ServeAsync_AttachAsReceiver_IsAnsweredWithASendingLinkFromTheNode` |

## Flow, transfer and settlement (Part 2 sections 2.6.7, 2.6.12, 2.6.14, 2.7.4 to 2.7.6, 2.8.2, 2.8.3)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Core-2.6.7-1 | MUST | Receiving, the container grants link credit 100 right after its `attach` and raises it back to 100 whenever it falls to 50. | BL-513 |
| Amqp1:Core-2.6.7-2 | MUST | A transfer beyond the credit given detaches the link with `amqp:link:transfer-limit-exceeded`. | BL-513 |
| Amqp1:Core-2.6.7-3 | MUST NOT | Sending, the container writes no delivery while the client's link credit is 0, and advances `delivery-count` per delivery. | BL-513 |
| Amqp1:Core-2.6.7-4 | MUST | A `flow` with `drain` true makes the sending container use up the credit, set it to 0 with `delivery-count` advanced and answer with a `flow`; `echo` true is answered with a `flow`. | BL-513 |
| Amqp1:Core-2.6.12-1 | MUST | Each delivery's first transfer carries the handle, a `delivery-id` and a `delivery-tag` unique among the link's unsettled deliveries; `message-format` 0. | BL-513 |
| Amqp1:Core-2.6.14-1 | MUST | A message longer than a frame is sent and received in several transfers with `more` set on all but the last; a continuation with another `delivery-id` or `delivery-tag` detaches the link with `amqp:invalid-field`. | BL-513 |
| Amqp1:Core-2.7.5-2 | MUST | A transfer with `aborted` true discards the delivery: nothing stored, nothing settled. | BL-513 |
| Amqp1:Core-2.6.12-2 | MUST | Receiving, the container settles each unsettled delivery with a `disposition` (role receiver, `settled` true, the state) and sends none for a pre-settled one. | BL-513 |
| Amqp1:Core-2.8.3-1 | MUST | The container's receiving `rcv-settle-mode` is `first`, whatever the client asked. | BL-513 |
| Amqp1:Core-2.8.2-1 | MUST | Sending, the container honours the client's `snd-settle-mode`: pre-settled for `settled`, unsettled for `unsettled` and `mixed`. | BL-513 |
| Amqp1:Core-2.8.3-2 | MUST | Sending to a client receiver in `rcv-settle-mode` `second`, the container answers the client's unsettled `disposition` with a settling one. | BL-513 |
| Amqp1:Core-2.7.6-1 | MUST | A client `disposition` over a range `first` to `last` applies to every delivery in it. | BL-513 |

## Messages and delivery states (Part 3 sections 3.2 and 3.4)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Messaging-3.2-1 | MUST | A received message decodes as optional header, delivery annotations, message annotations, properties and application properties, then `data` sections, one `amqp-sequence` or more, or one `amqp-value`, then an optional footer; anything else is `rejected` with `amqp:decode-error`. | BL-513 |
| Amqp1:Messaging-3.2-2 | MUST | The bare message reaches an `amqp1` receiver byte for byte, with message annotations and footer; delivery annotations are not passed on. | BL-513 |
| Amqp1:Messaging-3.2.1-1 | MUST | A redelivered message's header has `delivery-count` raised by one and `first-acquirer` false. | BL-513 |
| Amqp1:Surl-Message-1 | MUST | The bus payload is the `data` sections joined, a `binary` or `string` `amqp-value`'s content, or else the body sections' encoding; `ttl` and `absolute-expiry-time` become the expiry, `content-type`, `reply-to` and `correlation-id` are carried, and string `application-properties` become headers. | BL-513 |
| Amqp1:Surl-Message-2 | MUST | A bus message from another protocol reaches an `amqp1` receiver as `properties` (`to`, `creation-time`, and what it has), string `application-properties` and one `data` section holding the payload exactly. | BL-513 |
| Amqp1:Messaging-3.4.2-1 | MUST | A message the bus stores is settled `accepted`; an unsettled one is published at QoS 1, a pre-settled one at QoS 0. | BL-513 |
| Amqp1:Messaging-3.4.3-1 | MUST | A message the bus refuses is `rejected` with `amqp:resource-limit-exceeded`, one topic access denies with `amqp:unauthorized-access`, and one with `message-format` other than 0 with `amqp:not-implemented`. | BL-513 |
| Amqp1:Messaging-3.4.2-2 | MUST | A client's `accepted` acknowledges a queue delivery; `rejected` drops it. | BL-513 |
| Amqp1:Messaging-3.4.4-1 | MUST | A client's `released` puts a queue delivery back for any consumer. | BL-513 |
| Amqp1:Messaging-3.4.5-1 | MUST | A client's `modified` puts a queue delivery back marked redelivered, and with `undeliverable-here` true never offers it to the same link again. | BL-513 |
| Amqp1:Surl-Delivery-1 | MUST | Deliveries from a topic wait on the link while it has no credit, at most 1000, a later one dropped for that link and noted; every outcome only settles them. | BL-513 |
| Amqp1:Surl-Delivery-2 | MUST | A queue link consumes with at most 1000 unacknowledged deliveries; those not sent when it ends go back to the queue. | BL-513 |

## Limits, TLS and storage (ADR-0006; ADR-0100 decisions 3.6 and 3.7; Part 5 section 5.2.1)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| Amqp1:Surl-Limits-1 | MUST | A message past `--max-filesize` detaches its link with `amqp:link:message-size-exceeded` without reading more of it into memory; partial deliveries of one connection past the same bound close it with `amqp:resource-limit-exceeded`. | BL-513 |
| Amqp1:Surl-Limits-2 | MUST | No finished `open` by `--head-timeout` after accept (headers, TLS and SASL included) closes the connection with no bytes. | BL-764 |
| Amqp1:Surl-Limits-3 | MUST | `--max-time` and the connection limits close a connection with `close` and `amqp:resource-limit-exceeded` once `open` has been exchanged, otherwise with no bytes. | BL-764 |
| Amqp1:Surl-Limits-4 | MUST | Every error `description` is fixed text from the container's table, never an address, path, account or exception, and no `info` map is sent. | `Amqp1ContainerConnectionTests.ServeAsync_AttachRefusedByTheDirectory_IsAnsweredWithANullTerminusThenAClosingDetach` and every closing test: each `description` is a constant of `Amqp1ContainerConnection` |
| Amqp1:Security-5.2.1-1 | MUST | `amqp1s` carries the same protocol inside implicit TLS (TLS 1.2 or 1.3) from the first byte. | n/a: the TLS wrapper is the composition's `ImplicitTlsSchemeServer` (ADR-0010), added by BL-514, not the container's |
| Amqp1:Surl-Storage-1 | MUST | Queues and topic logs, with each message's native form, persist under `--directory` in `.surl/bus`; the container writes no file of its own. | n/a: persistence is the bus's (ADR-0100 decision 3.7, BL-634) and the native form's BL-706's; the container only hands the bus its messages, which BL-513's tests show |
