@echo off
echo === Hide N Seek ML-Agents Setup ===
echo.

REM Check Python
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python not found! Install Python 3.10+ first.
    pause
    exit /b 1
)

REM Create venv
echo [1/3] Creating virtual environment...
python -m venv venv

REM Activate
echo [2/3] Activating venv...
call .\venv\Scripts\activate.bat

REM Install packages
echo [3/3] Installing packages...
pip install mlagents torch numpy tensorboard

echo.
echo === Setup Complete! ===
echo.
echo Next steps:
echo 1. Open Unity and add ML-Agents package
echo 2. Run: .\venv\Scripts\activate
echo 3. Run: mlagents-learn Assets/Config/hideseek.yaml --run-id=test1
echo.
pause
