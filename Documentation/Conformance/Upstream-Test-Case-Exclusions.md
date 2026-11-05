# Upstream test-case exclusions

Upstream test cases that fail against Surl only because the byte they verify is the upstream test
server's arbitrary choice - not the protocol's specification, not what curl needs (a banner's
wording, a fixed `MDTM` time) - and so are skipped as `test-server-choice`
([ADR-0080](../Planning/Decisions/ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md),
decision 5). Anything else that fails is a Surl defect until measurement shows otherwise, and
becomes a task.

One row per case. The list is empty: no case has been shown to verify an arbitrary choice. Reading
it into `UpstreamTestCaseSelectionRules.TestServerChoices` is BL-685's.

| Case | Byte verified | Upstream's choice | Surl's answer | Why a correct server need not send upstream's |
| --- | --- | --- | --- | --- |
