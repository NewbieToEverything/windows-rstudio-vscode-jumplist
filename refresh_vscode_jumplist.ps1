<#
.SYNOPSIS
    刷新/恢复 VS Code 任务栏 Jump List
.DESCRIPTION
    读取 vscode_workspaces.txt：
      - 本地文件夹 -> 生成 .code-workspace
      - vscode-remote://... |名字 -> 生成 .vsremote（配合 launch_remote.vbs）
    加入 VS Code 的 Jump List。
    若检测到列表被 VS Code 覆盖，则自动重建。
.NOTES
    配合任务计划 JumpList-Refresh-VSCode（每分钟检查）
#>

$ErrorActionPreference = "Stop"

# ===== 配置 =====
$Aumid   = "Microsoft.VisualStudioCode"
# 优先从注册表 App Paths 读取真实 exe（安装盘符不一定在 C:，PATH 里的 code 常是 code.cmd）
$CodeExe = $null
foreach ($root in @(
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\code.exe",
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\code.exe")) {
    $v = (Get-ItemProperty -Path $root -ErrorAction SilentlyContinue).'(default)'
    if ($v -and (Test-Path $v)) { $CodeExe = $v; break }
}
if (-not $CodeExe) {
    $cmd = (Get-Command code -ErrorAction SilentlyContinue).Source
    if ($cmd) {
        $cand = Join-Path (Split-Path (Split-Path $cmd -Parent) -Parent) "Code.exe"
        if (Test-Path $cand) { $CodeExe = $cand } else { $CodeExe = $cmd }
    }
}
# ================

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$listFile  = Join-Path $scriptDir "vscode_workspaces.txt"
$wsDir     = Join-Path $scriptDir "workspaces"
$toolExe   = Join-Path $scriptDir "JumpListTool.exe"
$logFile   = Join-Path $scriptDir "jumplist.log"

function Write-Log {
    param([string]$Message)
    $entry = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [VSCode] $Message"
    Add-Content -Path $logFile -Value $entry -Encoding UTF8
    Write-Host $entry
}

if (-not (Test-Path $listFile)) { Write-Log "ERROR: 找不到 vscode_workspaces.txt"; exit 1 }
if (-not (Test-Path $toolExe))  { Write-Log "ERROR: 找不到 JumpListTool.exe"; exit 1 }

# ---- 读取列表并生成条目文件 ----
$entries = Get-Content $listFile -Encoding UTF8 |
    Where-Object { $_ -match '^\s*[^#\s]' } |
    ForEach-Object { $_.Trim() }

New-Item -ItemType Directory -Path $wsDir -Force | Out-Null
$items = @()
$missing = 0

foreach ($e in $entries) {
    $parts = $e -split '\|', 2
    $val   = $parts[0].Trim()
    $label = if ($parts.Count -gt 1) { $parts[1].Trim() } else { $null }

    if ($val -like 'vscode-remote://*') {
        if (-not $label) { $label = "Remote-" + [Math]::Abs($val.GetHashCode()) }
        $file = Join-Path $wsDir "$label.vsremote"
        [System.IO.File]::WriteAllText($file, $val, (New-Object System.Text.UTF8Encoding $false))
        $items += $file
    }
    else {
        if (-not (Test-Path $val)) { Write-Log "MISSING: $val"; $missing++; continue }
        $name = Split-Path $val -Leaf
        if ([string]::IsNullOrWhiteSpace($name)) { $name = ($val -replace '[:\\/]', '_') }
        $file = Join-Path $wsDir "$name.code-workspace"
        $json = @{ folders = @(@{ path = $val }); settings = @{} } | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($file, $json, (New-Object System.Text.UTF8Encoding $false))
        $items += $file
    }
}

if ($items.Count -eq 0) { Write-Log "没有有效工作区，跳过"; exit 0 }

# ---- 检测是否需要重建 ----
# 通用「新窗口」条目的标题（也用于检测该条目是否已存在）
# 启动参数：官方文档的 `-n` / `--new-window`，强制开新窗口，不复用上次活动窗口
# （默认行为是复用）。注意 sentinel 用 --newwin-entry，与 VS Code 自身参数区分。
$nwTitle = "New Window"
# (a) VS Code 是否覆盖了列表（标记消失），且「新窗口」条目是否仍在
$custDest = Join-Path $env:APPDATA "Microsoft\Windows\Recent\CustomDestinations"
$intact = $false
foreach ($f in (Get-ChildItem $custDest -File -ErrorAction SilentlyContinue)) {
    try {
        $u = [System.Text.Encoding]::Unicode.GetString([System.IO.File]::ReadAllBytes($f.FullName))
        if ($u -match '\.code-workspace|\.vsremote') { $intact = $true }
        # 「新窗口」条目也必须存在，否则视为不完整（否则旧列表会一直保留）
        if ($intact -and ($u -notmatch [regex]::Escape($nwTitle))) { $intact = $false }
        if ($intact) { break }
    } catch {}
}
# (b) vscode_workspaces.txt 是否被修改
$hashFile = Join-Path $scriptDir "workspaces.hash"
$currentHash = (Get-FileHash $listFile -Algorithm SHA256).Hash
$prevHash = if (Test-Path $hashFile) { Get-Content $hashFile -Raw } else { $null }
$configChanged = ($currentHash -ne $prevHash)
if ($intact -and -not $configChanged) {
    exit 0
}
# ---- 重建 ----
$nwArgs = @("--newwin-entry", $CodeExe, "--new-window", $nwTitle, "启动新的 VS Code 窗口")
$out = & $toolExe createlinks $Aumid $CodeExe @items @nwArgs 2>&1
Write-Log "已重建 Jump List: $out"
# 保存当前配置哈希
$currentHash | Set-Content $hashFile -Encoding UTF8 -NoNewline

