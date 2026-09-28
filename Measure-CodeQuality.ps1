<#
.SYNOPSIS
    Measures line coverage, branch coverage, cyclomatic complexity and CRAP score for
    every production library in the Surl solution, and fails if any member is outside
    the thresholds.

.DESCRIPTION
    Everything here comes out of tooling the solution already has, because Surl takes
    no packages beyond MSTest (CLAUDE.md):

      * Coverage - Microsoft.Testing.Extensions.CodeCoverage, which the MSTest
        meta-package already pulls into every .UnitTests project. It is asked for
        Cobertura, whose <method> elements carry line-rate, branch-rate and a
        complexity attribute.
      * Complexity - that same Cobertura complexity attribute. The build itself is
        gated separately by the SDK's CA1502 analyzer at the threshold in
        CodeMetricsConfig.txt. CA1502 measures the source and Cobertura measures the
        compiled method, so on a method full of pattern matching the two can differ by
        one or two. CA1502 is the one that breaks the build; this is the one that
        feeds CRAP, because it arrives in the same file as the coverage it pairs with.
      * CRAP - produced by no tool at all, so it is computed here:
            CRAP(m) = comp(m)^2 * (1 - cov(m))^3 + comp(m)
        At full coverage the middle term vanishes and CRAP is just the complexity,
        which is why the default CRAP limit of 30 only ever bites on a method that is
        both complex and undertested.

    A library covered by more than one test project is merged line by line, taking the
    best hit count and the best condition count seen for each line, so a helper in
    Surl.Protocol.Abstractions.UnitLibrary exercised from another protocol's tests is
    credited for it.

.PARAMETER Library
    Assembly names to report on, wildcards allowed. Defaults to every production
    assembly - anything named *.UnitLibrary, plus Surl.Console. Surl.Console's
    assembly is named surl, and is reported and matched here as Surl.Console.

.PARAMETER SkipTestRun
    Reuse the Cobertura files already in ResultsDirectory instead of running tests.
    Without -ResultsDirectory these are the reports this checkout's last run left in
    its own default directory (see ResultsDirectory).

.PARAMETER ResultsDirectory
    Where the Cobertura reports are written and read. Unless -SkipTestRun is given,
    this directory is deleted and recreated before the tests run, and every
    *.cobertura.xml under it is merged into the report.

    Defaults to a directory that belongs to this checkout alone:
    $env:TEMP\SurlCodeQuality\<checkout folder name>-<first 16 hex digits of the
    SHA-256 of the checkout's full path>. Each git worktree (for example each dark
    factory lane under <repo>.lanes\lane-<n>) therefore gets its own directory, so two
    runs from different checkouts at the same time never delete or read each other's
    reports, and -SkipTestRun still finds the reports of this checkout's last run.
    Two runs from the same checkout at the same time still share it.

    An explicit value is used exactly as given.

.PARAMETER IncludeIntegration
    Run the integration tests too. Off by default, matching the fast test command in
    CLAUDE.md.

.PARAMETER ReportPath
    Also write the Markdown report to this file.

.PARAMETER JsonPath
    Also write the measurements as JSON - totals, one row per library, every failing
    member and every coverage exclusion - for the published coverage report that
    .github/coverage/make-coverage-report.cs renders.

.EXAMPLE
    .\Measure-CodeQuality.ps1

.EXAMPLE
    .\Measure-CodeQuality.ps1 -Library Surl.Protocol.Http.UnitLibrary -ReportPath report.md
