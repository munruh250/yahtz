<#
.SYNOPSIS
    Builds an Android APK from the testbed, installs it, launches it, and fails on any runtime
    exception in logcat.

.DESCRIPTION
    EditMode and PlayMode run inside the editor, where nothing is stripped and every shader and
    component type is loaded. They cannot catch build-only failures. Two real examples, both of
    which left the game visibly broken on device while the suite was green:

      * CreatePrimitive(Cylinder) returns no collider on Android -- IL2CPP strips collider types
        the game never names, and nothing referenced CapsuleCollider. KitchenBuilder.Build threw
        in Awake, so the whole 3D scene, dice and scorecard were absent.
      * Shader.Find only resolves shaders that survive into the build.

    A desktop player is NOT a substitute: stripping is per-platform, and a Windows build booted
    clean on the exact code that crashed on the phone. Only this catches device-only breakage.

    Requires a connected, USB-debugging-authorised device (check with -ListDevices).

.EXAMPLE
    Tools\device-smoke.ps1 -ListDevices
    Tools\device-smoke.ps1            # build, install, launch, scan
    Tools\device-smoke.ps1 -SkipBuild # re-launch and re-scan the installed build
#>
param(
    [string]$Testbed = "C:\yz-test",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3-x86_64\Editor\Unity.exe",
    [string]$Package = "com.DefaultCompany.yahtzee",
    [int]$RunSeconds = 10,
    [switch]$SkipBuild,
    [switch]$ListDevices
)

$ErrorActionPreference = "Stop"
$source = Split-Path -Parent $PSScriptRoot
$adb = Join-Path (Split-Path -Parent $Unity) "Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
if (-not (Test-Path $adb)) { throw "adb not found at $adb -- is Android Build Support installed?" }

if ($ListDevices) { & $adb devices -l; exit 0 }

$apk = Join-Path $Testbed "Build\Smoke\yahtzee-smoke.apk"

if (-not $SkipBuild) {
    foreach ($folder in @("Assets", "Packages", "ProjectSettings")) {
        robocopy "$source\$folder" "$Testbed\$folder" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $folder (exit $LASTEXITCODE)" }
    }
    $global:LASTEXITCODE = 0

    Write-Host "Building APK (IL2CPP/ARM64 -- several minutes)..."
    $buildLog = Join-Path $Testbed "device-smoke-build.log"
    $build = Start-Process -FilePath $Unity -PassThru -ArgumentList @(
        "-batchmode", "-projectPath", $Testbed, "-logFile", $buildLog,
        "-executeMethod", "Yahtzee.EditorTools.SmokeBuildTool.BuildAndroidBatch")
    Wait-Process -Id $build.Id -Timeout 3600

    if (-not (Test-Path $apk)) {
        Write-Host "BUILD FAILED -- last 40 log lines:"
        Get-Content $buildLog -Tail 40
        exit 1
    }
    Get-Content $buildLog | Select-String "SMOKEBUILD" | ForEach-Object { $_.Line }
}

# adb chats on stderr routinely ("daemon not running; starting now"), and with
# ErrorActionPreference=Stop PowerShell turns that into a terminating NativeCommandError.
$ErrorActionPreference = "Continue"
& $adb start-server | Out-Null

if (-not (& $adb devices | Select-String "\sdevice$")) {
    Write-Host "No authorised device. Enable USB debugging and accept the on-phone prompt."
    & $adb devices -l
    exit 1
}

Write-Host "Installing..."
& $adb install -r $apk | Select-Object -Last 2

Write-Host "Launching and capturing logcat for ${RunSeconds}s..."
& $adb logcat -c
& $adb shell am force-stop $Package
& $adb shell monkey -p $Package -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds $RunSeconds

$log = Join-Path $Testbed "device-logcat.txt"
& $adb logcat -d -v brief > $log

$problems = Get-Content $log | Select-String -Pattern "E/Unity" |
            Select-String -Pattern "Exception|Error:|error CS"
if ($problems) {
    Write-Host "DEVICE SMOKE FAILED -- exceptions in logcat:"
    Get-Content $log | Select-String -Pattern "E/Unity" | Select-Object -First 40 | ForEach-Object { $_.Line }
    Write-Host "full log: $log"
    exit 1
}

Write-Host "DEVICE SMOKE CLEAN -- app launched with no Unity exceptions."
Write-Host "full log: $log"
exit 0
