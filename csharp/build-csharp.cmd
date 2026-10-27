@echo off
chcp 65001 >nul
setlocal
set "HERE=%~dp0"
set "LOG=%HERE%build.log"
echo ==== SolidCraft.Net 編譯 ==== > "%LOG%"
echo [%date% %time%] start >> "%LOG%"

echo [1/3] 檢查 .NET SDK...
set "DOTNET=dotnet"
where dotnet >nul 2>&1 && goto :dotnet_ok
if exist "%ProgramFiles%\dotnet\dotnet.exe" goto :dotnet_path
echo     未安裝，開始安裝 .NET 8 SDK（可能跳出系統管理員權限視窗，請按「是」）
echo [step1] installing .NET SDK >> "%LOG%"
winget install -e --id Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements >> "%LOG%" 2>&1
if not exist "%ProgramFiles%\dotnet\dotnet.exe" goto :fail_dotnet
:dotnet_path
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
:dotnet_ok
"%DOTNET%" --version >> "%LOG%" 2>&1
echo [step1] dotnet ok >> "%LOG%"

echo [2/3] 編譯 SolidCraft.Net 與範例...
echo [step2] build start %time% >> "%LOG%"
"%DOTNET%" build "%HERE%SolidCraft.Net.sln" -c Release -nologo >> "%LOG%" 2>&1
if errorlevel 1 goto :fail_build
echo [step2] BUILD OK %time% >> "%LOG%"

echo [3/3] 執行範例（SolidCraft 必須正在執行）...
echo [step3] sample start >> "%LOG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '%DOTNET%' '%HERE%SolidCraft.Sample\bin\Release\net8.0\SolidCraft.Sample.dll' 2>&1 | Tee-Object -FilePath '%LOG%' -Append; exit $LASTEXITCODE"
if errorlevel 1 goto :fail_sample
echo [step3] SAMPLE OK >> "%LOG%"
echo 完成。組件在 csharp\SolidCraft.Net\bin\Release\net8.0\SolidCraft.Net.dll
pause
exit /b 0

:fail_dotnet
echo [FAIL] dotnet install >> "%LOG%"
echo .NET SDK 安裝失敗，請查看 csharp\build.log。
pause
exit /b 1
:fail_build
echo [FAIL] build >> "%LOG%"
echo 編譯失敗，請查看 csharp\build.log。
pause
exit /b 1
:fail_sample
echo [FAIL] sample >> "%LOG%"
echo 範例執行失敗（SolidCraft 有開著嗎？），請查看 csharp\build.log。
pause
exit /b 1
