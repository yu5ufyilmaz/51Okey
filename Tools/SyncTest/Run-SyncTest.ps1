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
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -JoinRoom '*'          # adı bilinmiyorsa: lobide boş koltuklu ilk oda
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Solo -Mode smart        # clone'lar kapalı: tek test oyuncusu + oyunun botları
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Mode smart -SameNames  # 4 test oyuncusu da aynı Photon adını gösterir (iç kimlik AT-host/AT-g1.. aynı kalır)
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Mode smart -Bots 2     # 2 editör (host + 1 misafir) + oyunun 2 botu; -Bots 1..3, -Solo/-JoinRoom ile birlikte olmaz
  powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-SyncTest.ps1 -Mode smart -Seed 11 -DropRejoin after-draw -NoReturn   # bir editör kopar; kalanlar bota devreder
  -DropRejoin senaryoları (dört editör): offturn (sıra dışı, kararlı), after-discard (atıştan hemen sonra), before-draw, after-draw, after-open
    (tohum 1234 ya da 7; 11 tetiklemez), master (masa sahibi sıra dışı), master-turn (masa sahibi kendi turunda çektikten sonra),
    hand-end (misafir el sonu ekranında; -Deals 2 gerekir), master-hand-end, two. Kip: varsayılan dönüş (-DropOfflineSec sonra oyunun
    ReconnectFlow yolundan), -NoReturn (dönmez), -ReturnAfterEnd (diğerleri bitince döner, "oyun sona ermiş" görmeli).
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
    [switch]$Solo,   # sadece ana editör: test oyuncusu + oyunun 3 botu (clone'lar kapalıyken)
    [switch]$SameNames,   # tüm test oyuncuları aynı Photon adını gösterir (ad ile alan bulan kod bozulur)
    [ValidateSet('', 'offturn', 'after-discard', 'before-draw', 'after-draw', 'after-open', 'master', 'master-turn', 'master-hand-end', 'hand-end', 'two')][string]$DropRejoin = '',   # bir editör belirli anda bağlantıyı keser (dört editör gerekir)
    [switch]$NoReturn,   # kopan dönmez (varsayılan: -DropOfflineSec sonra döner)
    [switch]$ReturnAfterEnd,   # kopan, diğerleri bitince döner
    [switch]$SelfRestore,   # ağsız kanıt: test oyuncusu her 3. kendi turunda masayı fotoğraftan yeniden kurar ([DONUS] öz-kurulum eşit)
    [int]$DropOfflineSec = 8,   # dönüşten önce çevrimdışı süre
    [int]$Bots = 0,   # 1..3: ilk 4-N editör (host + misafirler) + oyunun N botu
    [string[]]$Projects = @(
        'F:\Documents\UnityProjects\51Okey',
        'F:\Documents\UnityProjects\51Okey_clone_0',
        'F:\Documents\UnityProjects\51Okey_clone_1',
        'F:\Documents\UnityProjects\51Okey_clone_2'
    )
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\SyncTestLib.ps1"
if ($Bots -lt 0 -or $Bots -gt 3) { Write-Host "HATA: -Bots 1, 2 veya 3 olmalı ($Bots verildi)"; exit 1 }
if ($Bots -and ($Solo -or $JoinRoom)) { Write-Host "HATA: -Bots, -Solo ve -JoinRoom ile birlikte verilemez"; exit 1 }
if ($DropRejoin -and ($Solo -or $Bots -or $JoinRoom)) { Write-Host "HATA: -DropRejoin yalnız dört editörle çalışır (-Solo, -Bots, -JoinRoom ile birlikte verilemez)"; exit 1 }
if ($NoReturn -and $ReturnAfterEnd) { Write-Host "HATA: -NoReturn ve -ReturnAfterEnd birlikte verilemez"; exit 1 }
# Kopan editör indeksleri (0 = host): masa sahibi senaryoları host'u, two 1 ve 2'yi, diğerleri 1'i koparır.
$dropEditors = @()
if ($DropRejoin -eq 'hand-end' -and $Deals -lt 2) { Write-Host "HATA: hand-end senaryosu -Deals 2 ister (sonraki el bot koltuğuyla oynanır)"; exit 1 }
if ($DropRejoin) {
    $dropEditors = switch ($DropRejoin) { { $_ -in 'master', 'master-turn', 'master-hand-end' } { @(0) } 'two' { @(1, 2) } default { @(1) } }
}
$dropMode = $(if ($NoReturn) { 'noreturn' } elseif ($ReturnAfterEnd) { 'afterend' } else { 'return' })
# Bot doldurma, host'un insanlar oturunca bitirdiği beklemedir (humans); 300 sn sadece güvenlik üst sınırı.
$botFillDelaySec = $(if ($Bots) { 300 } else { 0 })
if ($JoinRoom -and -not $PSBoundParameters.ContainsKey('Mode')) { $Mode = 'smart' }
if ($TimeoutSec -le 0) {
    $TimeoutSec = Get-DefaultTimeoutSec $Deals
    if ($Mode -eq 'smart') { $TimeoutSec *= 2 }   # dizme ve gözlem beklemeleri turları uzatır
    if ($JoinRoom) { $TimeoutSec = 3600 * $Deals }   # insan düşünür; el başına 1 saat
    $TimeoutSec += $botFillDelaySec   # oyun, insanlar oturunca başlar; üst sınır kadar pay
}

