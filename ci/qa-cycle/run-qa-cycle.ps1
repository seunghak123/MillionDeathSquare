# QA automation cycle: Toss WebGL build -> serve locally -> Playwright smoke test -> save report.
# Registered in Windows Task Scheduler to run daily at 00:00 / 02:00 / 04:00.
# Report-only: does not modify code or scene files automatically.
#
# NOTE: kept ASCII-only on purpose. Windows PowerShell 5.1 parses .ps1 files using the system
# ANSI codepage unless a UTF-8 BOM is present, and non-ASCII text here previously caused garbled
# strings and parser errors when the file was saved without a BOM.

$ErrorActionPreference = "Stop"

$ProjectPath  = "C:\Users\dhtmd\Desktop\BaseProject"
$UnityExe     = "C:\Program Files\Unity\Hub\Editor\2023.2.20f1\Editor\Unity.exe"
$BuildDir     = Join-Path $ProjectPath "Builds\Toss"
$AitDistDir   = Join-Path $ProjectPath "ait-build\dist"
$WorkDir      = Join-Path $ProjectPath "ci\qa-cycle"
$ReportsDir   = Join-Path $WorkDir "reports"
$LockFile     = Join-Path $WorkDir ".lock"
$Timestamp    = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$ReportFile   = Join-Path $ReportsDir "$Timestamp.md"
$BuildLogFile = Join-Path $ReportsDir "$Timestamp-unity-build.log"
$QaResultFile = Join-Path $ReportsDir "$Timestamp-qa-result.json"
$Port         = 8743

New-Item -ItemType Directory -Force -Path $ReportsDir | Out-Null

# Skip this run if a previous run is still in progress (e.g. build taking longer than 2 hours).
if (Test-Path $LockFile) {
    $lockAge = (Get-Date) - (Get-Item $LockFile).LastWriteTime
    if ($lockAge.TotalHours -lt 3) {
        Write-Output "[QA Cycle] Previous run still appears active (lock age: $([math]::Round($lockAge.TotalMinutes, 1)) min). Skipping this run."
        exit 0
    }
    Write-Output "[QA Cycle] Stale lock file found - assuming previous run crashed, cleaning up and continuing."
}
New-Item -ItemType File -Force -Path $LockFile | Out-Null

$report = New-Object System.Collections.Generic.List[string]
$serverProcess = $null

