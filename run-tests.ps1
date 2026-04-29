$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host 'Running Node.js tests...' -ForegroundColor Cyan
Push-Location (Join-Path $repoRoot 'nodejs')
try {
    npm test
}
finally {
    Pop-Location
}

Write-Host 'Running .NET tests...' -ForegroundColor Cyan
Push-Location (Join-Path $repoRoot 'dotnet/CopilotX.Tests')
try {
    dotnet run
}
finally {
    Pop-Location
}

Write-Host 'All tests passed.' -ForegroundColor Green
