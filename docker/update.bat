@echo off
REM Wilson stack update entry point. Tears the stack down, pulls the latest published
REM images, brings it back up detached, then lists all containers. Run from anywhere;
REM paths are anchored to this script's directory so Compose always uses docker\compose.yaml.
setlocal
pushd "%~dp0"

echo [Wilson] Stopping the stack...
docker compose down --remove-orphans

REM Compose uses fixed container_name values, so a leftover container (from a crash or a
REM concurrent compose run) can survive `down` and then collide on `up` with
REM "container name is already in use". Force-remove any such leftovers by the names this
REM compose file declares before starting.
echo [Wilson] Clearing any leftover fixed-name containers...
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "(docker compose config --format json | ConvertFrom-Json).services.PSObject.Properties.Value | ForEach-Object { $_.container_name } | Where-Object { $_ }" 2^>nul`) do docker rm -f %%c >nul 2>&1

echo [Wilson] Pulling images...
docker compose pull
if errorlevel 1 goto :error

echo [Wilson] Starting the stack (detached)...
docker compose up -d --remove-orphans
if errorlevel 1 goto :error

echo [Wilson] Containers:
docker ps -a

popd
endlocal
exit /b 0

:error
echo [Wilson] Update failed with error code %errorlevel%.
popd
endlocal
exit /b 1