#>
[CmdletBinding()]
param(
    [string[]] $Library,
    [switch] $SkipTestRun,
    [switch] $IncludeIntegration,
    [string] $ResultsDirectory,
    [double] $LineThreshold = 100,
    [double] $BranchThreshold = 100,
    [int] $ComplexityThreshold = 10,
    [double] $CrapThreshold = 30,
    [int] $MaxMissingLinesShown = 12,
    [string] $ReportPath,
    [string] $JsonPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = $PSScriptRoot

function Get-CheckoutResultsDirectory {
    param([string] $CheckoutRoot)
    $fullPath = [System.IO.Path]::GetFullPath($CheckoutRoot).TrimEnd([char] '\', [char] '/').ToLowerInvariant()
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($fullPath))
    } finally {
        $sha256.Dispose()
    }
    $hashText = -join ($hash[0..7] | ForEach-Object { $_.ToString('x2') })
    $checkoutName = Split-Path $fullPath -Leaf
    return Join-Path (Join-Path $env:TEMP 'SurlCodeQuality') "$checkoutName-$hashText"
}

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Get-CheckoutResultsDirectory -CheckoutRoot $repositoryRoot
}

# --- 1. Collect coverage ----------------------------------------------------------

if (-not $SkipTestRun) {
    if (Test-Path $ResultsDirectory) { Remove-Item $ResultsDirectory -Recurse -Force }
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

    $solution = Join-Path $repositoryRoot 'Surl.slnx'
    $testArguments = @(
        'test', $solution,
        '--collect:Code Coverage;Format=cobertura',
        '--results-directory', $ResultsDirectory
    )
    if (-not $IncludeIntegration) { $testArguments += @('--filter', 'TestCategory!=Integration') }

    Write-Host "Running tests with coverage into $ResultsDirectory ..." -ForegroundColor Cyan
    & dotnet @testArguments | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE. Fix the tests before measuring quality."
    }
}

$coverageFiles = @(Get-ChildItem -Path $ResultsDirectory -Filter '*.cobertura.xml' -Recurse -ErrorAction SilentlyContinue)
if ($coverageFiles.Count -eq 0) {
    throw "No Cobertura report found under $ResultsDirectory."
}

# --- 2. Merge every report into one table of production methods -------------------

function Test-IsProductionAssembly {
    param([string] $Name)

    if ($Name -like '*.UnitTests') { return $false }
    return ($Name -like '*.UnitLibrary') -or ($Name -eq 'Surl.Console')
}

# Surl.Console.csproj sets <AssemblyName>surl</AssemblyName>, which makes its
# Cobertura package "surl". Report it under its project name,
# so the table, the failing-member heading and -Library all see Surl.Console.
function Get-ReportedAssemblyName {
    param([string] $PackageName)

    if ($PackageName -ceq 'surl') { return 'Surl.Console' }
    return $PackageName
}

$methods = @{}

foreach ($file in $coverageFiles) {
    $document = New-Object System.Xml.XmlDocument
    $document.Load($file.FullName)

    foreach ($package in $document.SelectNodes('/coverage/packages/package')) {
        $assembly = Get-ReportedAssemblyName $package.GetAttribute('name')
        if (-not (Test-IsProductionAssembly $assembly)) { continue }

        foreach ($class in $package.SelectNodes('classes/class')) {
            $className = $class.GetAttribute('name')
            $sourceFile = $class.GetAttribute('filename')

            foreach ($method in $class.SelectNodes('methods/method')) {
                $member = $method.GetAttribute('name') + $method.GetAttribute('signature')
                $key = $assembly + '|' + $className + '|' + $member

                if (-not $methods.ContainsKey($key)) {
                    $methods[$key] = [pscustomobject]@{
                        Assembly   = $assembly
                        Class      = $className
                        Member     = $member
                        SourceFile = $sourceFile
                        Complexity = 0
                        Lines      = @{}
                    }
                }
                $entry = $methods[$key]

                # Complexity is a property of the code, not of the run, so every report
                # that saw this method agrees on it and the maximum is simply the value.
                $complexity = [int] $method.GetAttribute('complexity')
                if ($complexity -gt $entry.Complexity) { $entry.Complexity = $complexity }

                foreach ($line in $method.SelectNodes('lines/line')) {
                    $number = [int] $line.GetAttribute('number')
                    $hits = [int] $line.GetAttribute('hits')

                    $conditionsCovered = 0
                    $conditionsTotal = 0
                    $condition = $line.GetAttribute('condition-coverage')
                    if ($condition -match '\((\d+)/(\d+)\)') {
                        $conditionsCovered = [int] $Matches[1]
                        $conditionsTotal = [int] $Matches[2]
                    }

                    if (-not $entry.Lines.ContainsKey($number)) {
                        $entry.Lines[$number] = [pscustomobject]@{
                            Hits              = $hits
                            ConditionsCovered = $conditionsCovered
                            ConditionsTotal   = $conditionsTotal
                        }
                    }
                    else {
                        $existing = $entry.Lines[$number]
                        if ($hits -gt $existing.Hits) { $existing.Hits = $hits }
                        if ($conditionsCovered -gt $existing.ConditionsCovered) { $existing.ConditionsCovered = $conditionsCovered }
                        if ($conditionsTotal -gt $existing.ConditionsTotal) { $existing.ConditionsTotal = $conditionsTotal }
                    }
                }
            }
        }
    }
}

