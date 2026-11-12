# Check-Secrets.ps1'i git pre-commit hook'u olarak kurar. Kurmak kullanıcının kararıdır; bu betik kendiliğinden çalışmaz.
# Kullanım: Install-PreCommitHook.ps1 [-RepoRoot <depo>]   (varsayılan: bu betiğin deposu)
param([string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).ProviderPath)

$marker = '# secret-scan-hook: Install-PreCommitHook.ps1'
$hookText = @(
    '#!/bin/sh'
    $marker
    'cd "$(git rev-parse --show-toplevel)" || exit 1'
    'powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Security/Check-Secrets.ps1'
    'status=$?'
    'if [ "$status" -ne 0 ]; then'
    '    echo "pre-commit: secret scan failed (exit $status), commit blocked"'
    '    exit 1'
    'fi'
    'exit 0'
) -join "`n"

try {
    $gitDir = (& git -C $RepoRoot rev-parse --absolute-git-dir)
    if ($LASTEXITCODE -ne 0) { throw 'not a git repository' }
    $hooksDir = Join-Path $gitDir 'hooks'
    $hook = Join-Path $hooksDir 'pre-commit'
    if ((Test-Path -LiteralPath $hook) -and -not ([IO.File]::ReadAllText($hook).Contains($marker))) {
        Write-Output "REFUSED: $hook exists and was not written by this installer; not overwritten"
        exit 1
    }
    New-Item -ItemType Directory -Force -Path $hooksDir | Out-Null
    # LF ve BOM'suz: sh betiği CRLF/BOM ile çalışmaz.
    [IO.File]::WriteAllText($hook, $hookText + "`n", (New-Object Text.UTF8Encoding $false))
    Write-Output "installed: $hook (runs Tools/Security/Check-Secrets.ps1 before each commit)"
    exit 0
} catch {
    Write-Output "ERROR: $($_.Exception.Message)"
    exit 2
}
