<#
.SYNOPSIS
  4 açık editörde (ana proje + 3 ParrelSync clone) insansız oyun oynatır, sync sonucunu PASS/FAIL olarak verir.
  Tasarım: docs/superpowers/specs/2026-11-11-sync-autotest-design.md
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Deals 1
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Deals 1 -InjectDesync 5   # harness kendini test eder: FAIL beklenir
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Mode smart -Seed 11       # test oyuncuları (diz, aç, işle, yandan al)
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Mode smart -Mistakes 0.3 # bazen bilerek kural dışı
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -JoinRoom osman        # telefonda kurulan odaya 3 clone test oyuncusu olarak girer
  Tasarım (smart): docs/superpowers/specs/2026-11-11-test-oyunculari-design.md
.NOTES
  1 el ~4 dk sürer. Claude 10 dk'lık komut sınırına takılmasın diye uzun koşuları arka planda (run_in_background) başlatır;
  süreç yarıda öldürülürse finally çalışmaz, editörler Play'de ve config'ler Temp'te kalır.
#>
param(
    [int]$Deals = 1,
    [int]$Seed = 0,
    [int]$TimeoutSec = 0,   # 0 = el sayısına göre (Get-DefaultTimeoutSec)
    [int]$InjectDesync = 0,
    [ValidateSet('simple', 'smart')][string]$Mode = 'simple',
    [double]$Mistakes = 0,
    [int]$InjectOracleBug = 0,   # öz-test: AT-g1 bu hamlede beklenen cezayı bilerek bozar, [BUG] ile FAIL beklenir
    [switch]$KeepPlaying,
    [string]$JoinRoom = '',   # insana karşı: odayı telefon kurar (master o), 3 clone bu ada katılır
    [string[]]$Projects = @(
        'C:\Users\osman\Documents\51Okey',
        'C:\Users\osman\Documents\51Okey_clone_0',
        'C:\Users\osman\Documents\51Okey_clone_1',
        'C:\Users\osman\Documents\51Okey_clone_2'
    )
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\SyncTestLib.ps1"
if ($JoinRoom -and -not $PSBoundParameters.ContainsKey('Mode')) { $Mode = 'smart' }
if ($TimeoutSec -le 0) {
    $TimeoutSec = Get-DefaultTimeoutSec $Deals
    if ($Mode -eq 'smart') { $TimeoutSec *= 2 }   # dizme ve gözlem beklemeleri turları uzatır
    if ($JoinRoom) { $TimeoutSec = 3600 * $Deals }   # insan düşünür; el başına 1 saat
}

$Nicks = @('AT-host', 'AT-g1', 'AT-g2', 'AT-g3')
function Leaf($p) { Split-Path $p -Leaf }

# powershell -File diziyi tek string olarak verir: "a,b,c,d"
if ($Projects.Count -eq 1) { $Projects = @($Projects[0] -split ',' | ForEach-Object { $_.Trim() }) }
if ($Projects.Count -ne $Nicks.Count) { Write-Host "HATA: -Projects $($Nicks.Count) proje olmalı (host + 3 guest), $($Projects.Count) verildi"; exit 1 }
# Ana editör boş kalır (build orada alınır); master telefon olduğu için host config'i yok.
if ($JoinRoom) { $Projects = $Projects[1..3]; $Nicks = $Nicks[1..3] }

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
    if (-not (Test-EditorStopped $st)) { $problems += "$(Leaf $p): zaten Play'de (playMode=$($st.playMode))" }
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
# Photon'a sadece MainMenu'deki PhotonManager bağlanır; editörde hangi sahne açıksa açık olsun Play oradan başlasın.
# Kod dosyadan verilir: PowerShell 5.1 native argümanlardaki tırnakları siler.
$setStart = Join-Path ([IO.Path]::GetTempPath()) "synctest_setstart_$runId.cs"
$resetStart = Join-Path ([IO.Path]::GetTempPath()) "synctest_resetstart_$runId.cs"
[IO.File]::WriteAllText($setStart, 'UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/Osman/Scenes/MainMenu.unity"); UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene != null')
[IO.File]::WriteAllText($resetStart, 'UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null; true')
Write-Host "runId $runId | $Deals el | seed $Seed$(if ($JoinRoom) { " | '$JoinRoom' odasına katılır" })$(if ($InjectDesync) { " | AT-g1 $InjectDesync. hamlede desync enjekte eder" })"
try {
    # 3. Hazırlık
    for ($i = 0; $i -lt $Projects.Count; $i++) {
        $p = $Projects[$i]
        Invoke-UnityCmd $p @('editor', 'set_auto_tick', '--enabled', 'true', '--persist', 'false') | Out-Null
        if ((Invoke-UnityCmd $p @('script', 'eval', '--file', $setStart)) -ne $true) { throw "$(Leaf $p): Play başlangıç sahnesi MainMenu yapılamadı" }
        Invoke-UnityCmd $p @('console', 'clear') | Out-Null
        $dir = Join-Path $p 'Temp\SyncAutoTest'
        if (Test-Path $dir) { Remove-Item "$dir\*" -Recurse -Force }
        New-Item -ItemType Directory -Force $dir | Out-Null
        $cfg = [ordered]@{
            runId              = $runId
            role               = $(if ($i -eq 0 -and -not $JoinRoom) { 'host' } else { 'guest' })
            roomName           = $(if ($JoinRoom) { $JoinRoom } else { "autotest_$runId" })
            nickName           = $Nicks[$i]
            deals              = $Deals
            seed               = $Seed
            injectDesyncAtMove = $(if ($i -eq 1) { $InjectDesync } else { 0 })
            moveDelaySec       = 0.3
            mode               = $Mode
            mistakeRate        = $Mistakes
            injectOracleBugAtMove = $(if ($i -eq 1) { $InjectOracleBug } else { 0 })
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
        $line = "deal $(if ($h) { $h.deal } else { '?' }) | moves $moves$(if (-not $JoinRoom) { " | ok $(if ($h) { $h.sync.ok }) desync $(if ($h) { $h.sync.desync })" })"
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
            try { Invoke-UnityCmd $p @('script', 'eval', '--file', $resetStart) -Retries 10 | Out-Null }
            catch { Write-Host "UYARI: $(Leaf $p) Play başlangıç sahnesi sıfırlanamadı: $($_.Exception.Message)" }
        }
        $cfgPath = Join-Path $p 'Temp\SyncAutoTest\config.json'
        if (Test-Path $cfgPath) { Remove-Item $cfgPath -Force }
    }
}

# 7. Topla
$noise = 'Unity Remote requirements check failed|Unable to perform online search|Google\.IOSResolver|mainTemplate\.gradle|^\[DESYNC\]'
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

if ($Mode -eq 'smart') {
    # Oyuncular gerçekten açıp işledi mi görünsün; [BUG] varsa o hamlede 4 günlüğün ne dediği.
    for ($i = 0; $i -lt $Projects.Count; $i++) {
        $s = $final[$i]
        if (-not $s) { continue }
        Write-Host ("{0}: açma {1}, işleme {2}, yandan {3}, bilerek hata {4}, bug {5}, bitiş {6} | günlük: {7}" -f `
                $Nicks[$i], $s.opens, $s.processes, $s.sidePicks, $s.mistakes, $s.bugs, $(if ($s.finishType) { $s.finishType } else { '-' }), $s.journal)
    }
    $bugKey = Get-FirstBugKey -Statuses $final
    if ($bugKey) {
        Write-Host "İlk [BUG] hamlesi $bugKey, günlüklerde o hamle:"
        Get-JournalExcerpt -Paths @($final | ForEach-Object { if ($_) { $_.journal } }) -Key $bugKey | ForEach-Object { Write-Host "  $_" }
    }
}

$v = Get-SyncTestVerdict -Statuses $final -Nicks $Nicks -TimedOut $timedOut -NoHost:([bool]$JoinRoom)
$elapsed = [int]((Get-Date) - $started).TotalSeconds
if ($v.Pass) { Write-Host "PASS: $($v.Reason) ($elapsed sn)"; exit 0 }
Write-Host "FAIL: $($v.Reason) ($elapsed sn)"
exit 1
