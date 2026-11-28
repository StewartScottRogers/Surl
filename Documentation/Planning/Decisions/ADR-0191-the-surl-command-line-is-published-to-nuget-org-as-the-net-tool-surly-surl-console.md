# ADR-0191 — The surl command line is published to nuget.org as the .NET tool Surly.Surl.Console

- **Status:** Accepted
- **Date:** 2026-11-27
- **Decided by:** Claude under Stewart's delegation (BL-1983). Publishing `surl` as a .NET tool is Stewart's ask
  of 2026-11-27; the ID, the shape and the release steps are decided here.
- **Builds on:** [ADR-0098](ADR-0098-surl-api-and-surl-mcp-join-surl-as-products.md) (`surl`, `Surl.Api` and
  `Surl.Mcp` are Surl's products), [ADR-0103](ADR-0103-surl-mcp-its-tools-and-the-mcp-core-it-shares-with-the-extended-mcp-server.md)
  decision 7 (how `surl-mcp` is packed),
  [ADR-0187](ADR-0187-surl-publishes-its-nuget-packages-as-surly-surl-api-and-surly-surl-mcp.md) (package IDs
  are `Surly.Surl.<name>`) and [ADR-0085](ADR-0085-what-proves-the-native-surl-on-each-published-runtime-identifier.md)
  (what proves the native `surl`).
- **Acted on by:** BL-1984. Until it is Done and a tag is pushed, everything below is intent.

## Context

Stewart decided on 2026-11-27 that the Surly book follows the nuget.org packages, not the repositories: every
recipe must run for a reader with only .NET and nuget.org. `Surly.Surl.Api` and `Surly.Surl.Mcp` are on nuget.org
from 0.1.12. `surl` itself reaches a reader only as a native ahead-of-time (AOT) binary on a GitHub release, or
from a source build.

`release.yml` already pushes by trusted publishing: the `release` job has `id-token: write`, logs in with
`NuGet/login@v1` as the nuget.org user `StewartRogers86` (the creator of the policy, not the GitHub name), and
pushes each package with `--skip-duplicate` after it creates the GitHub release (BL-1795 wrote the steps;
BL-1797 settled the policy and the user name, and `v0.1.12` was the first release to push).

No project property makes `surl` a tool today. `dotnet pack Application/Surl.Console` on the tree as it stands
produces `surl.<version>.nupkg`, an ordinary library package that holds `surl.dll` and depends on ten
`Surl.*.UnitLibrary` packages, none of them published.

## Measured

With the .NET 10.0.404 SDK on Windows x64, 2026-11-27, `Surl.Console.csproj` edited for the measurement and then
restored. No Linux or macOS measurement was made.

| | One framework-dependent package | Runtime-specific packages (`ToolPackageRuntimeIdentifiers`) |
| --- | --- | --- |
| Packages for six platforms | 1 | 8: a pointer, six native, one `any` fallback |
| What a Windows x64 reader downloads | 3,691,215 bytes (6,513,436 with symbol and XML files left in) | 40,467,285 bytes as the SDK packs it: `surl.exe` 12,711,115 compressed, `surl.pdb` 27,753,879 compressed |
| `dotnet tool install` and `dnx` | both ran `surl --version` | both ran `surl --version` |
| What `dotnet tool install` puts on disk on Windows | `surl.exe` | `surl.cmd`, a batch file that starts the native binary |
| `surl --version`, median of 21 runs, two sets, machine busy with other builds | installed tool 503 ms and 890 ms | through `surl.cmd` 233 ms and 499 ms; native `surl.exe` directly 246 ms |

The runtime-specific shape also needs three kinds of pack: `dotnet pack` with `PublishAot` on for the pointer
(with it off for packing, that command failed with NETSDK1047), `dotnet pack -r <rid>` on a runner of each
operating system and architecture, and `dotnet pack -r any -p:PublishAot=false` for the fallback.

How `dnx` passes arguments to `surl` (measured with the framework-dependent package):

- `dnx` takes options it knows for itself. `dnx <package> --version`, with or without `surl` options before it,
  fails with `Required argument missing for option: '--version'.`, and `dnx <package> --help` shows `dnx`'s help.
  Its usage text also claims `-h`, `-v` and `--source`. After `--`, every argument reaches `surl`.
- In PowerShell, `dnx` is `dnx.ps1`, and `dnx <package> -- --version` fails the same way: the `--` does not get
  through. `dotnet dnx <package> -- <arguments>` works there.
- Run from `cmd.exe`, `dnx` (`dnx.cmd`) exited 0 when `surl` exited 2. `dotnet dnx`, `dnx.ps1` and the installed
  tool exited 2.

## Decision

1. **Package ID `Surly.Surl.Console`, command `surl`.** The ID follows ADR-0187's `Surly.Surl.<name>` for the
   project `Surl.Console`, as `Surly.Surl.Api` and `Surly.Surl.Mcp` follow theirs. The assembly stays `surl`.

2. **One framework-dependent tool package**, as `surl-mcp` is packed (ADR-0103 decision 7). It runs wherever
   .NET 10 does, including platforms the release builds no native binary for. `Application/Surl.Console/Surl.Console.csproj`
   gets:
   - `IsPackable` true, `PackAsTool` true, `ToolCommandName` `surl`, `PackageId` `Surly.Surl.Console`; the
     package type stays the default, a plain .NET tool;
   - `PublishAot` set only when `'$(PublishAot)' == '' And '$(_IsPacking)' != 'true'`, as `Surl.Mcp.csproj` has
     it, so `dotnet publish` still produces the native binary and `dotnet pack` does not;
   - `Authors`, `Description`, `PackageTags`, `PackageProjectUrl`, `RepositoryUrl`, `RepositoryType`,
     `PackageReadmeFile` (a new `Application/Surl.Console/README.md`) and `PackageLicenseFile` (`LICENSE.txt`), as
     the other two packages have them.

   No `ToolPackageRuntimeIdentifiers`, no `Version` element, no package reference, and no change to
   `Directory.Build.props`.

3. **The version is the tag without its `v`**, passed as `-p:Version`, as for the other two packages.

4. **`release.yml`.** The trusted publishing login, user and policy stay as they are.
   - `pack` job, after Surl.Mcp is packed:
     `dotnet pack Application/Surl.Console -c Release -p:Version="$VERSION" -p:PublishDocumentationFile=false -p:PublishReferencesDocumentationFiles=false -p:AllowedReferenceRelatedFileExtensions=.none -o nupkg`.
     The last three switches are the publish step's; they leave the libraries' symbol and XML files out.
   - `Check the packages` also fails unless the new package holds `README.md`, `LICENSE.txt`,
     `tools/net10.0/any/surl.dll` and `tools/net10.0/any/surl.runtimeconfig.json`, and its
     `DotnetToolSettings.xml` names the command `surl`.
   - A new step in the `pack` job proves the package runs: `dotnet tool install --tool-path "$RUNNER_TEMP/surl-tool" --source "$PWD/nupkg" Surly.Surl.Console --version "$VERSION"`,
     then the installed `surl --version` must exit 0 with a first line beginning `surl $VERSION (`.
   - The `nuget-packages` artifact (`nupkg/*.nupkg`) carries it with no change.
   - `release` job: `Surly.Surl.Console.$version.nupkg` joins the `sha256sum` line, so the GitHub release carries
     it beside the other two, and `Surly.Surl.Console` joins the push loop, with the same `--skip-duplicate`.
   - The workflow's header and job comments say three packages and fifteen files.
   - A dry run packs and checks the package and pushes nothing.

   The native publish legs, `Test-PublishedSurl.ps1`, the archives and the installers do not change.

5. **What the documents say** (BL-1984 writes them), from the first release after `v0.1.12`:

   ```
   dotnet tool install -g Surly.Surl.Console            # installs the surl command
   dotnet dnx Surly.Surl.Console -- <options> <url>     # or runs surl without that install
   ```

   - `surl --aihelp`'s overview (`AiHelpProse.OverviewAbout`) gives both commands and says that `surl`'s
     arguments go after `--`.
   - `README.md`, `DOWNLOAD.md` and the package's `README.md` give both, say they need the .NET 10 SDK, that a
     script or a test harness installs the tool rather than running it through `dnx` (the exit-code loss
     above), and that the GitHub release's native binary needs no .NET.
   - `Documentation/Product/Product-Overview.md` and `Documentation/Planning/Roadmap.md` stop saying `surl` is not
     a .NET tool.

## Options weighed

- **Runtime-specific packages with the native binaries and an `any` fallback.** Rejected. They start about a
  quarter to half a second sooner, but `surl` is a server started once and then left running, a .NET test suite
  that starts many has `Surl.Api`, and whoever wants the native binary has the release archive. Against that: 8 new
  package IDs instead of 1, packing spread over six native runners with the pointer pushed after the other seven,
  a larger download, and on Windows a `surl.cmd` that puts `cmd.exe` between a test harness and `surl`.
- **ID `Surly.Surl`.** Shorter to type, but outside `Surly.Surl.<name>`, which is Stewart's own rule (ADR-0187).
- **ID `Surly.Surl.Cli` or `Surly.Surl.Tool`.** `Surl.Cli` is the command-line parsing library, so `.Cli` would
  name the wrong project; there is no `Surl.Tool` project.

## Consequences

- A reader needs the .NET 10 runtime to run the tool; the SDK that installs it brings it.
- The tool runs just-in-time compiled, not the AOT binary ADR-0085 smoke-tests and runs conformance against. CI's
  tests run that same just-in-time code on Windows, Linux and macOS; the new `pack` step proves only that the
  package installs and starts.
- `Surly.Surl.Console` is a new ID. The trusted publishing policy pushed the first versions of the other two IDs,
  so no change on nuget.org is expected; the first tag with it is the proof.
- How Ctrl+C and SIGTERM reach `surl` through `dnx` or the installed tool was not measured.
