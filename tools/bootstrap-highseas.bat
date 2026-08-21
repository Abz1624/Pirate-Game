@echo off
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"
set PROJECT="%~dp0..\Pirate Game Unity"
%UNITY% -batchmode -quit -projectPath %PROJECT% -executeMethod HighSeas.Editor.HighSeasBootstrap.BootstrapAll -logFile "%~dp0bootstrap.log"
echo Bootstrap exit code: %ERRORLEVEL%
