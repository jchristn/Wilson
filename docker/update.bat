@echo off
setlocal

cd /d "%~dp0"
docker compose down && docker compose build && docker compose up -d && docker ps -a
