@echo off
set target=%1

echo.
echo [34m***  COMPILATION (%target%)  ***[0m
echo.

cd src
rd /s /q bin obj >nul

REM dotnet build -c Release  
REM if %errorlevel% neq 0 goto KO

REM Remarque: mettre /p:DocumentationFile= pour ne pas generer le fichier xml pour la documentation
echo.
dotnet publish -c Release -r %target% -p:PublishSingleFile=true  --self-contained true -o ../dist/%target% 
if %errorlevel% neq 0 goto NOK
echo.
echo ============
echo ===  [32mOK[0m  ===
echo ============
echo.

cd ..
echo.
echo [34m***  Traiter I18n  ***[0m
echo.
xcopy /E /I /Q locales dist\%target%\locales
if %errorlevel% neq 0 goto NOK
 
echo.
echo ============
echo ===  [32mOK[0m  ===
echo ============
echo.
exit 0

:NOK
   echo.
   echo ===========
   echo === [91mNOK[0m ===
   echo ===========
   echo.
   exit 1

   