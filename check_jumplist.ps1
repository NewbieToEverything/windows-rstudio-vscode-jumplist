<#
.SYNOPSIS
    刷新 RStudio 任务栏 Jump List
.DESCRIPTION
    新版 Electron RStudio 不再自动注册 Windows Jump List，
    本脚本用 ICustomDestinationList API 手动生成。

    工作流程：
      1. 确保文件类型 AUMID 已注册（HKCU\Software\Classes\<ProgID>\Application）
      2. 读取 app_list.txt 中的项目路径
      3. 生成 Jump List：
         - 优先用"自定义分类"（显示为「最近项目」）
         - 若因隐私设置（关闭"显示最近使用项目"）失败，自动回退到"任务"区

    配置说明见下方"配置"区。列表文件 app_list.txt：
      每行一个文件路径，# 开头为注释，空行忽略。

.NOTES
    配合任务计划 JumpList-Refresh（锁屏/解锁时运行）
#>

$ErrorActionPreference = "Stop"

# ============ 配置 ============
# RStudio 的 AUMID（等于其 exe 路径，必须是任务栏按钮实际使用的值）
# 优先从注册表 App Paths 读取真实 exe（安装盘符不一定在 C:）
$AppAumid = $null
foreach ($root in @(
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rstudio.exe",
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rstudio.exe")) {
    $v = (Get-ItemProperty -Path $root -ErrorAction SilentlyContinue).'(default)'
    if ($v -and (Test-Path $v)) { $AppAumid = $v; break }
}
if (-not $AppAumid) { $AppAumid = (Get-Command rstudio -ErrorAction SilentlyContinue).Source }
# 归一化盘符大小写（注册表可能返回小写 d:）
if ($AppAumid -match '^[a-z]:') { $AppAumid = $AppAumid.Substring(0,1).ToUpper() + $AppAumid.Substring(1) }
# 与项目文件关联的 ProgID
$ProgId = "RStudio.Rproj"
# Jump List 中显示的分类名称
$CategoryName = "最近项目"
# 最多显示的项目数
$MaxItems = 10
# ==============================

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$listFile = Join-Path $scriptDir "app_list.txt"
$toolExe = Join-Path $scriptDir "JumpListTool.exe"
$logFile = Join-Path $scriptDir "jumplist.log"

function Write-Log {
    param([string]$Message)
    $entry = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Add-Content -Path $logFile -Value $entry -Encoding UTF8
    Write-Host $entry
}

try {
    # ---- 1. 确保文件类型 AUMID 注册 ----
    $regPath = "HKCU:\Software\Classes\$ProgId\Application"
    if (-not (Test-Path $regPath)) {
        New-Item -Path $regPath -Force | Out-Null
    }
    $current = (Get-ItemProperty -Path $regPath -Name "AppUserModelID" -ErrorAction SilentlyContinue).AppUserModelID
    if ($current -ne $AppAumid) {
        Set-ItemProperty -Path $regPath -Name "AppUserModelID" -Value $AppAumid -Force
        Write-Log "已注册文件类型 AUMID: $ProgId -> $AppAumid"
    }

    # ---- 2. 检查工具 ----
    if (-not (Test-Path $toolExe)) {
        Write-Log "ERROR: 找不到 JumpListTool.exe ($toolExe)"
        exit 1
    }

    # ---- 3. 读取列表 ----
    if (-not (Test-Path $listFile)) {
        Write-Log "ERROR: 找不到 app_list.txt ($listFile)"
        exit 1
    }
    $all = Get-Content $listFile -Encoding UTF8 |
        Where-Object { $_ -match '^\s*[^#\s]' } |
        ForEach-Object { $_.Trim() }

    $existing = @($all | Where-Object { Test-Path $_ } | Select-Object -First $MaxItems)
    $missing = @($all | Where-Object { -not (Test-Path $_) })

    if ($existing.Count -eq 0) {
        Write-Log "列表中没有有效文件，跳过"
        exit 0
    }

    foreach ($m in $missing) { Write-Log "MISSING: $m" }

    # ---- 4. 生成 Jump List ----
    # 优先：自定义分类（隐私设置开启时会失败）
    # 通用「New Window」条目：通过 launch_rstudio_new.vbs 启动空白新会话。
# RStudio 无「强制新实例」CLI 开关（命令行开关仅 --run-diagnostics/--session-server/
# --session-url/--use-temp-cookies/--version/--log-level/--session-delay/--session-exit）。
# 依据源码：
#   - core/r-user-data.ts 定义 kProjectNone='none'，即 RS_INITIAL_PROJECT=none
#     表示「新会话不加载任何项目」，可避免继承上次打开的项目；
#   - application.ts 的 shouldInstanceOpen() 表明带 .rproj 启动必然开新实例，
#     而无参数启动可能复用，故这里不传 .rproj，只设环境变量强制空白新会话。
$launcher = "launch_rstudio_new.vbs"
$nwArgs = @("--newwin-entry", "wscript.exe", """$launcher""", "New Window", "启动新的 RStudio 空白会话（不加载项目）", $AppAumid)
    $out = & $toolExe create $AppAumid $CategoryName @existing @nwArgs 2>&1
    $code = $LASTEXITCODE

    if ($code -ne 0) {
        Write-Log "自定义分类失败（可能因关闭了'显示最近使用项目'），回退到任务模式"
        $out = & $toolExe createtasks $AppAumid @existing @nwArgs 2>&1
        $code = $LASTEXITCODE
        if ($code -eq 0) {
            Write-Log "（任务模式）$out"
        } else {
            Write-Log "ERROR: 任务模式也失败: $out"
            exit 1
        }
    } else {
        Write-Log "$out"
    }
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    exit 1
}
