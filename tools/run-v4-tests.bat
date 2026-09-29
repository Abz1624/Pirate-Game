@echo off
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"
set PROJECT="%~dp0..\Pirate Game Unity"
%UNITY% -batchmode -projectPath %PROJECT% -runTests -testPlatform editmode -assemblyNames "RoguePirates.V4.Tests.EditMode;RoguePirates.Tests.EditMode" -testResults "%~dp0editmode-results.xml" -logFile "%~dp0tests.log"
echo EditMode test exit code: %ERRORLEVEL%
%UNITY% -batchmode -projectPath %PROJECT% -runTests -testPlatform playmode -assemblyNames "RoguePirates.V4.Tests.PlayMode" -testResults "%~dp0playmode-results.xml" -logFile "%~dp0tests-playmode.log"
echo PlayMode test exit code: %ERRORLEVEL%
