@echo off
start pwsh -NoLogo -Command "$env:ANTHROPIC_BASE_URL='https://api.deepseek.com/anthropic'; $env:ANTHROPIC_AUTH_TOKEN='sk-f41daf457a21496788a68b6f233d561b'; $env:ANTHROPIC_MODEL='deepseek-chat'; $env:ANTHROPIC_DEFAULT_HAIKU_MODEL='deepseek-chat'; $env:API_TIMEOUT_MS='600000'; $env:CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC='1'; $env:ANTHROPIC_API_KEY=$null; Set-Location '%~dp0'; claude"
