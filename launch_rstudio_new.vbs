' launch_rstudio_new.vbs [rstudio.exe]
' 启动一个「空白新会话」的 RStudio：
'   - 设置 RS_INITIAL_PROJECT=none。这是 RStudio 源码里的官方约定
'     （src/node/desktop/src/core/r-user-data.ts: kProjectNone = 'none'），
'     表示"新会话不加载任何项目"，从而避免继承上次打开的项目；
'   - 不传 .rproj 参数，使其走 application.ts 中 shouldInstanceOpen() 的默认分支，
'     从而开出一个新实例而不是复用已有窗口。
Option Explicit

Dim fso, sh, scriptDir, exePath
Set fso = CreateObject("Scripting.FileSystemObject")
Set sh = CreateObject("WScript.Shell")

scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)

' rstudio.exe 路径：命令行参数优先，其次注册表 App Paths，最后 %ProgramFiles% 兜底
If WScript.Arguments.Count > 0 Then
    If Len(Trim(WScript.Arguments(0))) > 0 Then
        exePath = Trim(WScript.Arguments(0))
    End If
End If

If Len(exePath) = 0 Then
    On Error Resume Next
    exePath = sh.RegRead("HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rstudio.exe\")
    If Len(exePath) = 0 Then exePath = sh.RegRead("HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\rstudio.exe\")
    On Error Goto 0
End If

If Len(exePath) = 0 Then exePath = sh.ExpandEnvironmentStrings("%ProgramFiles%\RStudio\rstudio.exe")

If Not fso.FileExists(exePath) Then
    MsgBox "找不到 rstudio.exe:" & vbCrLf & exePath, vbExclamation, "RStudio New Window"
    WScript.Quit 1
End If

' 只对本次启动的子进程生效，不污染系统/用户环境变量
Dim procEnv
Set procEnv = sh.Environment("PROCESS")
procEnv("RS_INITIAL_PROJECT") = "none"

sh.CurrentDirectory = scriptDir
sh.Run """" & exePath & """", 1, False