@echo off
setlocal

set "ROOT=%~dp0"
set "APP_EXE=%ROOT%src\LP2M_Bar_Mngt.WinForms\bin\Debug\net10.0-windows\LP2M_Bar_Mngt.WinForms.exe"

if not exist "%APP_EXE%" (
    echo Application WinForms non compilee. Compilation en cours...
    dotnet build "%ROOT%src\LP2M_Bar_Mngt.WinForms\LP2M_Bar_Mngt.WinForms.csproj"
    if errorlevel 1 (
        echo.
        echo La compilation a echoue.
        pause
        exit /b 1
    )
)

start "LP2M_Bar_Mngt WinForms" "%APP_EXE%"
exit /b 0
