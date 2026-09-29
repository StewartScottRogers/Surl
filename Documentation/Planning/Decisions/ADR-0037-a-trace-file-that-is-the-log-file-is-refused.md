# ADR-0037 — A trace file that is the log file is refused

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-128.

## Context

[ADR-0033](ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md) opens the
`--log-file` appended (section 6) and the `--trace` or `--trace-ascii` file truncated
(section 4), each on its own, and says nothing about the two naming one file. When they do -
`--log-file x --trace x`, or two spellings of one full path such as `x` and `./x` - the
outcome differed by platform: Windows refused the second open with a sharing violation, so
surl ended with `CouldNotWriteFile` (23) and the operating system's message; Linux and macOS
opened both, the trace's truncate emptied the log, and the two writers overwrote each other's
bytes, silently losing log lines.

Upstream curl has no `--log-file`, so there are no upstream bytes to measure; the decision
follows the standing rules alone - the simplest behaviour, the same on every platform.

Two candidates were weighed:

1. **Refuse** before any listener binds.
2. **Share one writer** between the log and the trace.

## Decision

**Refuse.** When `--log-file` and `--trace` or `--trace-ascii` are both in effect, neither is
`-` (stdout), and `Path.GetFullPath` of the two paths compare equal ignoring case
(`StringComparison.OrdinalIgnoreCase`), surl:

- opens the `--log-file` as before, then refuses the trace file without opening it, closes the
  log file again, and ends before any listener binds with `CouldNotWriteFile` (23) - the same
  point and the same exit code as a trace file that cannot be opened (ADR-0033, section 6);
- writes, on stderr, `surl: (23) Could not open <path> for <option>: --log-file names the same file`,
  `<path>` the trace file as given and `<option>` `--trace` or `--trace-ascii`. The message
  follows ADR-0033 section 1's rules (hidden by `-s` alone), as every `(23)` does.

The check lives in `Surl.Console/LogStreams.cs` (`LogStreams.Open`). A trace file is only in
effect at the trace level, so `--log-file x --trace x -v` (the last level option wins, and it is
verbose) opens the log file alone and is not refused.

`-` names stdout, not a file: `--log-file - --trace -` writes both to stdout, as before.

**Why case is ignored everywhere.** Windows and macOS's default volumes compare names without
case, so `surl.log` and `SURL.LOG` are one file there; comparing ordinally would let macOS
truncate the log. Ignoring case on every platform gives one rule on all three, at the cost of
refusing, on a case-sensitive Linux file system, a log and a trace that differ only in case -
a pairing nobody needs, and a refusal that names the fix.

**Why not share one writer.** The two files have opposite lives: the log is appended so it
outlives a restart, the trace truncated per run. A shared file would have to pick one mode and
break the other's promise, and would interleave the dump into the log, which ADR-0033 section 6
keeps apart ("not a trace file's dump"). An operator who wants the dump in the log file already
has it: `--log-file x --log-level trace` dumps to the log stream.

**Why not 2 (`FailedInit`).** The pairing is only wrong once the paths are resolved against the
file system's working directory, at the point surl opens its files, where every other file
failure is 23; one exit code for "surl cannot write the file it was told to" keeps ADR-0033's
single rule.

## Alternatives considered

- **Share one writer**, rejected above.
- **Compare ordinally on Linux and ignoring case elsewhere** - rejected: macOS volumes may be
  case-sensitive or not, so the platform does not decide the answer, and the rule would differ
  by platform, which is the defect being fixed.
- **Compare file identities** (inode, file index) after opening - rejected: the trace's
  `FileMode.Create` truncates before an identity can be compared, and it needs platform calls
  outside the base class library's portable surface. Hard links and symbolic links to one file
  under two full paths stay the operator's own doing.

## Consequences

- The same command line gets the same answer on Windows, Linux and macOS, and no log line is
  lost silently.
- `Surl.Console.UnitTests/CommandLineRunnerLogTests.cs` pins the refusal through the
  `openLogFile` seam for the identical spelling, `./`, `..` and case spellings, and pins two
  different files still opening separately.
