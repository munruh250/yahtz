<#
.SYNOPSIS
    Runs the Unity test suites headlessly against a mirror of this project.

.DESCRIPTION
    Unity allows one instance per project PATH, so a headless run against the working copy fails
    with HandleProjectAlreadyOpenInAnotherInstance whenever the editor is open. This mirrors
    Assets/Packages/ProjectSettings to a second path and runs there instead, so tests and the
    framing renders work without anyone closing the editor.

    Library is deliberately not mirrored: it is regenerated, and copying one that a live editor is
    writing risks a torn state. The first run imports everything and is slow (minutes); later runs
    reuse the testbed's own Library and are as fast as a normal run.

.EXAMPLE
    Tools\run-tests.ps1                                     # PlayMode, the slow suite
    Tools\run-tests.ps1 -Platform EditMode                  # ~1s of tests after startup
    Tools\run-tests.ps1 -Filter Yahtzee.Tests.DiceTapTests  # one fixture
#>
param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform = "PlayMode",
    [string]$Filter = "",
    [string]$Testbed = "C:\yz-test",
    # The plain 2022.3.62f3 install on this machine is corrupt (missing PackageManager\Server);
    # the -x86_64 sibling is the intact one. See Docs/HANDOFF.md section 2.
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3-x86_64\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"
$source = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $Unity)) { throw "Unity not found at $Unity" }

foreach ($folder in @("Assets", "Packages", "ProjectSettings")) {
    robocopy "$source\$folder" "$Testbed\$folder" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $folder (exit $LASTEXITCODE)" }
}
$global:LASTEXITCODE = 0  # robocopy uses 1-7 for success variants

$results = Join-Path $Testbed "test-results-$Platform.xml"
$log = Join-Path $Testbed "test-log-$Platform.txt"
if (Test-Path $results) { Remove-Item $results }

$unityArgs = @("-batchmode", "-projectPath", $Testbed, "-runTests", "-testPlatform", $Platform,
               "-testResults", $results, "-logFile", $log)
if ($Filter -ne "") { $unityArgs += @("-testFilter", $Filter) }

$proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru
Wait-Process -Id $proc.Id -Timeout 1800

if (-not (Test-Path $results)) {
    Write-Host "NO RESULTS — last 25 log lines:"
    Get-Content $log -Tail 25
    exit 1
}

[xml]$xml = Get-Content $results
$run = $xml."test-run"
"{0}: total={1} passed={2} failed={3}" -f $Platform, $run.total, $run.passed, $run.failed

$xml.SelectNodes("//test-case") | ForEach-Object {
    if ($_.output) { $_.output.InnerText.TrimEnd() }
    if ($_.result -ne "Passed") {
        "FAILED: {0}" -f $_.fullname
        $_.failure.message.InnerText
        $_.failure."stack-trace".InnerText
    }
}

# Framing renders land in the testbed's own persistent data path, not the editor's.
if ($run.failed -eq "0" -and $Platform -eq "PlayMode") {
    "renders: $env:USERPROFILE\AppData\LocalLow\DefaultCompany\yahtzee\framings"
}
exit [int]$run.failed
