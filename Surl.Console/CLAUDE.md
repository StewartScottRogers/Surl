# Surl.Console

Phase 0 placeholder; Phase 1 makes it the composition root.

The `surl` executable (assembly name `surl`), published native ahead-of-time as a single
file. Phase 1 wires the option parser, the listeners and every protocol server here with
explicit dependency injection - never assembly scanning, which native AOT forbids. Until
then `Program.Main` only writes `surl: not implemented yet` to standard error and returns
`SurlExitCode.FailedInit` (2), the number upstream curl uses for `CURLE_FAILED_INIT`.

Keep this project thin: parsing belongs in `Surl.Cli`, serving in `Surl.Core`, each
protocol in its own library. Code here is wiring, tested in `Surl.Console.UnitTests`.
