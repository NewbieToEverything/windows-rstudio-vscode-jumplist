Set sh = CreateObject("WScript.Shell")
d = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
sh.Run "powershell.exe -WindowStyle Hidden -NonInteractive -ExecutionPolicy Bypass -File """ & d & "refresh_vscode_jumplist.ps1""", 0, False
