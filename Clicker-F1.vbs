Set ws = CreateObject("WScript.Shell")
Dim ps
ps = Replace(WScript.ScriptFullName, ".vbs", ".ps1")
ws.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & ps & """", 0, False
