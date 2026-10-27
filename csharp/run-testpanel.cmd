@echo off
chcp 65001 >nul
setlocal
set "HERE=%~dp0"
set "LOG=%HERE%testpanel.log"
set "DOTNET=dotnet"
where dotnet >nul 2>&1 || set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
echo [1/2] 編譯測試面板...
"%DOTNET%" build "%HERE%SolidCraft.TestPanel\SolidCraft.TestPanel.csproj" -c Release -nologo > "%LOG%" 2>&1
if errorlevel 1 goto :fail
echo [2/2] 啟動（SolidCraft 必須正在執行）...
start "" "%HERE%SolidCraft.TestPanel\bin\Release\net8.0-windows\SolidCraft.TestPanel.exe"
exit /b 0
:fail
echo 編譯失敗，請查看 csharp\testpanel.log。
pause
exit /b 1
