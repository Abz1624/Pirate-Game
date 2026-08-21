@echo off
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"
set PROJECT="%~dp0..\Pirate Game Unity"
%UNITY% -batchmode -projectPath %PROJECT% -executeMethod HighSeas.Editor.HighSeasBootstrap.BootstrapAll -logFile "%~dp0bootstrap.log"
%UNITY% -batchmode -projectPath %PROJECT% -runTests -testPlatform editmode -assemblyNames "HighSeas.Tests.EditMode" -testResults "%~dp0editmode-results.xml" -logFile "%~dp0tests.log"
echo Test exit code: %ERRORLEVEL%
