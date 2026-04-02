@echo off


echo ========================================
echo   FeeQuery - Docker Compose Build
echo ========================================
docker-compose up -d --build
if %errorlevel% neq 0 (
    echo [ERROR] docker-compose failed, exit code: %errorlevel%
    exit /b %errorlevel%
)

echo.
echo ========================================
echo   Started successfully!
echo ========================================
echo   URL   : http://localhost:8080
echo.
echo   Logs  : docker-compose logs -f
echo   Stop  : docker-compose down
echo   Clean : docker-compose down -v
echo ========================================
