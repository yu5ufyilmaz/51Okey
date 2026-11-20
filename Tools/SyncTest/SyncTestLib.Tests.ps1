# Pester 3.4: Invoke-Pester -Script Tools\SyncTest\SyncTestLib.Tests.ps1
. "$PSScriptRoot\SyncTestLib.ps1"

$Nicks = @('AT-host', 'AT-g1', 'AT-g2', 'AT-g3')

function New-Status($nick, $state = 'Done', $errors = @(), $desync = 0, $first = '', $ok = 30, $isMyTurn = $false) {
    [pscustomobject]@{
        nickName = $nick; state = $state; deal = 1; myMoves = 6; errors = $errors; isMyTurn = $isMyTurn
        sync = [pscustomobject]@{ ok = $ok; desync = $desync; first = $first }
    }
}

function New-DropStatus($nick, $state = 'Done', $drops = 0, $grants = 0, $lines = @('[SKOR] el 1: 1=0 2=5 3=0 4=0'), $over = $true) {
    $s = New-Status $nick $state
    $s | Add-Member drops $drops; $s | Add-Member rejoinGrants $grants; $s | Add-Member scoreLines $lines
    $s | Add-Member gameOverSeen $over; $s | Add-Member restoreEntered $false
    $s | Add-Member isMaster ($nick -eq 'AT-host'); $s | Add-Member ctlBotLines 1; $s | Add-Member gerialLines 0
    $s
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

    It 'passes in join mode, where no editor is master and nothing is compared' {
        $bots = @('AT-g1', 'AT-g2', 'AT-g3')
        $s = $bots | ForEach-Object { New-Status $_ -ok 0 }
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $bots -TimedOut $false -NoHost
        $v.Pass | Should Be $true
    }

    It 'still fails on a bot error in join mode' {
        $bots = @('AT-g1', 'AT-g2', 'AT-g3')
        $s = $bots | ForEach-Object { New-Status $_ -ok 0 }
        $s[1] = New-Status 'AT-g2' -ok 0 -errors @('[BUG] 1.4 AT-g2: x')
        $v = Get-SyncTestVerdict -Statuses $s -Nicks $bots -TimedOut $false -NoHost
        $v.Pass | Should Be $false
        $v.Reason | Should Match 'BUG'
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

Describe 'Get-SyncTestVerdict -DropEditors' {
    function New-DropSet { @((New-DropStatus 'AT-host'), (New-DropStatus 'AT-g1' -drops 1 -grants 1), (New-DropStatus 'AT-g2'), (New-DropStatus 'AT-g3')) }
    function Get-V($s, $mode = 'return', $drop = @(1), $scn = '') { Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false -DropEditors $drop -DropMode $mode -DropScenario $scn }

    It 'passes on return with a grant and equal score lines' {
        (Get-V (New-DropSet)).Pass | Should Be $true
    }
    It 'fails when no rejoin grant was seen' {
        $s = New-DropSet; $s[1].rejoinGrants = 0
        $v = Get-V $s
        $v.Pass | Should Be $false
        $v.Reason | Should Match 'geri verme yok'
    }
    It 'adds restore mode to the reason' {
        $s = New-DropSet; $s[1].rejoinGrants = 0; $s[1].restoreEntered = $true
        (Get-V $s).Reason | Should Match 'kurulum modunda kaldı'
    }
    It 'fails when a finisher has different score lines' {
        $s = New-DropSet; $s[2].scoreLines = @('[SKOR] el 1: 1=0 2=9 3=0 4=0')
        (Get-V $s).Reason | Should Match 'skor farklı'
    }
    It 'passes noreturn when the dropped editor is Gone, ignoring its state and lines' {
        $s = New-DropSet; $s[1] = New-DropStatus 'AT-g1' -state 'Gone' -drops 1 -lines @()
        (Get-V $s 'noreturn').Pass | Should Be $true
    }
    It 'does not time out on a Gone editor under noreturn' {
        $s = New-DropSet; $s[1] = New-DropStatus 'AT-g1' -state 'Gone' -drops 1
        (Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $true -DropEditors @(1) -DropMode 'noreturn').Pass | Should Be $true
    }
    It 'fails afterend when game over was not seen' {
        $s = New-DropSet; $s[1].gameOverSeen = $false
        (Get-V $s 'afterend').Reason | Should Match 'oyun sona ermiş görülmedi'
    }
    It 'sums desync over all editors, not just the host' {
        $s = New-DropSet
        $s[2] = New-DropStatus 'AT-g2'; $s[2].sync.desync = 1; $s[2].sync.first = 'x'
        (Get-V $s).Reason | Should Match 'DESYNC'
    }
    It 'fails when the scenario never fired' {
        $s = New-DropSet; $s[1].drops = 0
        (Get-V $s).Reason | Should Match 'senaryo tetiklenmedi'
    }
    It 'accepts a master drop: ok comes from the new master' {
        $s = New-DropSet; $s[0] = New-DropStatus 'AT-host' -drops 1 -grants 1
        $s[0].sync.ok = 0; $s[2].sync.ok = 20; $s[0].isMaster = $false; $s[2].isMaster = $true
        (Get-V $s 'return' @(0)).Pass | Should Be $true
    }
}

Describe 'Get-DropVerdict evidence' {
    function New-DropSet { @((New-DropStatus 'AT-host'), (New-DropStatus 'AT-g1' -drops 1 -grants 1), (New-DropStatus 'AT-g2'), (New-DropStatus 'AT-g3')) }
    function Get-V($s, $scn, $drop = @(1)) { Get-SyncTestVerdict -Statuses $s -Nicks $Nicks -TimedOut $false -DropEditors $drop -DropMode 'noreturn' -DropScenario $scn }

    It 'fails when empty score lines would compare equal' {
        $s = New-DropSet; foreach ($x in $s) { $x.scoreLines = @() }
        (Get-V $s 'offturn').Reason | Should Match 'SKOR'
    }
    It 'fails when the master saw no [CTL] bot line' {
        $s = New-DropSet; $s[0].ctlBotLines = 0
        (Get-V $s 'offturn').Reason | Should Match 'CTL'
    }
    It 'requires [GERIAL] on every remaining editor for after-draw' {
        $s = New-DropSet; foreach ($i in 0, 2, 3) { $s[$i].gerialLines = 2 }
        (Get-V $s 'after-draw').Pass | Should Be $true
        $s[3].gerialLines = 0
        (Get-V $s 'after-draw').Reason | Should Match 'AT-g3.*GERIAL'
    }
    It 'forbids [GERIAL] for offturn and hand-end, accepts either for after-discard' {
        $s = New-DropSet; $s[2].gerialLines = 1
        (Get-V $s 'offturn').Reason | Should Match 'olmamalıydı'
        (Get-V $s 'hand-end').Pass | Should Be $false
        (Get-V $s 'after-discard').Pass | Should Be $true
    }
    It 'names the known seeds for an untriggered after-open' {
        $s = New-DropSet; $s[1].drops = 0
        (Get-V $s 'after-open').Reason | Should Match '1234'
    }
}

Describe 'Get-DropSummary' {
    It 'prints one line per editor' {
        $s = @((New-DropStatus 'AT-host'), (New-DropStatus 'AT-g1' 'Gone' 1))
        $s[1] | Add-Member dropAtMove 4; $s[0].ctlBotLines = 2; $s[0].gerialLines = 1
        $l = @(Get-DropSummary -Statuses $s -Nicks @('AT-host', 'AT-g1') -DropEditors @(1))
        $l.Count | Should Be 2
        $l[0] | Should Match 'KALDI.*2 satır.*1 satır'
        $l[1] | Should Match 'KOPTU.*hamle 4'
    }
}
