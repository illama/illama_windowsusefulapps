@echo off
:: ============================================================================
:: Lanceur pour le Gestionnaire Systeme Windows Unifie v1.0.3
:: ============================================================================

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Relancement en tant qu'administrateur...
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "SCRIPT_DIR=%~dp0"
set "PS1_FILE="

for %%f in ("%SCRIPT_DIR%illama_windowsusefulapps*.ps1") do (
    set "PS1_FILE=%%f"
    goto :found
)

:notfound
echo ERREUR: Aucun fichier PowerShell trouve dans le dossier actuel.
echo.
echo Veuillez vous assurer qu'un fichier "illama_windowsusefulapps_vX.X.X.ps1"
echo se trouve dans le meme dossier que ce fichier BAT.
echo.
pause
exit /b 1

:found
echo Lancement du Gestionnaire Systeme Windows...
echo Fichier detecte: %PS1_FILE%
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1_FILE%"

if %errorLevel% neq 0 (
    echo.
    echo Une erreur s'est produite lors de l'execution du script.
    pause
)
