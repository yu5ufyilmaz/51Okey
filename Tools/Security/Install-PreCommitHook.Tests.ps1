$script = Join-Path $PSScriptRoot 'Install-PreCommitHook.ps1'
$psExe = (Get-Command powershell).Source
function NewRepo {
    $d = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $d | Out-Null
    & git -C $d init -q
    $d
}
function Install([string]$repo) {
    $out = & $psExe -NoProfile -ExecutionPolicy Bypass -File $script -RepoRoot $repo 2>&1 | Out-String
    @{ Code = $LASTEXITCODE; Out = $out }
}

Describe 'Install-PreCommitHook' {
    It 'hook dosyasini isaretle yazar (LF, BOM yok)' {
        $d = NewRepo
        (Install $d).Code | Should Be 0
        $hook = Join-Path $d '.git\hooks\pre-commit'
        Test-Path $hook | Should Be $true
        $bytes = [IO.File]::ReadAllBytes($hook)
        ($bytes[0] -eq 0xEF) | Should Be $false
        $text = [Text.Encoding]::UTF8.GetString($bytes)
        $text | Should Match 'secret-scan-hook: Install-PreCommitHook.ps1'
        $text | Should Match 'Check-Secrets.ps1'
        $text.Contains("`r") | Should Be $false
        $text.StartsWith('#!/bin/sh') | Should Be $true
    }
    It 'yabanci hook uzerine yazmaz, 1 doner' {
        $d = NewRepo
        $hook = Join-Path $d '.git\hooks\pre-commit'
        New-Item -ItemType Directory -Force -Path (Split-Path $hook) | Out-Null
        [IO.File]::WriteAllText($hook, "#!/bin/sh`nexit 0`n")
        (Install $d).Code | Should Be 1
        [IO.File]::ReadAllText($hook) | Should Be "#!/bin/sh`nexit 0`n"
    }
    It 'kendi hook unun ustune ikinci kez calisir' {
        $d = NewRepo
        (Install $d).Code | Should Be 0
        (Install $d).Code | Should Be 0
    }
}
