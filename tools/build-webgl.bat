@echo off
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"
set PROJECT="%~dp0..\Pirate Game Unity"
%UNITY% -batchmode -projectPath %PROJECT% -executeMethod RoguePirates.V4.Editor.V4BuildSettings.BuildWebGLFromCommandLine -logFile "%~dp0webgl-build.log"
echo WebGL build exit code: %ERRORLEVEL%
