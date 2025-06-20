@echo off
setlocal EnableDelayedExpansion

REM ---------- configuration ----------
set "OUT=cs_file_paths.txt"
set "ROOT=%~dp0"
REM strip trailing back-slash, if any:
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
REM ------------------------------------

REM work inside the Assets folder
pushd "%ROOT%" || (echo [ERROR] Could not change to "%ROOT%".& pause& goto :eof)

(
    echo --- .CS FILE PATHS ---
    echo.
    for /R %%F in (*.cs,*.asset) do (
        set "FULL=%%~fF"
        REM knock the whole ROOT\ part off:
        set "REL=!FULL:%ROOT%\=!"
        echo !REL!
    )
) > "%OUT%"

if exist "%SystemRoot%\System32\clip.exe" (
    type "%OUT%" | clip
    echo List of .cs files copied to clipboard.
) else (
    echo [INFO] clip.exe not found – list saved in "%OUT%".
)

popd
endlocal
pause
