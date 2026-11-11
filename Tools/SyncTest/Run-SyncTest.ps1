<#
.SYNOPSIS
  4 açık editörde (ana proje + 3 ParrelSync clone) insansız oyun oynatır, sync sonucunu PASS/FAIL olarak verir.
  Tasarım: docs/superpowers/specs/2026-11-11-sync-autotest-design.md
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Deals 1
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Deals 1 -InjectDesync 5   # harness kendini test eder: FAIL beklenir
#>
param(
    [int]$Deals = 1,
    [int]$Seed = 0,
    [int]$TimeoutSec = 600,
    [int]$InjectDesync = 0,
    [switch]$KeepPlaying,
    [string[]]$Projects = @(
        'C:\Users\osman\Documents\51Okey',
        'C:\Users\osman\Documents\51Okey_clone_0',
        'C:\Users\osman\Documents\51Okey_clone_1',
        'C:\Users\osman\Documents\51Okey_clone_2'
    )
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\SyncTestLib.ps1"

$Nicks = @('AT-host', 'AT-g1', 'AT-g2', 'AT-g3')
function Leaf($p) { Split-Path $p -Leaf }

# powershell -File diziyi tek string olarak verir: "a,b,c,d"
if ($Projects.Count -eq 1) { $Projects = @($Projects[0] -split ',' | ForEach-Object { $_.Trim() }) }
if ($Projects.Count -ne $Nicks.Count) { Write-Host "HATA: -Projects $($Nicks.Count) proje olmalı (host + 3 guest), $($Projects.Count) verildi"; exit 1 }

function Read-All {
    $all = New-Object object[] $Projects.Count
    for ($i = 0; $i -lt $Projects.Count; $i++) { $all[$i] = Read-SyncStatus $Projects[$i] }
    return , $all
}

function Wait-InTable($p, [int]$sec) {
    $deadline = (Get-Date).AddSeconds($sec)
    do {
        $s = Read-SyncStatus $p
        if ($s -and $s.state -in 'InTable', 'Playing', 'Done', 'Error') { return $s }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    return $s
}

# 1. Ön kontrol: kullanıcının açık bir Play'ine dokunmadan, hiçbir editörü başlatmadan dur.
$problems = @()
foreach ($p in $Projects) {
    if (-not (Test-Path $p)) { $problems += "${p}: proje klasörü yok"; continue }
    # Play'e yeni girmiş bir editör domain reload yüzünden ~15 sn cevap vermeyebilir.
    try { $st = Invoke-UnityCmd $p @('editor', 'status') -Retries 10 }
    catch { $problems += "${p}: erişilemiyor ($($_.Exception.Message))"; continue }
    if ($st.playMode -eq 'playing') { $problems += "$(Leaf $p): zaten Play'de" }
}
if ($problems.Count -gt 0) { $problems | ForEach-Object { Write-Host "HATA: $_" }; exit 1 }

# 2. Kod tazele: Assets junction ile paylaşılıyor ama her editörün kendi AssetDatabase'i var.
foreach ($p in $Projects) {
    Invoke-UnityCmd $p @('editor', 'invoke_menu', '--path', 'Assets/Refresh') | Out-Null
    Invoke-UnityCmd $p @('script', 'recompile') -Retries 10 | Out-Null
}
foreach ($p in $Projects) {
    $deadline = (Get-Date).AddSeconds(180)
    do {
        Start-Sleep -Seconds 2
        $rs = Invoke-UnityCmd $p @('script', 'recompile_status') -Retries 10
    } while ($rs.status -notin 'completed', 'up_to_date' -and (Get-Date) -lt $deadline)
    if ($rs.compilationFailed -or $rs.failed) {
        Write-Host "HATA: $(Leaf $p) derlenmiyor:"
        @($rs.errors) | Select-Object -First 5 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
    if ($rs.status -notin 'completed', 'up_to_date') { Write-Host "HATA: $(Leaf $p) derlemesi 180 sn'de bitmedi"; exit 1 }
}

$runId = Get-Date -Format 'yyyyMMdd_HHmmss'
$timedOut = $false
$started = Get-Date
Write-Host "runId $runId | $Deals el | seed $Seed$(if ($InjectDesync) { " | AT-g1 $InjectDesync. hamlede desync enjekte eder" })"
try {
    # 3. Hazırlık
    for ($i = 0; $i -lt $Projects.Count; $i++) {
        $p = $Projects[$i]
        Invoke-UnityCmd $p @('editor', 'set_auto_tick', '--enabled', 'true', '--persist', 'false') | Out-Null
        Invoke-UnityCmd $p @('console', 'clear') | Out-Null
        $dir = Join-Path $p 'Temp\SyncAutoTest'
        if (Test-Path $dir) { Remove-Item "$dir\*" -Recurse -Force }
        New-Item -ItemType Directory -Force $dir | Out-Null
        $cfg = [ordered]@{
            runId              = $runId
            role               = $(if ($i -eq 0) { 'host' } else { 'guest' })
            roomName           = "autotest_$runId"
            nickName           = $Nicks[$i]
            deals              = $Deals
            seed               = $Seed
            injectDesyncAtMove = $(if ($i -eq 1) { $InjectDesync } else { 0 })
            moveDelaySec       = 0.3
        }
        [IO.File]::WriteAllText((Join-Path $dir 'config.json'), ($cfg | ConvertTo-Json))
    }

    # 4. Sıralı başlat: host diğerlerini sadece OnPlayerEnteredRoom'da oturtur; Table yüklenmeden giren koltuksuz kalır.
    for ($i = 0; $i -lt $Projects.Count; $i++) {
        Write-Host "$($Nicks[$i]) Play'e giriyor ($(Leaf $Projects[$i]))"
        Invoke-UnityCmd $Projects[$i] @('editor.playmode', 'start') -Retries 10 | Out-Null
        $s = Wait-InTable $Projects[$i] 60
        if ($s -and $s.state -eq 'Error') { break }
        if (-not $s -or $s.state -notin 'InTable', 'Playing', 'Done') {
            $es = try { Invoke-UnityCmd $Projects[$i] @('editor', 'status') } catch { $null }
            Write-Host "$($Nicks[$i]) 60 sn'de masaya oturamadı: state=$(if ($s) { $s.state } else { 'status yok' }) frame=$($es.frameCount) ticking=$($es.playerLoopTicking)"
            $timedOut = $true
            break
        }
    }

    # 5. İzle
    $last = ''
    while (-not $timedOut) {
        $all = Read-All
        $h = $all[0]
        $moves = ($all | ForEach-Object { if ($_) { $_.myMoves } else { '?' } }) -join '/'
        $line = "deal $(if ($h) { $h.deal } else { '?' }) | moves $moves | ok $(if ($h) { $h.sync.ok }) desync $(if ($h) { $h.sync.desync })"
        if ($line -ne $last) { Write-Host $line; $last = $line }
        $states = @($all | ForEach-Object { if ($_) { $_.state } else { '' } })
        if (@($states | Where-Object { $_ -eq 'Done' }).Count -eq $Projects.Count) { break }
        if ($states -contains 'Error') { break }
        if ((Get-Date) -gt $started.AddSeconds($TimeoutSec)) { $timedOut = $true; break }
        Start-Sleep -Seconds 2
    }
    # Play durunca Photon her client'ta OnLeftRoom tetikler ("odadan düştü"); sonucu durdurmadan önce al.
    $final = Read-All
}
finally {
    # 6. Kapat: kalmış config, kullanıcının sonraki elle Play'ini otopilotla açardı.
    foreach ($p in $Projects) {
        if (-not $KeepPlaying) {
            try { Invoke-UnityCmd $p @('editor.playmode', 'stop') -Retries 10 | Out-Null }
            catch { Write-Host "UYARI: $(Leaf $p) Play'den çıkarılamadı: $($_.Exception.Message)" }
        }
        $cfgPath = Join-Path $p 'Temp\SyncAutoTest\config.json'
        if (Test-Path $cfgPath) { Remove-Item $cfgPath -Force }
    }
}

# 7. Topla
$noise = 'Unity Remote requirements check failed|Google\.IOSResolver|mainTemplate\.gradle|^\[DESYNC\]'
foreach ($p in $Projects) {
    try {
        $e = Invoke-UnityCmd $p @('console', 'get', '--level', 'error', '--tail', '20')
        $msgs = @($e.entries | ForEach-Object { ("$($_.message)" -split "`n")[0] } | Where-Object { $_ -notmatch $noise })
        if ($msgs.Count -gt 0) {
            Write-Host "$(Leaf $p) console hataları ($($msgs.Count)):"
            $msgs | Select-Object -First 3 | ForEach-Object { Write-Host "  $_" }
        }
    }
    catch { }
}
for ($i = 0; $i -lt $Projects.Count; $i++) {
    if ($final[$i] -and $final[$i].desyncLog) { Write-Host "$($Nicks[$i]) log: $($final[$i].desyncLog)" }
}

$v = Get-SyncTestVerdict -Statuses $final -Nicks $Nicks -TimedOut $timedOut
$elapsed = [int]((Get-Date) - $started).TotalSeconds
if ($v.Pass) { Write-Host "PASS: $($v.Reason) ($elapsed sn)"; exit 0 }
Write-Host "FAIL: $($v.Reason) ($elapsed sn)"
exit 1
