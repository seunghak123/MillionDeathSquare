# Toss (Apps in Toss) dev/prod build + deploy pipeline.
#
#   .\ci\toss\deploy-toss.ps1 [-Env dev|prod|all] [-DryRun] [-ForceCloseEditor]
#
# -ForceCloseEditor : if the Unity Editor has this project open, close it (graceful first, then kill after
#                     30s) instead of aborting. Used by the Discord bot. Unsaved Editor changes are lost.
#
# Per environment: Unity batchmode build (toss-env -> toss-full) -> verify <appName>.ait -> `ait deploy`.
#   dev  : TOSS_DEV symbol  -> test ad group IDs / test promotion
#   prod : TOSS_PROD symbol -> real ad group IDs / real promotion
# NOTE: `ait deploy` always uploads to the console's QR *test* environment (the CLI has no release
# feature). The real store release is done in the Apps in Toss console. dev/prod builds are told apart
# by the deploy memo ("[dev] ..." / "[prod] ...") and by the symbols baked into the build.
#
# Env overrides: TOSS_UNITY_EXE (full path to Unity.exe), TOSS_NODE_DIR (dir containing node.exe/pnpm.cmd).
# Secrets (deployment key) are read from Assets/AppsInToss/Editor/AITCredentials.asset and are never logged.
#
# Kept ASCII-only on purpose (Windows PowerShell 5.1 reads BOM-less files as ANSI).

param(
    [ValidateSet("dev", "prod", "all")]
    [string]$Env = "all",
    [switch]$DryRun,
    [switch]$ForceCloseEditor
)

$ErrorActionPreference = "Stop"

$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$LogDir      = Join-Path $PSScriptRoot "logs"
$LockFile    = Join-Path $PSScriptRoot ".lock"
$AitBuildDir = Join-Path $ProjectPath "ait-build"
$AitConfig   = Join-Path $ProjectPath "Assets\AppsInToss\Editor\AITConfig.asset"
$AitCred     = Join-Path $ProjectPath "Assets\AppsInToss\Editor\AITCredentials.asset"
$ProjSettings = Join-Path $ProjectPath "ProjectSettings\ProjectSettings.asset"
$Stamp       = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$MainLog     = Join-Path $LogDir "deploy-$Stamp.log"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$script:DeployKey = ""

function Write-Log([string]$msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format "HH:mm:ss"), $msg
    if ($script:DeployKey) { $line = $line.Replace($script:DeployKey, "***") }
    Write-Host $line
    Add-Content -Path $MainLog -Value $line -Encoding UTF8
}

function Get-YamlValue([string]$file, [string]$key) {
    $m = Select-String -Path $file -Pattern ("^\s*{0}:\s*(.*)$" -f [regex]::Escape($key)) | Select-Object -First 1
    if (-not $m) { return "" }
    return $m.Matches[0].Groups[1].Value.Trim().Trim("'").Trim('"')
}

function Resolve-UnityExe {
    if ($env:TOSS_UNITY_EXE) { return $env:TOSS_UNITY_EXE }
    $verFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
    $ver = (Select-String -Path $verFile -Pattern "m_EditorVersion:\s*(\S+)" | Select-Object -First 1).Matches[0].Groups[1].Value
    return "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe"
}

function Resolve-NodeDir {
    if ($env:TOSS_NODE_DIR) { return $env:TOSS_NODE_DIR }
    $base = Join-Path $env:LOCALAPPDATA ".ait-unity-sdk\nodejs"
    if (Test-Path $base) {
        $cand = Get-ChildItem $base -Directory | Sort-Object Name -Descending | ForEach-Object { Join-Path $_.FullName "win-x64" } |
            Where-Object { Test-Path (Join-Path $_ "pnpm.cmd") } | Select-Object -First 1
        if ($cand) { return $cand }
    }
    return $null
}

function Get-UnityProcsOnProject {
    Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$ProjectPath*" }
}

function Test-SymbolsInProject([string]$e) {
    $txt = Get-Content $ProjSettings -Raw
    $hasDev  = $txt -match "\bTOSS_DEV\b"
    $hasProd = $txt -match "\bTOSS_PROD\b"
    if ($e -eq "prod") { return ($hasProd -and -not $hasDev) }
    return ($hasDev -and -not $hasProd)
}

