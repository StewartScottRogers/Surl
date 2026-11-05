<#
.SYNOPSIS
    Smoke-tests a published native-AOT surl: one native file, --version, one HTTP GET, and a
    documented stop, with no .NET runtime it could use (ADR-0085, decision 1).

.DESCRIPTION
    Fails on the first check that fails, naming it, and exits 1; exits 0 when every check passes.

      1. It is one native file. The publish directory holds surl (surl.exe on Windows) and at
         most its native symbol file (surl.pdb, surl.dbg or surl.dSYM). Any *.dll,
         surl.runtimeconfig.json or surl.deps.json fails it. The binary's own imports must not
         name the .NET host or runtime: ldd on Linux, otool -L on macOS, the PE import table on
         Windows.
      2. It starts and says what it is. surl --version exits 0 and its first line is exactly
         "surl <Version> (<RuntimeIdentifier>)".
      3. It serves. surl --directory <dir> http://127.0.0.1:0/ serves a generated 65,536-byte
         file (bytes 0 to 255 repeated) to the GET the pinned reference build of upstream curl
         sends for http://127.0.0.1:<port>/<file>, and the answer is 200 with that body.
      4. It stops as documented. On Linux and macOS SIGTERM makes surl exit 0 within 10
         seconds; on Windows Stop-Process ends it and it is gone.

    Checks 2 to 4 run with every DOTNET_* environment variable removed and every PATH entry
    that holds a dotnet executable removed. On Linux they run a second time inside a Docker
    container of ubuntu:22.04, the image release.yml's glibc floor names, with the publish
    directory mounted read-only: that image has no .NET at all.

.PARAMETER PublishDirectory
    The dotnet publish output directory holding surl.

.PARAMETER Version
    The version passed to dotnet publish (-p:Version).

.PARAMETER RuntimeIdentifier
    The runtime identifier the binary was published for, e.g. linux-arm64.

.PARAMETER SkipContainer
    On Linux, skip the second run inside the ubuntu:22.04 container (for a machine without
    Docker). release.yml never passes it.

.EXAMPLE
    ./Test-PublishedSurl.ps1 -PublishDirectory publish -Version 0.0.0-dev.12 -RuntimeIdentifier win-x64
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $RuntimeIdentifier,
    [switch] $SkipContainer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The request the pinned reference build (curl 8.21.0) sends for curl http://127.0.0.1:<port>/<file>,
# as Record-CurlExchange.ps1 recorded it in
# Foundation/Surl.Authentication.UnitTests/Fixtures/digest-non-ascii/request.bin, with the path
# and port as placeholders.
$CurlRequestTemplate = "GET /{0} HTTP/1.1`r`nHost: 127.0.0.1:{1}`r`nUser-Agent: curl/8.21.0`r`nAccept: */*`r`n`r`n"
$ServedFileName = 'smoke.bin'
$ServedFileLength = 65536
$ListeningTimeoutSeconds = 30
$StopTimeoutSeconds = 10
$ContainerImage = 'ubuntu:22.04'

function Fail([string] $check, [string] $why) {
    throw "$check failed: $why"
}

function Write-Passed([string] $check) {
    Write-Output "PASS  $check"
}

# --- Check 1: one native file --------------------------------------------------------------

function Get-SurlFileName {
    if ($IsWindows) { 'surl.exe' } else { 'surl' }
}

function Test-OneNativeFile([string] $directory) {
    $check = 'Check 1 (one native file)'
    $surlName = Get-SurlFileName
    $allowed = @($surlName, 'surl.pdb', 'surl.dbg', 'surl.dSYM')
    $entries = @(Get-ChildItem -LiteralPath $directory -Force | Select-Object -ExpandProperty Name)
    if ($entries -notcontains $surlName) {
        Fail $check "$directory holds no $surlName."
    }
    $unexpected = @($entries | Where-Object { $allowed -notcontains $_ })
    if ($unexpected.Count -gt 0) {
        Fail $check "$directory also holds $($unexpected -join ', '); a native publish leaves only $surlName and its symbol file."
    }

    $binary = Join-Path $directory $surlName
    $runtimeLibraries = @(Get-NativeImports $binary | Where-Object { $_ -match '(?i)hostfxr|hostpolicy|coreclr' })
    if ($runtimeLibraries.Count -gt 0) {
        Fail $check "$surlName imports $($runtimeLibraries -join ', '), a .NET host or runtime library."
    }
    Write-Passed "$check - $($entries -join ', ')"
}

