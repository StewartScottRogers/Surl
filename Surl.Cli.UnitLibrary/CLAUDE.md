# Surl.Cli.UnitLibrary

Phase 1.

Surl's command line: the option table, argument parsing, configuration files, usage text,
and the mapping from a bad command line to a `SurlExitCode`. `surl [options] <url>` names
what to listen on, the way `curl [options] <url>` names what to fetch. An option keeps
curl's name and meaning wherever a server-side meaning exists (`--cert`, `--key`,
`--cacert`, `-v`, `--trace`, `-w`); the table itself is a Phase 1 decision, recorded in
an ADR.

Never touch the console here; `Surl.Console` hands this library its arguments and writers.
