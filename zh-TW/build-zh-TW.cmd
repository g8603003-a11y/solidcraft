@echo off
chcp 65001 >nul
setlocal
rem 這個腳本放在 repo 的 zh-TW 資料夾；SRC 是 repo 根目錄
for %%I in ("%~dp0..") do set "SRC=%%~fI"
set "LOG=%~dp0build.log"
set "CARGO=%USERPROFILE%\.cargo\bin\cargo.exe"
set "RUSTUP=%USERPROFILE%\.cargo\bin\rustup.exe"
echo ==== SolidCraft 繁體中文版編譯 ==== > "%LOG%"
echo [%date% %time%] start >> "%LOG%"

echo [1/4] 檢查 Visual Studio C++ 編譯工具...
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "HAVEVC="
if exist "%VSWHERE%" for /f "usebackq delims=" %%i in (`"%VSWHERE%" -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "HAVEVC=%%i"
if defined HAVEVC goto :vc_ok
echo     未安裝，開始安裝（會跳出系統管理員權限視窗，請按「是」，約 10-20 分鐘）
echo [step1] installing VS Build Tools >> "%LOG%"
winget install -e --id Microsoft.VisualStudio.2022.BuildTools --accept-source-agreements --accept-package-agreements --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended" >> "%LOG%" 2>&1
goto :vc_done
:vc_ok
echo     已安裝
echo [step1] VC tools present >> "%LOG%"
:vc_done

echo [2/4] 檢查 Rust...
if exist "%RUSTUP%" goto :rust_ok
echo     未安裝，開始安裝 Rust
echo [step2] installing rustup >> "%LOG%"
winget install -e --id Rustlang.Rustup --accept-source-agreements --accept-package-agreements >> "%LOG%" 2>&1
if exist "%RUSTUP%" goto :rust_ok
echo [step2] FAILED rustup not found >> "%LOG%"
echo Rust 安裝失敗，請查看 zh-TW\build.log。
pause
exit /b 1
:rust_ok
"%RUSTUP%" default stable >> "%LOG%" 2>&1

echo [3/4] 編譯 SolidCraft（第一次約 10-60 分鐘，請勿關閉此視窗）...
echo [step3] cargo build start %time% >> "%LOG%"
cd /d "%SRC%"
"%CARGO%" build --release -p solidcraft >> "%LOG%" 2>&1
if errorlevel 1 goto :build_fail
echo [step3] BUILD OK %time% >> "%LOG%"

echo [4/4] 完成！啟動繁體中文版 SolidCraft...
echo [step4] launching >> "%LOG%"
start "" "%SRC%\target\release\solidcraft.exe"
echo [%date% %time%] done >> "%LOG%"
echo 完成。執行檔位置：%SRC%\target\release\solidcraft.exe
pause
exit /b 0

:build_fail
echo [step3] FAILED >> "%LOG%"
echo 編譯失敗，請查看 zh-TW\build.log。
pause
exit /b 1
