# 任务栏 Jump List 刷新工具

## 背景

Windows 任务栏按钮右键菜单里的 Jump List（「最近项目 / 工作区」）依赖系统的「显示最近使用项目」隐私设置
（`HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\Start_TrackDocs`）。
该设置一旦关闭，列表会被系统清空；Wise Care 之类的清理工具也会把它清掉。

本项目用 `ICustomDestinationList` API 手动重建 Jump List，使其在隐私设置关闭的情况下依然可用，
并通过计划任务在锁屏/解锁时、以及被 VS Code 覆盖后自动恢复。

## 核心原理

1. **AUMID（AppUserModelID）**：Jump List 按 AUMID 归属。
   - RStudio：等于其 `rstudio.exe` 路径（优先从注册表 `App Paths\rstudio.exe` 读取，兼容非 C: 安装盘）
   - VS Code：`Microsoft.VisualStudioCode`（来自其 `product.json` 的 `win32AppUserModelId`）
2. **文件类型注册**：要让某类文件出现在应用的任务栏菜单里，该扩展名对应的 ProgID 必须注册 AUMID：
   `HKCU\Software\Classes\<ProgID>\Application\AppUserModelID`，否则报 `0x80040F03`。
3. **隐私设置的限制**：`Start_TrackDocs = 0` 时，`AppendCategory`（自定义分类）会失败
   （`E_ACCESSDENIED / 0x80070005`），但 `AddUserTasks`（任务区）仍然可用。
   因此脚本先尝试「自定义分类」，失败则自动回退到「任务模式」。
4. **VS Code 会覆盖列表**：VS Code 自身实现了 Jump List（`setJumpList` / `addRecentDocument`），
   启动时会把我们写入的列表覆盖掉，所以需要每分钟检测并重建。

## 目录结构

| 文件 | 作用 |
|---|---|
| `JumpListTool.exe` / `.cs` | C# 封装的 `ICustomDestinationList` 工具，核心执行体 |
| `check_jumplist.ps1` | RStudio 刷新脚本（分类 → 失败回退任务模式） |
| `refresh_vscode_jumplist.ps1` | VS Code 刷新脚本 + 覆盖检测 |
| `app_list.example.txt` | RStudio 项目列表模板（复制为 `app_list.txt` 后填写，已 gitignore） |
| `vscode_workspaces.example.txt` | VS Code 工作区列表模板（复制为 `vscode_workspaces.txt`，已 gitignore） |
| `run_hidden.vbs` | 无窗口调用 `check_jumplist.ps1`（消除任务窗口闪烁） |
| `run_hidden_vscode.vbs` | 无窗口调用 `refresh_vscode_jumplist.ps1` |
| `launch_remote.vbs` | 读取 `.vsremote` 内的 URI 并启动 VS Code 远程窗口 |
| `launch_rstudio_new.vbs` | 供 RStudio「New Window」条目调用：设 `RS_INITIAL_PROJECT=none` 启动空白新会话 |
| `docker.ico` / `docker.svg` | 远程（Dev Container）条目的图标 |
| `JumpList-Refresh-Rstudio.xml` | RStudio 计划任务（锁屏 / 解锁 +10s） |
| `JumpList-Refresh-VSCode.xml` | VS Code 计划任务（每分钟检测） |
| `workspaces\` | 自动生成的 `.code-workspace` / `.vsremote` 条目文件（已 gitignore） |
| `workspaces.hash` | `vscode_workspaces.txt` 的内容哈希，用于检测配置变更（已 gitignore） |
| `jumplist.log` | 运行日志（已 gitignore） |

## JumpListTool 用法

```
JumpListTool create      <aumid> <category> <file1> [file2] ...   加入自定义分类
JumpListTool createtasks <aumid> <file1> [file2] ...              加入任务区
JumpListTool createlinks <aumid> <exe> <target1> [target2] ...    shell link 任务（PKEY_Title 去扩展名；tooltip=PKEY_Description 显示项目路径；.vsremote 项自动用 wscript.exe+launch_remote.vbs）
JumpListTool delete      <aumid>                                  删除该应用的列表
JumpListTool setaumid    <lnk> <aumid>                             给 .lnk 写 AUMID
JumpListTool readaumid   <lnk>                                     读 .lnk 的 AUMID
```

重新编译：

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /out:JumpListTool.exe JumpListTool.cs
```

## 使用

本仓库不含个人配置。首次使用请从模板复制出配置文件（真实文件已在 `.gitignore` 中）：

```powershell
copy app_list.example.txt app_list.txt
copy vscode_workspaces.example.txt vscode_workspaces.txt
```

计划任务 XML 里的 `<WorkingDirectory>` 也需改成你自己的项目目录（默认是占位符 `C:\path\to\jump-list-refresh`）。

### RStudio

编辑 `app_list.txt`，每行一个 `.Rproj` 路径（`#` 为注释、空行忽略），保存后下次触发生效。手动执行：

