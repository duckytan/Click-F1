@echo off
setlocal
chcp 65001 >nul

set "csc="
if exist "%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "csc=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)
if not defined csc (
    if exist "%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
        set "csc=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
)
if not defined csc (
    echo [!] .NET Framework csc.exe not found on this machine.
    pause
    exit /b 1
)

"%csc%" /nologo /target:winexe /out:"%~dp0Clicker-F1.exe" "%~dp0Clicker-F1.cs"
if errorlevel 1 (
    echo [!] Build failed.
    pause
    exit /b 1
)

echo [OK] Clicker-F1.exe built (small native exe, hotkey = F1).
echo      Double-click Clicker-F1.exe to launch the clicker.
pause
