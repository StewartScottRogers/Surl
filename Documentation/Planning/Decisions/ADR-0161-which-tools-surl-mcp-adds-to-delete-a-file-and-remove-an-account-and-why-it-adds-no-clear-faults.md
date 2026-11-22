# ADR-0161 — Which tools `Surl.Mcp` adds to delete a file and remove an account, and why it adds no "clear faults"

- **Status:** Accepted
- **Date:** 2026-11-15
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-15, in BL-1154.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4, 7 and 8,
  [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md) decisions 3 and 4.
- **Amends:** ADR-0124 decision 8 (two more tools).
- **Acted on by:** BL-1175.

## Context

A test that seeds a file or an account must be able to undo it without a full `reset_server`. `Surl.Api` has
`SurlServer.Files.DeleteAsync` and `SurlServer.Accounts.Remove`; the control endpoint has
`DELETE /v1/files/{path}` and `DELETE /v1/accounts/{user}`. `Surl.Mcp` has `put_file` and `add_account` but nothing
to undo them. `Surl.Api` also has `Faults.Clear()`; the control endpoint and `Surl.Mcp` have only remove-one
(`DELETE /v1/faults/{id}`, `remove_fault`).

## Decision

### 1. Two new tools

Each takes `server_id` (required, as every tool), has `additionalProperties: false`, and returns a result whose
`structuredContent` equals its text block (ADR-0103 decision 3). An unknown server ID is `isError: true`.
ADR-0103 decision 4 applies unchanged: no local path is read and no password is echoed.

| Tool | Other properties | Result | Error results (`isError: true`) |
| --- | --- | --- | --- |
| `delete_file` | `path` (string, `minLength` 1, a request path such as `/reports/a.txt`) | `{"path"}` | `no file at <path>` when nothing, a folder or a hidden path is there (`SurlSeedRefusedException`, result `Absent`); a path no store accepts is refused with the same text `put_file` uses |
| `remove_account` | `user` (string, no `minLength`: an empty `user` removes the Bearer-token account, ADR-0132 amendment 2) | `{"user"}` - never anything about the password | `server <id> has no account <user>` when `Accounts.Remove` returns `false` |

- `delete_file` calls `SurlServer.Files.DeleteAsync` and so is the test's own delete: no client permission is needed,
  the fault plan is not consulted, and a `StoreChangedEvent` with `StoreChange.Deleted` is published.
- `remove_account` calls `SurlServer.Accounts.Remove`, which removes an account given at start too and publishes
  `StoreChange.AccountRemoved`.
- Neither tool needs a special case for a disk store: a `--directory` content store is a real folder and the delete
  applies to it as a client's delete would; a refusal from any store is the `isError` text above.
- Neither result is a failure when the thing was already gone beyond what the error rows say: the error is the
  answer, as in the control endpoint's `404`.

### 2. Place in `tools/list`

Each tool follows the tool it undoes, so related tools stay together and existing positions of the earlier tools do
not move: `put_file`, **`delete_file`**, `publish_message`, `add_account`, **`remove_account`**, `reset_server`.
Registration in `Surl.Mcp`'s `Program.cs` follows that order.

### 3. No "clear faults" tool or route

Not added to `Surl.Mcp` or the control endpoint.

- `list_faults` then `remove_fault` per rule gives the same result in a bounded number of calls, and an agent
  sees what it removes. A clear-all invites removing a rule the test did not add, a start rule included, with no
  record of which.
- `reset_server` (ADR-0124 decision 3) is the whole-instance undo a test between cases wants; a fault plan sits
  outside it by design, so a test removes the rules it added by ID.
- `Surl.Api`'s `Faults.Clear()` stays: an in-process test owns its plan and a one-line clear costs nothing there. The
  hosts differ because an out-of-process caller has a round trip per call and no type to protect, so it is held to
  explicit removals.
- If a later need shows up, a clear is an addition (a new route or tool, never a changed one) and a new ADR.

## Alternatives considered

- **`clear_faults` on both hosts.** Rejected for the reasons in decision 3.
- **One `delete` tool with a `kind`.** Rejected: ADR-0103 gives each tool one job and one closed schema.
- **A silent success when nothing is there.** Rejected: the Api and the control endpoint both report absence.

## Consequences

- `Surl.Mcp.UnitLibrary` gains `DeleteFileTool.cs` and `RemoveAccountTool.cs` and their tests, and `--aihelp` facts
  and the package README name them (BL-1175).
- ADR-0124 decision 8's table gains two rows by this amendment.
