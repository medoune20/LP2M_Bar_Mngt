@echo off
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\LP2M_Bar_Mngt.Web\LP2M_Bar_Mngt.Web.csproj"
set "APP_DLL=%ROOT%src\LP2M_Bar_Mngt.Web\bin\Debug\net10.0\LP2M_Bar_Mngt.Web.dll"
set "URL=http://localhost:5057"
set "BIND_URL=http://0.0.0.0:5057"

if not exist "%APP_DLL%" (
    echo Application web non compilee. Compilation en cours...
    dotnet build "%PROJECT%" /p:RestoreIgnoreFailedSources=true
    if errorlevel 1 (
        echo.
        echo La compilation a echoue.
        pause
        exit /b 1
    )
)

echo Demarrage de LP2M_Bar_Mngt Web sur %URL%
echo Acces reseau possible depuis un autre poste : http://ADRESSE_IP_DU_PC:5057
start "" "%URL%"
cd /d "%ROOT%src\LP2M_Bar_Mngt.Web"
dotnet "%APP_DLL%" --urls "%BIND_URL%"
