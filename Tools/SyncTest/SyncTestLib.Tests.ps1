# Pester 3.4: Invoke-Pester -Script Tools\SyncTest\SyncTestLib.Tests.ps1
. "$PSScriptRoot\SyncTestLib.ps1"

$Nicks = @('AT-host', 'AT-g1', 'AT-g2', 'AT-g3')

function New-Status($nick, $state = 'Done', $errors = @(), $desync = 0, $first = '', $ok = 30, $isMyTurn = $false) {
    [pscustomobject]@{
        nickName = $nick; state = $state; deal = 1; myMoves = 6; errors = $errors; isMyTurn = $isMyTurn
        sync = [pscustomobject]@{ ok = $ok; desync = $desync; first = $first }
    }
}

function New-Statuses { $Nicks | ForEach-Object { New-Status $_ } }

Describe 'Get-SyncTestVerdict' {
    It 'passes when every client is Done with no desync and no errors' {
        $v = Get-SyncTestVerdict -Statuses (New-Statuses) -Nicks $Nicks -TimedOut $false
        $v.Pass | Should Be $true
    }

    It 'fails with DESYNC when the host counted a desync' {
        $s = New-Statuses
        $s[0] = New-Status 'AT-host' -desync 1 -first 'x'
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false
        $v.Pass | Should Be $false
        $v.Reason | Should Match 'DESYNC'
    }

    It 'names the client and its first error when it is in Error' {
        $s = New-Statuses
        $s[2] = New-Status 'AT-g2' -state 'Error' -errors @('boom')
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false
        $v.Pass | Should Be $false
        $v.Reason | Should Match 'AT-g2'
        $v.Reason | Should Match 'boom'
    }

    It 'names the stuck client and its state on timeout' {
        $s = New-Statuses
        $s[3] = New-Status 'AT-g3' -state 'Playing'
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $true
        $v.Pass | Should Be $false
        $v.Reason | Should Match 'AT-g3.*Playing'
    }

    It 'names a client whose status is missing' {
        $s = New-Statuses
        $s[1] = $null
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false
        $v.Pass | Should Be $false
        $v.Reason | Should Match 'AT-g1'
    }

    It 'reports DESYNC before a client Error' {
        $s = New-Statuses
        $s[0] = New-Status 'AT-host' -desync 1 -first 'x'
        $s[1] = New-Status 'AT-g1' -state 'Error' -errors @('boom')
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false
        $v.Reason | Should Match 'DESYNC'
    }

    It 'reports the client Error, not the guests that never started' {
        $s = New-Statuses
        $s[1] = New-Status 'AT-g1' -state 'Error' -errors @('CreateRoom: x')
        $s[2] = $null
        $s[3] = $null
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false
        $v.Reason | Should Match 'AT-g1: Error: CreateRoom'
    }

    It 'names the client whose turn is stuck on timeout' {
        $s = $Nicks | ForEach-Object { New-Status $_ -state 'Playing' }
        $s[2] = New-Status 'AT-g2' -state 'Playing' -isMyTurn $true
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $true
        $v.Reason | Should Match 'AT-g2'
    }

    It 'fails when the host compared nothing' {
        $s = New-Statuses
        $s[0] = New-Status 'AT-host' -ok 0
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false
        $v.Pass | Should Be $false
    }
}

Describe 'Test-EditorStopped' {
    It 'accepts only a stopped editor' {
        Test-EditorStopped ([pscustomobject]@{ playMode = 'stopped' }) | Should Be $true
        Test-EditorStopped ([pscustomobject]@{ playMode = 'paused' }) | Should Be $false
        Test-EditorStopped ([pscustomobject]@{ playMode = 'playing' }) | Should Be $false
        Test-EditorStopped $null | Should Be $false
    }
}

Describe 'Get-DefaultTimeoutSec' {
    It 'scales with deals' {
        Get-DefaultTimeoutSec 1 | Should Be 360
        Get-DefaultTimeoutSec 3 | Should Be 840
    }
}

Describe 'Read-SyncStatus' {
    $project = Join-Path $TestDrive 'p'
    $dir = Join-Path $project 'Temp\SyncAutoTest'
    New-Item -ItemType Directory -Force $dir | Out-Null

    It 'returns null for a half-written file' {
        [IO.File]::WriteAllText((Join-Path $dir 'status.json'), '{"state":')
        Read-SyncStatus -Project $project | Should Be $null
    }

    It 'reads a valid file' {
        [IO.File]::WriteAllText((Join-Path $dir 'status.json'), '{"state":"Playing"}')
        (Read-SyncStatus -Project $project).state | Should Be 'Playing'
    }
}

Describe 'Get-FirstBugKey' {
    It 'returns the key of the first [BUG] error' {
        $s = New-Statuses
        $s[2] = New-Status 'AT-g2' -errors @('NullReference @ X', '[BUG] 1.21 AT-g2: at r9 beklenen que3 +250 gözlenen ceza yok')
        Get-FirstBugKey -Statuses $s | Should Be '1.21'
    }

    It 'returns null when there is no bug' {
        Get-FirstBugKey -Statuses (New-Statuses) | Should Be $null
    }
}

Describe 'Get-JournalExcerpt' {
    It 'returns the lines of every journal at that key' {
        $a = Join-Path $TestDrive 'a.log'
        $b = Join-Path $TestDrive 'b.log'
        [IO.File]::WriteAllText($a, "1.20 | AT-host que1 | x`n1.21 | AT-host que1 | y`n")
        [IO.File]::WriteAllText($b, "1.21 | AT-g1 que2 | z`n1.22 | AT-g1 que2 | w`n")
        $lines = @(Get-JournalExcerpt -Paths @($a, $b, (Join-Path $TestDrive 'missing.log')) -Key '1.21')
        $lines.Count | Should Be 2
        $lines[0] | Should Match 'AT-host.*y'
        $lines[1] | Should Match 'AT-g1.*z'
    }
}
