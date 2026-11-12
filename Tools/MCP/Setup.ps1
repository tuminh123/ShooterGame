$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$toolsRoot = Join-Path $projectRoot '.mcp-tools'
$uvPath = Join-Path $toolsRoot 'uv/uv.exe'
$uvVersion = '0.13.7'
$uvSha256 = '0cdc5b38fe5a8168ed6a25814f5ba43c21d313dd3187314fccb37b7cec46e021'

if (-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITEW6432 -eq 'ARM64' -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') {
    throw 'This bootstrap supports Windows x64. See Docs/Tools/Unity-MCP.md for other operating systems.'
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Install Git from https://git-scm.com/downloads and reopen the terminal first.'
}

if (-not (Test-Path -LiteralPath $uvPath)) {
    New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
    $archive = Join-Path $toolsRoot 'uv.zip'
    $url = "https://github.com/astral-sh/uv/releases/download/$uvVersion/uv-x86_64-pc-windows-msvc.zip"
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $uvSha256) {
        throw 'uv archive SHA-256 mismatch. The downloaded archive will not be executed.'
    }
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $toolsRoot 'uv') -Force
}

# Keep downloaded runtimes and caches inside this checkout, outside Git.
$env:UV_CACHE_DIR = Join-Path $toolsRoot 'cache'
$env:UV_PYTHON_INSTALL_DIR = Join-Path $toolsRoot 'python'
$env:UNITY_MCP_DISABLE_TELEMETRY = 'true'
# Upstream contains long test paths; enable long paths for this Git process only.
$env:GIT_CONFIG_PARAMETERS = "$env:GIT_CONFIG_PARAMETERS 'core.longpaths=true'".Trim()
& $uvPath sync --project $PSScriptRoot --locked --no-dev --python 3.11
if ($LASTEXITCODE -ne 0) { throw 'MCP dependency installation failed.' }
Write-Host 'MCP tools are ready. Run Tools/MCP/Start-Server.ps1, then connect Unity to http://127.0.0.1:8080.'
Write-Host 'If Unity is already open: Shooter Game > MCP > Refresh Local Tools, then Refresh in MCP Setup.'