```powershell
powershell -ExecutionPolicy Bypass -File ".\check_jumplist.ps1"
```

### VS Code

编辑 `vscode_workspaces.txt`：

- **本地文件夹**：直接写路径 → 生成 `workspaces\<文件夹名>.code-workspace`
- **远程工作区**：`vscode-remote://... |显示名` → 生成 `workspaces\<显示名>.vsremote`

手动执行：

```powershell
powershell -ExecutionPolicy Bypass -File ".\refresh_vscode_jumplist.ps1"
```

## 计划任务导入

当前账户属于 Administrators 且 UAC 过滤，**无法用命令行创建任务**（`Access is denied`），
需手动导入 `taskschd.msc`：

1. `Win+R` → `taskschd.msc`
2. 右侧「导入任务」，分别选择 `JumpList-Refresh-Rstudio.xml`、`JumpList-Refresh-VSCode.xml`

## 已知限制 / 踩坑记录

- **显示名与 tooltip 自定义**：所有命令（`create` / `createtasks` / `createlinks`）都通过 `IShellLinkW` 创建条目，
  再用 `IPropertyStore::SetValue` 设：
  - `PKEY_Title`（列表里显示的名字，取文件名去扩展名，如 `SEM`、`HMCDM_FORGETTING`）
  - `PKEY_Description`（hover tooltip）：
    - RStudio → `.Rproj` 完整文件路径
    - VS Code 本地 → `.code-workspace` 内 `folders[0].path`（实际项目文件夹）
    - VS Code 远程 → `.vsremote` 内的 URI
  同时设 `SetIconLocation`（本地用 `Code.exe` / `.Rproj` 文件图标，远程用 `docker.ico`）。
  早期版本 RStudio 用 `IShellItem`，无法自定义，显示名会带 `.Rproj`、tooltip 由系统生成。
- **exe 路径优先从注册表读取**：脚本先查 `HKCU` / `HKLM` 的
  `SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\<exe>`，得到真实可执行文件路径。
  若回退到 `Get-Command code`，拿到的常是 PATH 里的 `code.cmd`（包装脚本），
  会导致 Jump List 图标变成齿轮、`SetPath` 指向 `.cmd`。同理 RStudio 安装盘不在 C: 时，
  `${env:ProgramFiles}` 会指向错误位置。
- **路径尽量相对化**：`.vbs` 用 `WScript.ScriptFullName` 自定位脚本目录；
  PowerShell 脚本用 `$MyInvocation.MyCommand.Path` 取自身目录；`JumpListTool.cs` 用
  `Assembly.GetExecutingAssembly().Location` 定位 `docker.ico` 与 `launch_remote.vbs`。
  项目整体移动目录后，只需更新两个计划任务 XML 里的 `<WorkingDirectory>`（任务工作目录，无法相对）。
- **`IShellLink` 会破坏参数中的引号**：`SetArguments` 里若放带空格的绝对路径
  （如 `"D:\my apps\tool\launch.vbs"`），实际启动时会在空格处被截断，
  表现为 `wscript` 报 `"D:\my"没有文件扩展`。
  故 Jump List 条目一律用「相对脚本名 + `SetWorkingDirectory` 指向脚本目录」，
  参数中不含空格（`.vsremote` 条目与 `New Window` 条目都遵循此约定）。
- **PowerShell splat 会丢弃空字符串**：`@数组` 传给原生程序时空串被吞掉，
  导致后续参数错位（曾使 `New Window` 条目被静默丢弃）。
  故「无启动参数」用显式占位符 `(none)` 表示，在 C# 侧再转回空串。
- **文件夹不能加入 Jump List**：文件夹 AUMID 是全局的
  （`HKCR\Folder\AppUserModelID = Microsoft.Windows.Explorer`），改动会影响所有文件夹窗口。
  故本地工作区统一用 `.code-workspace` 文件代替。
- **`.vsremote` 必须无 BOM**：`Set-Content -Encoding UTF8` 会写入 UTF-8 BOM，
  导致 VBS `ReadLine()` 读到的 URI 首字符变成 65279（BOM），`Code.exe --folder-uri` 失败、打开空白窗口。
  现已改用 `[System.IO.File]::WriteAllText($f, $v, New-Object System.Text.UTF8Encoding $false)`，
  并在 `launch_remote.vbs` 中额外做 BOM 剥离。
- **覆盖检测的编码细节**：`CustomDestinations` 文件里条目路径以 UTF-8 存储，
  而 `refresh_vscode_jumplist.ps1` 按 UTF-16 解码后匹配 `.code-workspace`，仍能正确命中，逻辑不受影响。
- **列表文件位置**：`%APPDATA%\Microsoft\Windows\Recent\CustomDestinations\`
  （VS Code 对应 `1ced32d74a95c7bc.customDestinations-ms`）。