# --- 3. Score each method ---------------------------------------------------------

$scored = New-Object System.Collections.Generic.List[object]

foreach ($entry in $methods.Values) {
    $lineNumbers = @($entry.Lines.Keys)
    $lineTotal = $lineNumbers.Count
    if ($lineTotal -eq 0) { continue }

    $missing = @($lineNumbers | Where-Object { $entry.Lines[$_].Hits -eq 0 } | Sort-Object)
    $lineRate = ($lineTotal - $missing.Count) / $lineTotal

    $branchTotal = 0
    $branchCovered = 0
    foreach ($number in $lineNumbers) {
        $branchTotal += $entry.Lines[$number].ConditionsTotal
        $branchCovered += $entry.Lines[$number].ConditionsCovered
    }
    if ($branchTotal -eq 0) { $branchRate = 1.0 } else { $branchRate = $branchCovered / $branchTotal }

    $complexity = $entry.Complexity
    $crap = ($complexity * $complexity) * [math]::Pow(1 - $lineRate, 3) + $complexity

    $failures = New-Object System.Collections.Generic.List[string]
    if (($lineRate * 100) -lt $LineThreshold) { $failures.Add('line') }
    if (($branchRate * 100) -lt $BranchThreshold) { $failures.Add('branch') }
    if ($complexity -gt $ComplexityThreshold) { $failures.Add('complexity') }
    if ($crap -gt $CrapThreshold) { $failures.Add('crap') }

    $relativeFile = $entry.SourceFile
    if ($relativeFile -and $relativeFile.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $relativeFile = $relativeFile.Substring($repositoryRoot.Length).TrimStart('\', '/')
    }

    $scored.Add([pscustomobject]@{
            Assembly      = $entry.Assembly
            Class         = $entry.Class
            Member        = $entry.Member
            SourceFile    = $relativeFile
            FirstLine     = ($lineNumbers | Measure-Object -Minimum).Minimum
            LinesCovered  = $lineTotal - $missing.Count
            LinesTotal    = $lineTotal
            BranchCovered = $branchCovered
            BranchTotal   = $branchTotal
            LinePercent   = [math]::Round($lineRate * 100, 2)
            BranchPercent = [math]::Round($branchRate * 100, 2)
            Complexity    = $complexity
            Crap          = [math]::Round($crap, 2)
            MissingLines  = $missing
            Failures      = $failures
        })
}

# ToArray rather than @(...): Windows PowerShell 5.1 throws "Argument types do not
# match" when @() wraps a List[object] whose elements are PSCustomObjects.
$selected = $scored.ToArray()
if ($Library) {
    $selected = @($scored | Where-Object {
            $name = $_.Assembly
            @($Library | Where-Object { $name -like $_ }).Count -gt 0
        })
}

# --- 4. Report --------------------------------------------------------------------

$report = New-Object System.Collections.Generic.List[string]
$report.Add('# Surl code-quality audit')
$report.Add('')
$report.Add(('Measured {0}. Thresholds: line {1}%, branch {2}%, cyclomatic complexity {3}, CRAP {4}.' -f
    (Get-Date -Format 'yyyy-MM-dd HH:mm'), $LineThreshold, $BranchThreshold, $ComplexityThreshold, $CrapThreshold))
$report.Add('')

$assemblies = @($selected | Select-Object -ExpandProperty Assembly -Unique | Sort-Object)
$totalViolations = 0

$report.Add('## Libraries')
$report.Add('')
$report.Add('| Library | Line % | Branch % | Members | Failing | Worst CRAP |')
$report.Add('| --- | ---: | ---: | ---: | ---: | ---: |')

$jsonLibraries = New-Object System.Collections.Generic.List[object]
$jsonFailing = New-Object System.Collections.Generic.List[object]

foreach ($assembly in $assemblies) {
    $members = @($selected | Where-Object { $_.Assembly -eq $assembly })
    $failing = @($members | Where-Object { $_.Failures.Count -gt 0 })
    $totalViolations += $failing.Count

    # Weighted by line, not an average of per-member percentages: a one-line property
    # left uncovered should not cost as much as a forty-line method left uncovered.
    $linesTotal = ($members | Measure-Object -Property LinesTotal -Sum).Sum
    $linesCovered = ($members | Measure-Object -Property LinesCovered -Sum).Sum
    $branchTotal = ($members | Measure-Object -Property BranchTotal -Sum).Sum
    $branchCovered = ($members | Measure-Object -Property BranchCovered -Sum).Sum

    if ($linesTotal -eq 0) { $linePercent = 100 } else { $linePercent = [math]::Round(100 * $linesCovered / $linesTotal, 2) }
    if ($branchTotal -eq 0) { $branchPercent = 100 } else { $branchPercent = [math]::Round(100 * $branchCovered / $branchTotal, 2) }
    $worstCrap = ($members | Measure-Object -Property Crap -Maximum).Maximum

    $report.Add(('| {0} | {1} | {2} | {3} | {4} | {5} |' -f
            $assembly, $linePercent, $branchPercent, $members.Count, $failing.Count, $worstCrap))
    $jsonLibraries.Add([ordered]@{
            name = $assembly; linePercent = $linePercent; branchPercent = $branchPercent
            linesCovered = [int]$linesCovered; linesTotal = [int]$linesTotal
            branchesCovered = [int]$branchCovered; branchesTotal = [int]$branchTotal
            members = $members.Count; failingMembers = $failing.Count; worstCrap = $worstCrap
        })
}

$report.Add('')

foreach ($assembly in $assemblies) {
    $failing = @($selected |
        Where-Object { $_.Assembly -eq $assembly -and $_.Failures.Count -gt 0 } |
        Sort-Object -Property Crap -Descending)
    if ($failing.Count -eq 0) { continue }

    $report.Add(('## {0} - {1} failing member(s)' -f $assembly, $failing.Count))
    $report.Add('')
    $report.Add('| Member | Where | Line % | Branch % | Cx | CRAP | Fails | Uncovered lines |')
    $report.Add('| --- | --- | ---: | ---: | ---: | ---: | --- | --- |')

    foreach ($member in $failing) {
        $shown = @($member.MissingLines | Select-Object -First $MaxMissingLinesShown)
        $missingText = ''
        if ($shown.Count -gt 0) {
            $missingText = ($shown -join ', ')
            if ($member.MissingLines.Count -gt $shown.Count) {
                $missingText += (' (+{0} more)' -f ($member.MissingLines.Count - $shown.Count))
            }
        }

        # An async method is compiled into a state-machine class, so coverage arrives as
        # Owner.<TheMethod>d__14.MoveNext(). Reported literally, nobody can find it;
        # reported as Owner.TheMethod (async), it is where the developer left it.
        $segments = @($member.Class -split '\.')
        $shortClass = $segments[-1]
        $displayMember = $member.Member
        if ($shortClass -match '^<(?<method>[^>]+)>d__\d+$') {
            $displayMember = $Matches['method'] + ' (async)'
            if ($segments.Count -ge 2) { $shortClass = $segments[-2] }
        }

        $report.Add(('| `{0}.{1}` | {2}:{3} | {4} | {5} | {6} | {7} | {8} | {9} |' -f
                $shortClass, $displayMember, $member.SourceFile, $member.FirstLine,
                $member.LinePercent, $member.BranchPercent, $member.Complexity, $member.Crap,
                ($member.Failures -join ' '), $missingText))
        $jsonFailing.Add([ordered]@{
                library = $assembly; member = "$shortClass.$displayMember"
                file = "$($member.SourceFile)"; line = [int]$member.FirstLine
                linePercent = $member.LinePercent; branchPercent = $member.BranchPercent
                complexity = [int]$member.Complexity; crap = $member.Crap
                fails = @($member.Failures); uncoveredLines = $missingText
            })
    }
    $report.Add('')
}

# An excluded member is not in the coverage report at all, so exclusions are invisible
# unless they are listed on purpose. An [ExcludeFromCodeCoverage] with no justifying
# comment above it is a finding in its own right.
$excludeHits = @(Get-ChildItem -Path $repositoryRoot -Filter '*.cs' -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|data)\\' -and $_.FullName -notmatch '\.UnitTests\\' } |
    Select-String -Pattern 'ExcludeFromCodeCoverage' -SimpleMatch)