function Get-NativeImports([string] $binary) {
    if ($IsLinux) {
        return @(& ldd $binary 2>&1 | ForEach-Object { "$_".Trim() })
    }
    if ($IsMacOS) {
        return @(& otool -L $binary | Select-Object -Skip 1 | ForEach-Object { "$_".Trim() })
    }
    return @(Get-PortableExecutableImports $binary)
}

function Get-PortableExecutableImports([string] $binary) {
    # Each 20-byte import descriptor holds the RVA of its DLL's name at offset 12; a zero ends the table.
    $image = [System.IO.File]::ReadAllBytes($binary)
    $reader = [System.Reflection.PortableExecutable.PEReader]::new([System.IO.MemoryStream]::new($image))
    try {
        $headers = $reader.PEHeaders
        $importTable = $headers.PEHeader.ImportTableDirectory
        if ($importTable.Size -eq 0) { return @() }
        $names = [System.Collections.Generic.List[string]]::new()
        $descriptor = Convert-RvaToOffset $headers $importTable.RelativeVirtualAddress
        while ($true) {
            $nameRva = [System.BitConverter]::ToInt32($image, $descriptor + 12)
            if ($nameRva -eq 0) { break }
            $names.Add((Read-AsciiString $image (Convert-RvaToOffset $headers $nameRva)))
            $descriptor += 20
        }
        return $names.ToArray()
    } finally {
        $reader.Dispose()
    }
}

function Convert-RvaToOffset($headers, [int] $rva) {
    foreach ($section in $headers.SectionHeaders) {
        if ($rva -ge $section.VirtualAddress -and $rva -lt ($section.VirtualAddress + [Math]::Max($section.VirtualSize, $section.SizeOfRawData))) {
            return $rva - $section.VirtualAddress + $section.PointerToRawData
        }
    }
    throw "RVA 0x$($rva.ToString('x')) is in no section."
}

function Read-AsciiString([byte[]] $image, [int] $offset) {
    $end = [Array]::IndexOf($image, [byte]0, $offset)
    return [System.Text.Encoding]::ASCII.GetString($image, $offset, $end - $offset)
}

# --- How surl is launched: on this machine with no .NET, or inside a container ----------------

function New-ScrubbedStartInfo([string] $fileName, [string[]] $arguments) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($fileName)
    foreach ($argument in $arguments) { $startInfo.ArgumentList.Add($argument) }
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($name in @($startInfo.Environment.Keys)) {
        if ($name -like 'DOTNET_*' -or $name -like 'COREHOST_*') { [void]$startInfo.Environment.Remove($name) }
    }
    $dotnetName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $pathKey = @($startInfo.Environment.Keys | Where-Object { $_ -ieq 'PATH' })[0]
    $kept = @($startInfo.Environment[$pathKey] -split [System.IO.Path]::PathSeparator | Where-Object {
            $_ -and -not (Test-Path -LiteralPath (Join-Path $_ $dotnetName) -PathType Leaf)
        })
    $startInfo.Environment[$pathKey] = $kept -join [System.IO.Path]::PathSeparator
    return $startInfo
}

# A launcher is where surl runs: on this machine with a scrubbed environment, or in a container.
function New-HostLauncher([string] $binary) {
    @{ Name = 'on this machine, with no DOTNET_* variable and no dotnet on PATH'; Binary = $binary; Docker = $null; Container = $null }
}

function New-ContainerLauncher([string] $publishDirectory) {
    # Resolved before PATH is scrubbed: the scrub may drop /usr/bin when it holds a dotnet link.
    $docker = (Get-Command docker -CommandType Application | Select-Object -First 1).Source
    # surl runs as this user inside the container, so the .surl folder it creates in the served
    # directory is this user's to remove afterwards, as it is on the host.
    $user = "$((& id -u).Trim()):$((& id -g).Trim())"
    @{ Name = "inside a $ContainerImage container with no .NET"; Docker = $docker; User = $user; PublishDirectory = $publishDirectory; Container = $null }
}