function Invoke-Unity([string]$exe, [string[]]$unityArgs, [string]$logFile, [string]$label) {
    Write-Log "Unity [$label]: $exe $($unityArgs -join ' ')"
    if ($DryRun) { Write-Log "(DryRun) not executed"; return 0 }
    $p = Start-Process -FilePath $exe -ArgumentList $unityArgs -PassThru -Wait -WindowStyle Hidden
    return $p.ExitCode
}

function Show-LogTail([string]$file, [int]$n = 40) {
    if (Test-Path $file) {
        Write-Log "---- last $n lines of $file ----"
        Get-Content $file -Tail $n | ForEach-Object { Write-Log $_ }
    }
}

function Build-And-Deploy([string]$e, [string]$unityExe, [string]$appName, [string]$version) {
    $result = @{ Env = $e; Ok = $false; Detail = ""; Url = "" }
    $setLog   = Join-Path $LogDir "unity-$Stamp-$e-setenv.log"
    $buildLog = Join-Path $LogDir "unity-$Stamp-$e-build.log"

    # ---- 1) symbols: set + persist in a separate process so the build process compiles with them ----
    if (-not $DryRun -and (Test-SymbolsInProject $e)) {
        Write-Log "[$e] scripting symbols already correct - skipping toss-env step"
    } else {
        $a = @("-batchmode", "-nographics", "-quit", "-projectPath", $ProjectPath,
               "-executeMethod", "Seunghak.Editor.WebGLBuildTool.BuildCI",
               "-platform", "toss-env", "-tossEnv", $e, "-logFile", $setLog)
        $code = Invoke-Unity $unityExe $a $setLog "$e set symbols"
        if ($code -ne 0) { $result.Detail = "set-symbols step failed (exit $code)"; Show-LogTail $setLog; return $result }
        if (-not $DryRun -and -not (Test-SymbolsInProject $e)) {
            $result.Detail = "TOSS_DEV/TOSS_PROD not persisted in ProjectSettings after set-symbols step"
            Show-LogTail $setLog
            return $result
        }
    }

    # ---- 2a) clean previous build output (always) ----
    # Previous webgl/ and ait-build/ outputs must never leak into a new package.
    # ait-build/node_modules is kept: it is a dependency cache (also provides `ait deploy`), not build output.
    $webglDir = Join-Path $ProjectPath "webgl"
    if ($DryRun) {
        Write-Log "[$e] (DryRun) would remove $webglDir and $AitBuildDir (except node_modules)"
    } else {
        if (Test-Path $webglDir) { Remove-Item $webglDir -Recurse -Force; Write-Log "[$e] removed $webglDir" }
        if (Test-Path $AitBuildDir) {
            Get-ChildItem $AitBuildDir -Force | Where-Object { $_.Name -ne "node_modules" } |
                Remove-Item -Recurse -Force
            Write-Log "[$e] cleaned $AitBuildDir (node_modules kept)"
        }
    }

    # ---- 2) build + AIT packaging ----
    $buildStart = Get-Date
    $a = @("-batchmode", "-nographics", "-quit", "-projectPath", $ProjectPath,
           "-executeMethod", "Seunghak.Editor.WebGLBuildTool.BuildCI",
           "-platform", "toss-full", "-tossEnv", $e, "-logFile", $buildLog)
    $code = Invoke-Unity $unityExe $a $buildLog "$e build"
    if ($code -ne 0) { $result.Detail = "Unity build failed (exit $code) - see $buildLog"; Show-LogTail $buildLog 60; return $result }

    # ---- 3) artifact check ----
    $ait = Join-Path $AitBuildDir "$appName.ait"
    if ($DryRun) {
        Write-Log "(DryRun) would verify $ait exists and is newer than build start"
    } else {
        if (-not (Test-Path $ait)) { $result.Detail = "artifact missing: $ait"; Show-LogTail $buildLog 60; return $result }
        $fi = Get-Item $ait
        if ($fi.LastWriteTime -lt $buildStart) { $result.Detail = "artifact is stale (older than this build): $ait"; Show-LogTail $buildLog 60; return $result }
        Write-Log ("[$e] artifact OK: {0} ({1:N1} MB)" -f $ait, ($fi.Length / 1MB))
    }

    # ---- 4) deploy ----
    $memo = "[$e] $appName v$version $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    $nodeDir = Resolve-NodeDir
    $pnpm = if ($nodeDir) { Join-Path $nodeDir "pnpm.cmd" } else { "pnpm" }
    $shown = "`"$pnpm`" exec ait deploy --api-key *** -m `"$memo`"  (cwd: $AitBuildDir)"
    Write-Log "[$e] deploy: $shown"
    if ($DryRun) { Write-Log "(DryRun) not executed"; $result.Ok = $true; $result.Detail = "dry-run"; return $result }

    if (-not $nodeDir) { $result.Detail = "embedded Node.js/pnpm not found (run an Apps in Toss build once in the Editor, or set TOSS_NODE_DIR)"; return $result }
    $oldPath = $env:PATH
    $env:PATH = (Join-Path $AitBuildDir "node_modules\.bin") + ";" + $nodeDir + ";" + $env:PATH
    # Non-interactive: pnpm's pre-run deps check may need to reinstall node_modules
    # (e.g. after an SDK upgrade) and aborts without a TTY unless CI=true.
    $oldCi = $env:CI
    $env:CI = "true"
    $deployLog = Join-Path $LogDir "deploy-$Stamp-$e-ait.log"
    $out = New-Object System.Collections.Generic.List[string]
    Push-Location $AitBuildDir
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $pnpm exec ait deploy --api-key $script:DeployKey -m $memo 2>&1 | ForEach-Object {
            $l = ("$_").Replace($script:DeployKey, "***")
            $out.Add($l); Write-Host $l
        }
        $dcode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $prevEap
        Pop-Location
        $env:PATH = $oldPath
        $env:CI = $oldCi
    }
    $out | Set-Content -Path $deployLog -Encoding UTF8
    $m = [regex]::Match(($out -join "`n"), "intoss-private://[^\s|]+")
    if ($m.Success) { $result.Url = $m.Value }
    if ($dcode -ne 0) { $result.Detail = "ait deploy failed (exit $dcode) - see $deployLog"; return $result }
    $result.Ok = $true
    $result.Detail = "deployed"
    return $result
}

