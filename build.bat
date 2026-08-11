@echo off
chcp 65001 >nul
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo [!] csc.exe not found. Please install .NET Framework 4.x.
    pause
    exit /b 1
)

echo [*] Compiling Clicker-F1.exe ...
"%CSC%" /nologo /target:winexe /out:Clicker-F1.exe Clicker-F1.cs /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
if %ERRORLEVEL% neq 0 (
    echo [!] Build failed.
    pause
    exit /b 1
)

echo [OK] Build succeeded: Clicker-F1.exe
pause