$report.Add('## Coverage exclusions in production code')
$report.Add('')
if ($excludeHits.Count -eq 0) {
    $report.Add('None. Every production member is measured.')
}
else {
    foreach ($hit in $excludeHits) {
        $path = $hit.Path
        if ($path.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            $path = $path.Substring($repositoryRoot.Length).TrimStart('\', '/')
        }
        $report.Add(('- `{0}:{1}` - {2}' -f $path, $hit.LineNumber, $hit.Line.Trim()))
    }
}

$report.Add('')
$report.Add(('**{0} failing member(s) across {1} production assembly/assemblies.**' -f $totalViolations, $assemblies.Count))

$text = $report -join [Environment]::NewLine
Write-Output $text

if ($ReportPath) {
    $text | Out-File -FilePath $ReportPath -Encoding utf8
    Write-Host "Report written to $ReportPath" -ForegroundColor Cyan
}

# The same numbers as data, for the published coverage report (.github/coverage).
if ($JsonPath) {
    $allLines = ($selected | Measure-Object -Property LinesTotal -Sum).Sum
    $allLinesCovered = ($selected | Measure-Object -Property LinesCovered -Sum).Sum
    $allBranches = ($selected | Measure-Object -Property BranchTotal -Sum).Sum
    $allBranchesCovered = ($selected | Measure-Object -Property BranchCovered -Sum).Sum
    $exclusions = @($excludeHits | ForEach-Object {
            $path = $_.Path
            if ($path.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) { $path = $path.Substring($repositoryRoot.Length).TrimStart('\', '/') }
            [ordered]@{ file = $path; line = [int]$_.LineNumber; text = $_.Line.Trim() }
        })
    $data = [ordered]@{
        measuredAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        thresholds = [ordered]@{ linePercent = $LineThreshold; branchPercent = $BranchThreshold; complexity = $ComplexityThreshold; crap = $CrapThreshold }
        totals = [ordered]@{
            linePercent = $(if ($allLines) { [math]::Round(100 * $allLinesCovered / $allLines, 2) } else { 100 })
            branchPercent = $(if ($allBranches) { [math]::Round(100 * $allBranchesCovered / $allBranches, 2) } else { 100 })
            linesCovered = [int]$allLinesCovered; linesTotal = [int]$allLines
            branchesCovered = [int]$allBranchesCovered; branchesTotal = [int]$allBranches
            libraries = $assemblies.Count
            librariesAtGate = @($jsonLibraries | Where-Object { $_.failingMembers -eq 0 }).Count
            failingMembers = $totalViolations
        }
        libraries = $jsonLibraries.ToArray()
        failing = $jsonFailing.ToArray()
        exclusions = $exclusions
    }
    $data | ConvertTo-Json -Depth 6 | Out-File -FilePath $JsonPath -Encoding utf8
    Write-Host "Data written to $JsonPath" -ForegroundColor Cyan
}

if ($totalViolations -gt 0) { exit 1 }
exit 0