# ======================================================================
$envs = if ($Env -eq "all") { @("dev", "prod") } else { @($Env) }
Write-Log "Toss deploy start: Env=$Env DryRun=$($DryRun.IsPresent) project=$ProjectPath"

try {
    # ---- single-run lock ----
    if (-not $DryRun) {
        if (Test-Path $LockFile) {
            $oldPid = [int](Get-Content $LockFile -ErrorAction SilentlyContinue | Select-Object -First 1)
            if ($oldPid -and (Get-Process -Id $oldPid -ErrorAction SilentlyContinue)) {
                Write-Log "ABORT: another deploy is already running (pid $oldPid)."
                exit 1
            }
        }
        Set-Content -Path $LockFile -Value $PID
    }

    # ---- preconditions ----
    $unityExe = Resolve-UnityExe
    Write-Log "Unity exe: $unityExe"
    if (-not (Test-Path $unityExe)) {
        Write-Log "ABORT: Unity.exe not found. Install the version in ProjectSettings/ProjectVersion.txt or set TOSS_UNITY_EXE."
        if (-not $DryRun) { exit 1 }
    }

    $procs = Get-UnityProcsOnProject
    if ($procs -and $ForceCloseEditor -and -not $DryRun) {
        # Only the interactive Editor is closed. A batchmode Unity on this project is another build -> abort below.
        $editors = $procs | Where-Object { $_.CommandLine -notmatch '-batchmode' -and $_.CommandLine -notmatch 'AssetImportWorker' }
        foreach ($ed in $editors) {
            Write-Log "ForceCloseEditor: closing Unity Editor (PID $($ed.ProcessId)) ..."
            $p = Get-Process -Id $ed.ProcessId -ErrorAction SilentlyContinue
            if ($p) {
                [void]$p.CloseMainWindow()
                if (-not $p.WaitForExit(30000)) {
                    Write-Log "ForceCloseEditor: Editor did not exit in 30s (save dialog?) - killing it."
                    Stop-Process -Id $ed.ProcessId -Force -ErrorAction SilentlyContinue
                    [void]$p.WaitForExit(15000)
                }
            }
        }
        # Leftover asset import workers of the closed Editor would keep the project locked.
        Get-UnityProcsOnProject | Where-Object { $_.CommandLine -match 'AssetImportWorker' } |
            ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Seconds 2
        $lockFile = Join-Path $ProjectPath "Temp\UnityLockfile"
        if (Test-Path $lockFile) { Remove-Item $lockFile -Force -ErrorAction SilentlyContinue }
        $procs = Get-UnityProcsOnProject
        if (-not $procs) { Write-Log "ForceCloseEditor: Editor closed." }
    }
    if ($procs) {
        $pids = ($procs | ForEach-Object { $_.ProcessId }) -join ", "
        Write-Log "Unity Editor/batch process is using this project (PID $pids)."
        if ($DryRun) {
            Write-Log "(DryRun) a real run would ABORT here - close the Editor first (batchmode cannot open an already-open project)."
        } else {
            Write-Log "ABORT: close the Unity Editor for this project, then retry. (batchmode fails when the project is open; not force-closing it to avoid losing unsaved work)"
            exit 1
        }
    }

    $appName = Get-YamlValue $AitConfig "appName"
    $version = Get-YamlValue $AitConfig "version"
    $iconUrl = Get-YamlValue $AitConfig "iconUrl"
    if (-not $appName) { Write-Log "ABORT: appName missing in AITConfig.asset"; if (-not $DryRun) { exit 1 } }
    Write-Log "App: $appName v$version"
    if (-not $iconUrl) { Write-Log "NOTE: AITConfig.iconUrl is empty - building without a brand icon." }

    if (-not (Test-Path $AitCred)) {
        Write-Log "ABORT: AITCredentials.asset not found (deployment key not set)."
        if (-not $DryRun) { exit 1 }
    } else {
        $script:DeployKey = Get-YamlValue $AitCred "deploymentKey"
        if (-not $script:DeployKey) {
            Write-Log "ABORT: deploymentKey is empty in AITCredentials.asset (Apps in Toss > Configuration)."
            if (-not $DryRun) { exit 1 }
        } else {
            Write-Log "Deployment key: present (value hidden)"
        }
    }

    # ---- run ----
    $results = @()
    foreach ($e in $envs) {
        Write-Log "=== [$e] start ==="
        $r = Build-And-Deploy $e $unityExe $appName $version
        $results += $r
        if ($r.Ok) { Write-Log "=== [$e] OK ($($r.Detail)) $($r.Url) ===" }
        else       { Write-Log "=== [$e] FAILED: $($r.Detail) ==="; break }   # all: do not continue to prod if dev failed
    }

    # ---- leave the project on dev symbols (non-fatal) ----
    if (-not $DryRun -and ($envs -contains "prod") -and -not (Test-SymbolsInProject "dev")) {
        $restoreLog = Join-Path $LogDir "unity-$Stamp-restore-dev.log"
        $a = @("-batchmode", "-nographics", "-quit", "-projectPath", $ProjectPath,
               "-executeMethod", "Seunghak.Editor.WebGLBuildTool.BuildCI",
               "-platform", "toss-env", "-tossEnv", "dev", "-logFile", $restoreLog)
        $c = Invoke-Unity $unityExe $a $restoreLog "restore dev symbols"
        if ($c -ne 0) { Write-Log "WARNING: could not restore dev symbols (exit $c). ProjectSettings still has TOSS_PROD." }
    }

    # ---- summary ----
    Write-Log "================ SUMMARY ================"
    $failed = $false
    foreach ($r in $results) {
        $status = if ($r.Ok) { "OK" } else { "FAILED" }
        Write-Log ("{0,-5} {1}  {2}  {3}" -f $r.Env, $status, $r.Detail, $r.Url)
        if (-not $r.Ok) { $failed = $true }
    }
    foreach ($e in $envs) { if (-not ($results | Where-Object { $_.Env -eq $e })) { Write-Log ("{0,-5} SKIPPED" -f $e); $failed = $true } }
    Write-Log "log: $MainLog"
    if ($failed) { exit 1 }
    exit 0
}
finally {
    if (-not $DryRun -and (Test-Path $LockFile)) {
        $lp = Get-Content $LockFile -ErrorAction SilentlyContinue | Select-Object -First 1
        if ("$lp" -eq "$PID") { Remove-Item $LockFile -Force -ErrorAction SilentlyContinue }
    }
}