try {
    $report.Add("# QA Auto Cycle Report - $Timestamp")
    $report.Add("")

    # ---- 0. Skip this run if the Unity Editor already has this project open ----
    # An earlier version of this script force-closed the running Editor before building.
    # That turned out to be unreliable in this environment: killing Unity.exe gets treated
    # as a crash, and something (Unity Hub's reopen-last-project behavior, most likely)
    # relaunches the Editor on the same project shortly after, leaving Temp/UnityLockfile
    # locked again and the very next batchmode launch failing anyway. Skipping is safer:
    # no risk of losing unsaved editor work, and the next scheduled run (2 hours later)
    # gets a fresh chance.
    # Temp/UnityLockfile can also be left behind by a build that was itself killed/crashed
    # (not a real running Editor). Only treat it as "editor is open" if a Unity.exe process
    # for this project is actually running; otherwise it is a stale lock and we clear it
    # ourselves so future scheduled runs are not blocked forever.
    $lockfilePath = Join-Path $ProjectPath "Temp\UnityLockfile"
    if (Test-Path $lockfilePath) {
        $unityProcs = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -and $_.CommandLine -like "*$ProjectPath*" }
        if ($unityProcs) {
            $report.Add("## Skipped")
            $report.Add("Unity Editor already has this project open (Temp/UnityLockfile present, PID(s): $(($unityProcs | ForEach-Object { $_.ProcessId }) -join ', ')). Skipping this run rather than force-closing it.")
            $report | Out-File -FilePath $ReportFile -Encoding utf8
            Write-Output "[QA Cycle] Skipped (editor open) - report: $ReportFile"
            exit 0
        }
        # Stale lock: no matching process, safe to clear.
        Remove-Item -Path $lockfilePath -Force -ErrorAction SilentlyContinue
    }

    # ---- 1. WebGL build (Toss) + AIT packaging ----
    # "toss-full" runs Unity's WebGL build AND the Apps-in-Toss SDK's synchronous
    # packaging step (AITConvertCore.DoExport), producing ait-build/dist/ and the
    # .ait package. The plain "toss" platform only does the raw Unity build and does
    # NOT reliably produce an .ait file (TossPostBuild's reflection-based post-build
    # hook does not consistently fire) - use toss-full for anything that needs the
    # actual deployable Toss package.
    $report.Add("## 1. WebGL Build + AIT Package (Toss)")
    $buildStart = Get-Date
    $unityArgs = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", $ProjectPath,
        "-executeMethod", "Seunghak.Editor.WebGLBuildTool.BuildCI",
        "-platform", "toss-full",
        "-logFile", $BuildLogFile
    )
    $unityProc = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -Wait -WindowStyle Hidden
    $buildExitCode = $unityProc.ExitCode
    $buildDuration = (Get-Date) - $buildStart

    if ($buildExitCode -ne 0) {
        $report.Add("**FAILED** (exit code $buildExitCode, took $($buildDuration.ToString('mm\:ss')))")
        $report.Add("")
        $report.Add("Unity build log (last 60 lines):")
        $report.Add('```')
        if (Test-Path $BuildLogFile) {
            $report.AddRange([string[]](Get-Content $BuildLogFile -Tail 60))
        } else {
            $report.Add("(build log file not found)")
        }
        $report.Add('```')
        $report | Out-File -FilePath $ReportFile -Encoding utf8
        Write-Output "[QA Cycle] Build failed - report: $ReportFile"
        exit 1
    }
    $report.Add("OK (took $($buildDuration.ToString('mm\:ss')))")
    $report.Add("")

    # ---- 1b. Verify the AIT package was actually produced ----
    $report.Add("## 1b. AIT Package Output Check")
    if ((Test-Path $AitDistDir) -and (Get-ChildItem $AitDistDir -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $buildStart })) {
        $distFiles = Get-ChildItem $AitDistDir -Recurse -File
        $distSizeMb = [math]::Round(($distFiles | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
        $report.Add("OK - ait-build/dist/ contains $($distFiles.Count) files freshly written this run ($distSizeMb MB).")
    } else {
        $report.Add("**WARNING** - ait-build/dist/ has no files updated by this run (Toss AIT packaging step may not have actually run despite build reporting success).")
        $report.Add("Unity build log (last 80 lines):")
        $report.Add('```')
        if (Test-Path $BuildLogFile) {
            $report.AddRange([string[]](Get-Content $BuildLogFile -Tail 80))
        }
        $report.Add('```')
    }
    $report.Add("")

    # ---- 2. Serve the actual deployable package locally (ait-build/dist, not the raw Builds/Toss output) ----
    $serveDir = if (Test-Path $AitDistDir) { $AitDistDir } else { $BuildDir }
    $serverArgs = @("--yes", "http-server", $serveDir, "-p", "$Port", "-s")
    $serverProcess = Start-Process -FilePath "npx" -ArgumentList $serverArgs -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 3

    # ---- 3. Playwright QA smoke test ----
    $report.Add("## 2. QA Smoke Test (serving: $serveDir)")
    node "$WorkDir\qa-smoke-test.mjs" "http://localhost:$Port" "$QaResultFile"
    $qaExitCode = $LASTEXITCODE

    if (Test-Path $QaResultFile) {
        $qaResult = Get-Content $QaResultFile -Raw | ConvertFrom-Json
        $status = if ($qaExitCode -eq 0) { "PASS" } else { "ISSUES FOUND" }
        $report.Add("Result: **$status**")
        $report.Add("- Page loaded: $($qaResult.loaded)")
        $report.Add("- Canvas found: $($qaResult.canvasFound)")
        $report.Add("- Input attempted: $($qaResult.inputAttempted)")
        $report.Add("- Console/runtime error count: $($qaResult.errors.Count)")
        if ($qaResult.errors.Count -gt 0) {
            $report.Add("")
            $report.Add("Errors:")
            foreach ($e in $qaResult.errors) { $report.Add("- $e") }
        }
    } else {
        $report.Add("Result: **UNKNOWN** - QA script did not produce a result file (exit code $qaExitCode)")
    }

    $report | Out-File -FilePath $ReportFile -Encoding utf8
    Write-Output "[QA Cycle] Done - report: $ReportFile"
}
finally {
    if ($serverProcess -ne $null) {
        Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -Path $LockFile -Force -ErrorAction SilentlyContinue
}