# Starts surl with its own arguments; a served directory among them is mounted into the container.
function Start-Surl($launcher, [string[]] $arguments, [string] $servedDirectory) {
    if (-not $launcher.Docker) {
        return [System.Diagnostics.Process]::Start((New-ScrubbedStartInfo $launcher.Binary $arguments))
    }
    $launcher.Container = "surl-smoke-$([guid]::NewGuid().ToString('n'))"
    $dockerArguments = @('run', '--rm', '--name', $launcher.Container, '--network', 'host',
        '--user', $launcher.User, '-v', "$($launcher.PublishDirectory):/surl:ro")
    $inside = @($arguments)
    if ($servedDirectory) {
        # Writable: --directory keeps its lock in <path>/.surl/ and exits 23 on a read-only
        # directory (`surl --aihelp content`).
        $dockerArguments += @('-v', "$($servedDirectory):/served")
        $inside = @($inside | ForEach-Object { if ($_ -eq $servedDirectory) { '/served' } else { $_ } })
    }
    $dockerArguments += @($ContainerImage, '/surl/surl') + $inside
    return [System.Diagnostics.Process]::Start((New-ScrubbedStartInfo $launcher.Docker $dockerArguments))
}

# Tells surl to stop as its documentation says: SIGTERM on Linux and macOS, Stop-Process on Windows.
function Stop-Surl($launcher, $process) {
    if ($launcher.Docker) {
        & $launcher.Docker kill --signal TERM $launcher.Container | Out-Null
    } elseif ($IsWindows) {
        Stop-Process -Id $process.Id -Force
    } else {
        & (Get-Command kill -CommandType Application | Select-Object -First 1).Source -TERM $process.Id
    }
}

# Removes a container that outlived its test; nothing on this machine.
function Remove-SurlContainer($launcher) {
    if ($launcher.Docker -and $launcher.Container) { & $launcher.Docker rm -f $launcher.Container 2>&1 | Out-Null }
}

# --- Check 2: --version --------------------------------------------------------------------

function Test-VersionLine($launcher) {
    $check = "Check 2 (--version, $($launcher.Name))"
    $process = Start-Surl $launcher @('--version') $null
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(120000)) {
        $process.Kill($true)
        Fail $check 'surl --version did not exit within 120 seconds.'
    }
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) {
        Fail $check "surl --version exited $($process.ExitCode). stderr: $stderr"
    }
    $firstLine = ($stdout -split "`r?`n")[0]
    $expected = "surl $Version ($RuntimeIdentifier)"
    if ($firstLine -cne $expected) {
        Fail $check "its first line is '$firstLine', not '$expected'."
    }
    Write-Passed "$check - $firstLine"
}

# --- Checks 3 and 4: serve one file, then stop ------------------------------------------------

function New-ServedDirectory {
    $directory = Join-Path ([System.IO.Path]::GetTempPath()) "surl-smoke-$([guid]::NewGuid().ToString('n'))"
    New-Item -ItemType Directory -Path $directory | Out-Null
    $content = [byte[]]::new($ServedFileLength)
    for ($i = 0; $i -lt $content.Length; $i++) { $content[$i] = [byte]($i % 256) }
    [System.IO.File]::WriteAllBytes((Join-Path $directory $ServedFileName), $content)
    if (-not $IsWindows) { & chmod -R a+rX $directory }
    return [pscustomobject]@{ Path = $directory; Content = $content }
}

function Wait-ListeningLine($process, [string] $check) {
    $deadline = [DateTime]::UtcNow.AddSeconds($ListeningTimeoutSeconds)
    while ($true) {
        $remaining = $deadline - [DateTime]::UtcNow
        if ($remaining -le [TimeSpan]::Zero) {
            Fail $check "surl printed no 'Listening on' line within $ListeningTimeoutSeconds seconds."
        }
        $lineTask = $process.StandardOutput.ReadLineAsync()
        if (-not $lineTask.Wait($remaining)) {
            Fail $check "surl printed no 'Listening on' line within $ListeningTimeoutSeconds seconds."
        }
        $line = $lineTask.Result
        if ($null -eq $line) {
            $process.WaitForExit()
            Fail $check "surl exited $($process.ExitCode) before it listened. stderr: $($process.StandardError.ReadToEnd())"
        }
        if ($line -match '^Listening on (http://127\.0\.0\.1:(\d+)/)') {
            return [int]$Matches[2]
        }
    }
}

