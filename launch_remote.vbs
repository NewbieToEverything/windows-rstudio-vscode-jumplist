' launch_remote.vbs <uri-file>
' Reads the first line of the file (a vscode-remote URI) and launches VS Code.
Dim fso, f, uri, sh
Set fso = CreateObject("Scripting.FileSystemObject")
Set f = fso.OpenTextFile(WScript.Arguments(0), 1)
uri = Trim(f.ReadLine)
f.Close
' Strip BOM if present
If Len(uri) > 0 Then
    If AscW(Left(uri, 1)) = 65279 Then uri = Mid(uri, 2)
End If
Set sh = CreateObject("WScript.Shell")
codeExe = ""
On Error Resume Next
codeExe = sh.RegRead("HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Code.exe\")
On Error Goto 0
If codeExe = "" Then codeExe = "Code.exe"
sh.Run """" & codeExe & """ --folder-uri """ & uri & """", 0, False
