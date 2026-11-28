# Download

Surl's releases are at https://github.com/StewartScottRogers/Surl/releases. The latest on
2026-11-27 is [v0.1.12](https://github.com/StewartScottRogers/Surl/releases/tag/v0.1.12).
`surl` answers `http`, `https` and the other schemes the README lists under
[What this is](README.md#what-this-is), and the
[extended protocols](README.md#extended-protocols).

## What a release holds

Pushing a `v*` tag runs `.github/workflows/release.yml`, which publishes the native
ahead-of-time `surl` and `surl-mcp` binaries for six platforms and attaches their archives,
with a `SHA256SUMS` file, to a GitHub release:

| Platform | `surl` archive | `surl-mcp` archive |
| --- | --- | --- |
| Windows x64 | `surl-win-x64.zip` | `surl-mcp-win-x64.zip` |
| Windows Arm64 | `surl-win-arm64.zip` | `surl-mcp-win-arm64.zip` |
| Linux x64 (glibc 2.35 or newer) | `surl-linux-x64.tar.gz` | `surl-mcp-linux-x64.tar.gz` |
| Linux Arm64 (glibc 2.35 or newer) | `surl-linux-arm64.tar.gz` | `surl-mcp-linux-arm64.tar.gz` |
| macOS x64 | `surl-osx-x64.tar.gz` | `surl-mcp-osx-x64.tar.gz` |
| macOS Arm64 | `surl-osx-arm64.tar.gz` | `surl-mcp-osx-arm64.tar.gz` |

`surl-mcp` is Surl's MCP server: an MCP client - an AI agent's host - starts it as a stdio
server, and its tools let the agent start, list and stop Surl protocol servers and see what
their clients sent. Each archive holds the one executable, `LICENSE.txt` and `README.md`.

From `v0.1.10`, the first release after BL-1789, a release also carries the two NuGet
packages. `<version>` is the tag without its `v`:

| NuGet package file | Holds |
| --- | --- |
| `Surly.Surl.Api.<version>.nupkg` | `Surl.Api`, which starts and stops Surl servers inside a .NET test suite |
| `Surly.Surl.Mcp.<version>.nupkg` | `surl-mcp` as a framework-dependent .NET tool package of type `McpServer` |

`SHA256SUMS` in such a release lists both `.nupkg` files as well as the twelve archives.

From the first release after `v0.1.12`, a release is to carry a third package, and `SHA256SUMS` is
to list it too:

| NuGet package file | Holds |
| --- | --- |
| `Surly.Surl.Console.<version>.nupkg` | the `surl` command line as a framework-dependent .NET tool package, command `surl` |

## NuGet packages on nuget.org

`Surly.Surl.Api` and `Surly.Surl.Mcp` are on nuget.org from 0.1.12: the release job pushes
them there after it creates the GitHub release. Both need .NET 10:

```
dotnet add package Surly.Surl.Api         # adds Surl.Api to a test project
dotnet tool install -g Surly.Surl.Mcp     # installs the surl-mcp command
dnx Surly.Surl.Mcp                        # or runs surl-mcp without that install
```

For `v0.1.10` and `v0.1.11`, download the `.nupkg` files from the GitHub release.

### The surl command line

From the first release after `v0.1.12`, the release job is to push `Surly.Surl.Console` to
nuget.org as well; no release has pushed it yet (2026-11-27). It needs the .NET 10 SDK:

```
dotnet tool install -g Surly.Surl.Console            # installs the surl command
dotnet dnx Surly.Surl.Console -- <options> <url>     # or runs surl without that install
```

`surl`'s arguments go after `--`: `dnx` takes options such as `--version` and `--help` for itself.
A script or a test harness should install the tool rather than run it through `dnx`: run from
`cmd.exe`, `dnx` exited 0 when `surl` exited 2 (measured on Windows with the .NET 10.0.404 SDK).
The native `surl` binary in a GitHub release's `surl` archive, or from
[your own build](#build-it-yourself), needs no .NET.

## Installers

The installers in this repository fetch the `surl` archive for the machine they run on from
the newest release, pre-releases included, check it against the release's `SHA256SUMS`, and
put `surl` in a directory of its own (they fetch neither `surl-mcp` nor any NuGet package):

```sh
curl -fsSL https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.sh | sh    # Linux, macOS
```

```powershell
irm https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.ps1 | iex          # Windows
```

To install another release, set `SURL_VERSION` for `install.sh`, or run `install.ps1` with
`-Version`, naming the tag. Tags and releases are Stewart's call (`CLAUDE.md`).

## Build it yourself

```
dotnet publish Application/Surl.Console -c Release -r <rid> -o publish
```

`<rid>` is one of `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` or
`osx-arm64`, and native ahead-of-time compilation cannot cross operating systems, so build
on the one you target. On Windows it needs the MSVC linker from Visual Studio's "Desktop
development with C++" workload and
`C:\Program Files (x86)\Microsoft Visual Studio\Installer` on `PATH` for vswhere; on
Linux, `clang` and `zlib1g-dev`.
