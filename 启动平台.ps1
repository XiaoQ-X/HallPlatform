$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not $env:PORT) { $env:PORT = '8182' }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw '请先安装 Node.js 22.13 或更新版本。' }
if (-not (Test-Path -LiteralPath 'dist/index.html')) { throw '请先按 README 安装依赖并构建前端。' }
node --experimental-sqlite server/index.js