function Read-HttpResponse([System.Net.Sockets.NetworkStream] $stream) {
    $buffer = [System.IO.MemoryStream]::new()
    $chunk = [byte[]]::new(16384)
    $stream.ReadTimeout = 30000
    while (($read = $stream.Read($chunk, 0, $chunk.Length)) -gt 0) {
        $buffer.Write($chunk, 0, $read)
        $bytes = $buffer.ToArray()
        $headerEnd = Find-HeaderEnd $bytes
        if ($headerEnd -lt 0) { continue }
        $head = [System.Text.Encoding]::ASCII.GetString($bytes, 0, $headerEnd)
        if ($head -notmatch '(?im)^Content-Length:\s*(\d+)\s*$') {
            return [pscustomobject]@{ Head = $head; Body = $null }
        }
        $length = [int]$Matches[1]
        $bodyStart = $headerEnd + 4
        if ($bytes.Length - $bodyStart -ge $length) {
            $body = [byte[]]::new($length)
            [Array]::Copy($bytes, $bodyStart, $body, 0, $length)
            return [pscustomobject]@{ Head = $head; Body = $body }
        }
    }
    return [pscustomobject]@{ Head = [System.Text.Encoding]::ASCII.GetString($buffer.ToArray()); Body = $null }
}

function Find-HeaderEnd([byte[]] $bytes) {
    for ($i = 0; $i + 3 -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 13 -and $bytes[$i + 1] -eq 10 -and $bytes[$i + 2] -eq 13 -and $bytes[$i + 3] -eq 10) { return $i }
    }
    return -1
}

function Test-ServeAndStop($launcher, $served) {
    $serveCheck = "Check 3 (serves a file, $($launcher.Name))"
    $stopCheck = "Check 4 (stops as documented, $($launcher.Name))"
    $process = Start-Surl $launcher @('--directory', $served.Path, 'http://127.0.0.1:0/') $served.Path
    $stderrTask = $null
    try {
        $port = Wait-ListeningLine $process $serveCheck
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $drain = $process.StandardOutput.ReadToEndAsync()

        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $client.Connect('127.0.0.1', $port)
            $stream = $client.GetStream()
            $request = [System.Text.Encoding]::ASCII.GetBytes(($CurlRequestTemplate -f $ServedFileName, $port))
            $stream.Write($request, 0, $request.Length)
            $response = Read-HttpResponse $stream
        } finally {
            $client.Dispose()
        }
        $statusLine = ($response.Head -split "`r`n")[0]
        if ($statusLine -notmatch '^HTTP/1\.1 200 ') {
            Fail $serveCheck "the status line is '$statusLine', not 200."
        }
        if ($null -eq $response.Body -or [Convert]::ToBase64String($response.Body) -cne [Convert]::ToBase64String($served.Content)) {
            $got = if ($null -eq $response.Body) { 'no Content-Length body' } else { "$($response.Body.Length) bytes that differ" }
            Fail $serveCheck "the body is $got; expected the $ServedFileLength-byte file."
        }
        Write-Passed "$serveCheck - $statusLine, $ServedFileLength bytes match"

        Stop-Surl $launcher $process
        if (-not $process.WaitForExit($StopTimeoutSeconds * 1000)) {
            Fail $stopCheck "surl was still running $StopTimeoutSeconds seconds after it was told to stop."
        }
        $process.WaitForExit()
        if ($IsWindows -and -not $launcher.Docker) {
            if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
                Fail $stopCheck "process $($process.Id) is still there after Stop-Process."
            }
            Write-Passed "$stopCheck - Stop-Process ended it"
        } else {
            if ($process.ExitCode -ne 0) {
                $stderr = if ($stderrTask) { $stderrTask.GetAwaiter().GetResult() } else { '' }
                Fail $stopCheck "surl exited $($process.ExitCode) after SIGTERM, not 0. stderr: $stderr"
            }
            Write-Passed "$stopCheck - SIGTERM, exit 0"
        }
    } finally {
        if (-not $process.HasExited) { $process.Kill($true) }
        Remove-SurlContainer $launcher
        $process.Dispose()
    }
}

# --- The run ---------------------------------------------------------------------------------

try {
    $publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
    Test-OneNativeFile $publish
    $binary = Join-Path $publish (Get-SurlFileName)
    if (-not $IsWindows) { & chmod 755 $binary }

    $launchers = @(New-HostLauncher $binary)
    if ($IsLinux -and -not $SkipContainer) {
        $launchers += New-ContainerLauncher $publish
    }
    foreach ($launcher in $launchers) {
        Test-VersionLine $launcher
        $served = New-ServedDirectory
        try {
            Test-ServeAndStop $launcher $served
        } finally {
            Remove-Item -LiteralPath $served.Path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Output "The published surl for $RuntimeIdentifier passed every check."
    exit 0
} catch {
    Write-Output "::error::$($_.Exception.Message)"
    Write-Output "FAIL  $($_.Exception.Message)"
    exit 1
}
