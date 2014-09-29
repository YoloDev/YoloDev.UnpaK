@echo off
cd %~dp0

SETLOCAL ENABLEEXTENSIONS
SET CACHED_NUGET=%LocalAppData%\NuGet\NuGet.exe

IF EXIST %CACHED_NUGET% goto copynuget
echo Downloading latest version of NuGet.exe...
IF NOT EXIST %LocalAppData%\NuGet md %LocalAppData%\NuGet
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest 'https://www.nuget.org/nuget.exe' -OutFile '%CACHED_NUGET%'"

:copynuget
IF EXIST .nuget\nuget.exe goto restore
md .nuget
copy %CACHED_NUGET% .nuget\nuget.exe > nul

:restore
IF EXIST packages\KoreBuild goto run
.nuget\NuGet.exe install KoreBuild -ExcludeVersion -o packages -nocache -pre
.nuget\NuGet.exe install Sake -version 0.2 -o packages -ExcludeVersion

IF "%SKIP_KRE_INSTALL%"=="1" goto run
CALL packages\KoreBuild\build\kvm upgrade -runtime CLR -x86
CALL packages\KoreBuild\build\kvm install default -runtime CoreCLR -x86

:run
CALL packages\KoreBuild\build\kvm use default -runtime CLR -x86
packages\Sake\tools\Sake.exe -I packages\KoreBuild\build -f makefile.shade %*

:publish
IF NOT "%ERRORLEVEL%" == "0" goto end

IF "%K_BUILD_VERSION%" == "" goto noversion
IF "%APPVEYOR_BUILD_VERSION%" == "" goto noversion
IF NOT "%APPVEYOR_REPO_BRANCH%" == "master" goto wrongbranch
IF NOT "%APPVEYOR_PULL_REQUEST_NUMBER%" == "" goto pullreq
IF "%NUGET_SOURCE%" == "" goto end
echo Publishing packages
for %%x in ("%~dp0artifacts\build\*-%K_BUILD_VERSION%.nupkg") do (
	echo Publishing "%%x"
	%~dp0.nuget\NuGet.exe push "%%x" "%NUGET_API_KEY%" -Source "%NUGET_SOURCE%"
)

REM Symbol packages doesn't work right now, due to the fact that I use source packages (see build 2)
REM IF "%SYMBOL_SOURCE%" == "" goto end
REM for %%x in ("%~dp0artifacts\build\*-%K_BUILD_VERSION%.symbols.nupkg") do (
REM 	echo Publishing "%%x"
REM 	%~dp0.nuget\NuGet.exe push "%%x" "%SYMBOL_API_KEY%" -Source "%SYMBOL_SOURCE%"
REM )
goto end

:wrongbranch
echo Skipping commit since branch = %APPVEYOR_REPO_NAME%
goto end

:pullreq
echo Skipping commit since it's a pull request
goto end

:noversion
echo Skipping commit since no version was provided
goto end

:end
exit /b %ERRORLEVEL%
