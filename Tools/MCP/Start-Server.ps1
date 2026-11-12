$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$server = Join-Path $PSScriptRoot '.venv/Scripts/mcp-for-unity.exe'
if (-not (Test-Path -LiteralPath $server)) {
    throw 'Run Tools/MCP/Setup.ps1 once on this machine before starting the MCP server.'
}

# Only this process and its children receive these settings.
$env:UNITY_MCP_DISABLE_TELEMETRY = 'true'
$env:UNITY_MCP_LOG_DIR = Join-Path $projectRoot 'Logs/MCP'
& $server --transport http --http-host 127.0.0.1 --http-url http://127.0.0.1:8080 --default-instance ShooterGame
exit $LASTEXITCODE