$Nicks = @('AT-host', 'AT-g1', 'AT-g2', 'AT-g3')
function Leaf($p) { Split-Path $p -Leaf }

# powershell -File diziyi tek string olarak verir: "a,b,c,d"
if ($Projects.Count -eq 1) { $Projects = @($Projects[0] -split ',' | ForEach-Object { $_.Trim() }) }
if ($Projects.Count -ne $Nicks.Count) { Write-Host "HATA: -Projects $($Nicks.Count) proje olmalı (host + 3 guest), $($Projects.Count) verildi"; exit 1 }
# Ana editör boş kalır (build orada alınır); master telefon olduğu için host config'i yok.
if ($JoinRoom) { $Projects = $Projects[1..3]; $Nicks = $Nicks[1..3] }
# Tek insan: kıyaslanacak başka client yok, sync sayacı boş kalır; hüküm hata ve [BUG]'a bakar.
if ($Solo) { $Projects = @($Projects[0]); $Nicks = @($Nicks[0]) }
# Eksik koltukları oyunun botları doldurur: ilk 4-N proje (host + misafirler) kullanılır.
if ($Bots) { $Projects = @($Projects[0..(3 - $Bots)]); $Nicks = @($Nicks[0..(3 - $Bots)]) }
# -Bots 3'te tek editör kalır (-Solo gibi): kıyaslanacak başka client yok.
$noHost = [bool]$JoinRoom -or $Solo -or $Projects.Count -eq 1

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
$allDone = $false
$final = $null
$started = Get-Date
# Photon'a sadece MainMenu'deki PhotonManager bağlanır; editörde hangi sahne açıksa açık olsun Play oradan başlasın.
# Kod dosyadan verilir: PowerShell 5.1 native argümanlardaki tırnakları siler.
$setStart = Join-Path ([IO.Path]::GetTempPath()) "synctest_setstart_$runId.cs"
$resetStart = Join-Path ([IO.Path]::GetTempPath()) "synctest_resetstart_$runId.cs"
[IO.File]::WriteAllText($setStart, 'UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>("Assets/Osman/Scenes/MainMenu.unity"); UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene != null')
[IO.File]::WriteAllText($resetStart, 'UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = null; true')
Write-Host "runId $runId | $Deals el | seed $Seed$(if ($JoinRoom) { " | '$JoinRoom' odasına katılır" })$(if ($InjectDesync) { " | AT-g1 $InjectDesync. hamlede desync enjekte eder" })$(if ($SameNames) { " | aynı isim" })$(if ($DropRejoin) { " | kopma: $DropRejoin ($dropMode), kopan: $(($dropEditors | ForEach-Object { $Nicks[$_] }) -join ',')" })$(if ($Bots) { " | $($Projects.Count) editör + $Bots bot (insanlar oturunca başlar, üst sınır $botFillDelaySec sn)" })"
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
            fillWithBots       = [bool]($Solo -or $Bots)
            displayName        = $(if ($SameNames) { 'Ayni' } else { '' })
            botFillDelaySec    = $botFillDelaySec
            humanHost          = [bool]$JoinRoom
            humans             = $(if ($Bots) { 4 - $Bots } else { 0 })
            dropScenario       = $DropRejoin
            dropThis           = [bool]($dropEditors -contains $i)
            dropOfflineSec     = $DropOfflineSec
            dropMode           = $(if ($DropRejoin) { $dropMode } else { '' })
            selfRestoreEveryTurns = $(if ($SelfRestore) { 3 } else { 0 })
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
    $stoppedGone = @{}
    $released = $false
    while (-not $timedOut) {
        $all = Read-All
        $h = $all[0]
        $moves = ($all | ForEach-Object { if ($_) { $_.myMoves } else { '?' } }) -join '/'
        $okSum = ($all | Where-Object { $_ } | ForEach-Object { $_.sync.ok } | Measure-Object -Sum).Sum
        $deSum = ($all | Where-Object { $_ } | ForEach-Object { $_.sync.desync } | Measure-Object -Sum).Sum
        $line = "deal $(if ($h) { $h.deal } else { '?' }) | moves $moves$(if ($DropRejoin) { " | ok $okSum desync $deSum | durum $(($all | ForEach-Object { if ($_) { $_.state } else { '?' } }) -join '/')" } elseif (-not $JoinRoom) { " | ok $(if ($h) { $h.sync.ok }) desync $(if ($h) { $h.sync.desync })" })"
        if ($line -ne $last) { Write-Host $line; $last = $line }
        $states = @($all | ForEach-Object { if ($_) { $_.state } else { '' } })
        # Gone: kopup dönmeyecek editör; bitmiş sayılır ve Play'den hemen çıkarılır.
        for ($i = 0; $i -lt $Projects.Count; $i++) {
            if ($states[$i] -eq 'Gone' -and -not $stoppedGone[$i]) {
                $stoppedGone[$i] = $true
                Write-Host "$($Nicks[$i]) koptu ve dönmeyecek; Play'den çıkarılıyor"
                try { Invoke-UnityCmd $Projects[$i] @('editor.playmode', 'stop') -Retries 10 | Out-Null }
                catch { Write-Host "UYARI: $(Leaf $Projects[$i]) Play'den çıkarılamadı: $($_.Exception.Message)" }
            }
        }
        # -ReturnAfterEnd: kalanlar bitince kopan(lar)a dönme izni (release.json) ver.
        if ($DropRejoin -and $dropMode -eq 'afterend' -and -not $released) {
            $rest = @(0..($Projects.Count - 1) | Where-Object { $dropEditors -notcontains $_ })
            if (@($rest | Where-Object { $states[$_] -eq 'Done' }).Count -eq $rest.Count) {
                foreach ($i in $dropEditors) { [IO.File]::WriteAllText((Join-Path $Projects[$i] 'Temp\SyncAutoTest\release.json'), '{}') }
                $released = $true
                Write-Host "diğerleri bitti; kopanlara dönüş izni verildi"
            }
        }
        if (@($states | Where-Object { $_ -in 'Done', 'Gone' }).Count -eq $Projects.Count) { $allDone = $true; break }
        if ($states -contains 'Error') { break }
        if ((Get-Date) -gt $started.AddSeconds($TimeoutSec)) { $timedOut = $true; break }
        Start-Sleep -Seconds 2
    }
    # Play durunca Photon her client'ta OnLeftRoom tetikler ("odadan düştü"); sonucu durdurmadan önce al.
    $final = Read-All
}
finally {
    # 6. Kapat: kalmış config, kullanıcının sonraki elle Play'ini otopilotla açardı.
    # -JoinRoom: hata/zaman aşımıyla bittiyse insanın oyunu sürüyor olabilir; test oyuncularını durdurma.
    $stillPlaying = @()
    if ([bool]$JoinRoom -and -not $allDone -and ($null -ne $final)) {
        for ($i = 0; $i -lt $Projects.Count; $i++) { if ($final[$i] -and $final[$i].state -eq 'Playing') { $stillPlaying += $Projects[$i] } }
    }
    if ($stillPlaying.Count -gt 0) {
        Write-Host "UYARI: -JoinRoom koşusu tamamlanmadan bitti; hâlâ oynayan test oyuncuları: $(($stillPlaying | ForEach-Object { Leaf $_ }) -join ', '). Hata durumundakiler zaten oynamıyor."
        Write-Host "Durdurmak için her clone'da: unity command editor.playmode stop --project-path <clone yolu>"
    }
    foreach ($p in $Projects) {
        $leave = $stillPlaying -contains $p
        $idx = [array]::IndexOf($Projects, $p)
        # Gone editörün Play'i izleme döngüsünde zaten durduruldu.
        if (-not $KeepPlaying -and -not $leave -and -not $stoppedGone[$idx]) {
            try { Invoke-UnityCmd $p @('editor.playmode', 'stop') -Retries 10 | Out-Null }
            catch { Write-Host "UYARI: $(Leaf $p) Play'den çıkarılamadı: $($_.Exception.Message)" }
        }
        if (-not $KeepPlaying) {
            # playModeStartScene yalnız sonraki Play'i etkiler; oynarken sıfırlamak güvenli.
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

if ($DropRejoin) {
    Write-Host "Kopma koşusu ($DropRejoin, $dropMode):"
    Get-DropSummary -Statuses $final -Nicks $Nicks -DropEditors $dropEditors | ForEach-Object { Write-Host "  $_" }
    # Dönüş adımları editör başına (Editor.log karışık): kopan hangi adıma vardı, masa sahibi ne karar verdi.
    for ($i = 0; $i -lt $Nicks.Count; $i++) {
        $dl = @(if ($final[$i]) { $final[$i].donusLines })
        if ($dl.Count -eq 0) { continue }
        Write-Host "  $($Nicks[$i]) [DONUS] ($($dl.Count) satır, son 25):"
        $dl | Select-Object -Last 25 | ForEach-Object { Write-Host "    $_" }
    }
}

if ($Mode -eq 'smart') {
    # Oyuncular gerçekten açıp işledi mi görünsün; [BUG] varsa o hamlede 4 günlüğün ne dediği.
    for ($i = 0; $i -lt $Projects.Count; $i++) {
        $s = $final[$i]
        if (-not $s) { continue }
        Write-Host ("{0}: açma {1}, işleme {2}, yandan {3}, bilerek hata {4}, bug {5}, bitiş {6} | günlük: {7}$(if ($s.guardTrips -gt 0) { " | adım koruması tıkandı: $($s.guardTrips)" })" -f `
                $Nicks[$i], $s.opens, $s.processes, $s.sidePicks, $s.mistakes, $s.bugs, $(if ($s.finishType) { $s.finishType } else { '-' }), $s.journal)
    }
    $bugKey = Get-FirstBugKey -Statuses $final
    if ($bugKey) {
        Write-Host "İlk [BUG] hamlesi $bugKey, günlüklerde o hamle:"
        Get-JournalExcerpt -Paths @($final | ForEach-Object { if ($_) { $_.journal } }) -Key $bugKey | ForEach-Object { Write-Host "  $_" }
    }
}

$v = Get-SyncTestVerdict -Statuses $final -Nicks $Nicks -TimedOut $timedOut -NoHost:$noHost -DropEditors $dropEditors -DropMode $(if ($DropRejoin) { $dropMode } else { '' }) -DropScenario $DropRejoin
$elapsed = [int]((Get-Date) - $started).TotalSeconds
# Masa özeti (TableDigestLog): aynı el.seri anahtarında bütün editörlerin özeti aynı olmalı.
$dg = Compare-DigestLogs -Paths @($final | ForEach-Object { if ($_) { $_.digestLog } else { $null } }) -Nicks $Nicks -Skip $(if ($DropRejoin -and $dropMode -ne 'return') { $dropEditors } else { @() })
if (-not $dg.Ok) { Write-Host "FAIL: $($dg.Message)"; if ($v.Pass) { exit 1 } }
elseif ($dg.Keys -gt 0) { Write-Host "özet: $($dg.Keys) anahtar, $($Nicks.Count) editörde aynı" }
if ($v.Pass) { Write-Host "PASS: $($v.Reason) ($elapsed sn)"; exit 0 }
Write-Host "FAIL: $($v.Reason) ($elapsed sn)"
exit 1
