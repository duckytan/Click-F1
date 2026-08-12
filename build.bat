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
if exist Click.exe (
    "%CSC%" /nologo /target:winexe /out:Clicker-F1.exe Clicker-F1.cs /resource:Click.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
) else (
    echo [!] 未找到 Click.exe，将编译为“不含内嵌 Click.exe”的版本（运行时会要求同目录放 Click.exe）。
    "%CSC%" /nologo /target:winexe /out:Clicker-F1.exe Clicker-F1.cs /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll
)
if %ERRORLEVEL% neq 0 (
    echo [!] Build failed.
    pause
    exit /b 1
)

echo [OK] Build succeeded: Clicker-F1.exe
pause
