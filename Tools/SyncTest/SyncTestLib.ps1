# Run-SyncTest.ps1 yardımcıları. Dot-source edilir; testleri: SyncTestLib.Tests.ps1 (Pester 3.4).

$UnityExe = "C:\Users\osman\AppData\Local\Unity\bin\unity.exe"

# Çalışan Editor'e pipeline komutu yollar, --result-only JSON'unu nesne olarak döner.
# Playmode girişindeki domain reload sırasında "No Pipeline instance found" geçicidir.
function Invoke-UnityCmd {
    param([string]$Project, [string[]]$Arguments, [int]$Retries = 5)
    # PS 5.1: native stderr + 'Stop' = NativeCommandError fırlatır; hatayı aşağıda çıktıdan okuyoruz.
    $ErrorActionPreference = 'Continue'
    for ($i = 0; $i -le $Retries; $i++) {
        $out = (& $UnityExe command @Arguments --project-path $Project --result-only --no-banner 2>&1 | Out-String).Trim()
        if ($out -notmatch 'No Pipeline instance found') { break }
        Start-Sleep -Seconds 2
    }
    if ($out -match 'No Pipeline instance found') { throw "$Project : Editor erişilemiyor (Pipeline yok)" }
    if ($out -eq '' -or $out -eq 'null') { return $null }
    try { $obj = $out | ConvertFrom-Json } catch { throw "$Project : '$($Arguments -join ' ')' anlaşılmayan çıktı: $out" }
    if ($obj -is [pscustomobject] -and $obj.PSObject.Properties['success'] -and $obj.success -eq $false) {
        throw "$Project : $($obj.errors[0].message)"
    }
    return $obj
}

# Client yazarken okunabilsin diye Delete paylaşımıyla açar; yarım dosya = $null (bir sonraki turda tekrar okunur).
function Read-SyncStatus {
    param([string]$Project)
    $path = Join-Path $Project 'Temp\SyncAutoTest\status.json'
    if (-not (Test-Path $path)) { return $null }
    try {
        $fs = [IO.File]::Open($path, 'Open', 'Read', 'ReadWrite, Delete')
        $sr = New-Object IO.StreamReader($fs)
        try { return $sr.ReadToEnd() | ConvertFrom-Json } finally { $sr.Dispose() }
    } catch {
        return $null
    }
}

# Statuses[0] host'tur. Öncelik: DESYNC > Error > eksik status > zaman aşımı > yakalanan hata > hiç kıyas yok.
# Error eksik status'tan önce: biri girişte düşerse sonraki guest'ler hiç başlamaz, asıl sebep o Error'dır.
# -NoHost: odayı telefon kurdu, master o; editörlerde sync sayacı yok, sadece hata ve [BUG] bakılır.
function Get-SyncTestVerdict {
    param([object[]]$Statuses, [string[]]$Nicks, [bool]$TimedOut, [switch]$NoHost, [int[]]$DropEditors = @(), [string]$DropMode = '')
    $fail = { param($reason) [pscustomobject]@{ Pass = $false; Reason = $reason } }
    # -DropRejoin: masa sahibi de kopabilir, sayaçlar yeni master'da devam eder; hepsinin toplamı alınır.
    $drop = $DropEditors.Count -gt 0
    # noreturn: kopan editör dönmez; durumu, hataları ve skor satırları kıyasın dışında.
    $skip = @(); if ($drop -and $DropMode -eq 'noreturn') { $skip = $DropEditors }
    $all = @(0..($Nicks.Count - 1) | Where-Object { $skip -notcontains $_ })

    $host0 = $Statuses[0]
    if ($drop) {
        $bad = @($Statuses | Where-Object { $_ -and $_.sync.desync -gt 0 })
        if ($bad.Count -gt 0) { return & $fail "DESYNC: $($bad[0].sync.first)" }
    }
    elseif (-not $NoHost -and $host0 -and $host0.sync.desync -gt 0) { return & $fail "DESYNC: $($host0.sync.first)" }
    foreach ($i in $all) {
        $s = $Statuses[$i]
        if ($s -and $s.state -eq 'Error') { return & $fail "$($Nicks[$i]): Error: $(@($s.errors)[0])" }
    }
    foreach ($i in $all) {
        if ($null -eq $Statuses[$i]) { return & $fail "$($Nicks[$i]): status yok" }
    }
    if ($TimedOut) {
        # Takılan genelde sırası gelen client'tır; yoksa ilk bitmeyen.
        $stuck = @($all | Where-Object { $Statuses[$_].state -ne 'Done' })
        $turn = @($stuck | Where-Object { $Statuses[$_].isMyTurn })
        if ($stuck.Count -gt 0) {
            $i = $(if ($turn.Count -gt 0) { $turn[0] } else { $stuck[0] })
            $s = $Statuses[$i]
            return & $fail "zaman aşımı: $($Nicks[$i]) $($s.state)$(if ($s.isMyTurn) { ', sıra onda' }) (deal $($s.deal), moves $($s.myMoves))"
        }
    }
    foreach ($i in $all) {
        $s = $Statuses[$i]
        if (@($s.errors).Count -gt 0) { return & $fail "$($Nicks[$i]): $(@($s.errors)[0])" }
    }
    if ($drop) { return Get-DropVerdict -Statuses $Statuses -Nicks $Nicks -DropEditors $DropEditors -DropMode $DropMode -Compared $all }
    if ($NoHost) { return [pscustomobject]@{ Pass = $true; Reason = "$($host0.deal) el, test oyuncularında hata yok" } }
    # Snapshot'lar master'a hiç ulaşmadıysa "desync 0" bir şey kanıtlamaz.
    if ($host0.sync.ok -le 0) { return & $fail "host hiç snapshot kıyaslamadı (ok 0)" }
    [pscustomobject]@{ Pass = $true; Reason = "$($host0.deal) el, ok $($host0.sync.ok), desync 0" }
}

# Get-SyncTestVerdict'in -DropRejoin kolu: senaryo tetiklendi mi, geri verme / oyun sonu görüldü mü, skorlar aynı mı.
function Get-DropVerdict {
    param([object[]]$Statuses, [string[]]$Nicks, [int[]]$DropEditors, [string]$DropMode, [int[]]$Compared)
    $fail = { param($reason) [pscustomobject]@{ Pass = $false; Reason = $reason } }
    foreach ($i in $DropEditors) {
        $s = $Statuses[$i]
        if (-not $s -or $s.drops -lt 1) { return & $fail "$($Nicks[$i]): senaryo tetiklenmedi (drops 0)" }
    }
    foreach ($i in $DropEditors) {
        $s = $Statuses[$i]
        if ($DropMode -eq 'return' -and $s.rejoinGrants -lt 1) {
            return & $fail "$($Nicks[$i]): geri verme yok (rejoinGrants 0)$(if ($s.restoreEntered) { ', kurulum modunda kaldı' })"
        }
        if ($DropMode -eq 'afterend' -and -not $s.gameOverSeen) { return & $fail "$($Nicks[$i]): oyun sona ermiş görülmedi" }
    }
    $done = @($Compared | Where-Object { $Statuses[$_].state -eq 'Done' })
    if ($done.Count -gt 1) {
        $ref = @($Statuses[$done[0]].scoreLines) -join "`n"
        foreach ($i in $done) {
            if ((@($Statuses[$i].scoreLines) -join "`n") -ne $ref) { return & $fail "skor farklı: $($Nicks[$done[0]]) ile $($Nicks[$i])" }
        }
    }
    $ok = ($Statuses | Where-Object { $_ } | ForEach-Object { $_.sync.ok } | Measure-Object -Sum).Sum
    # Snapshot'lar hiç kıyaslanmadıysa "desync 0" bir şey kanıtlamaz.
    if ($ok -le 0) { return & $fail "hiç snapshot kıyaslanmadı (ok 0)" }
    $deal = ($Statuses | Where-Object { $_ } | ForEach-Object { $_.deal } | Measure-Object -Maximum).Maximum
    [pscustomobject]@{ Pass = $true; Reason = "$deal el, ok $ok, desync 0, kopma $DropMode" }
}

# Kopma koşusu için editör başına tek satır: ne oldu + kalanların gördüğü [CTL] bot / [GERIAL] satır sayısı.
function Get-DropSummary {
    param([object[]]$Statuses, [string[]]$Nicks, [int[]]$DropEditors)
    for ($i = 0; $i -lt $Nicks.Count; $i++) {
        $s = $Statuses[$i]
        if (-not $s) { "$($Nicks[$i]): status yok"; continue }
        if ($DropEditors -contains $i) {
            "$($Nicks[$i]): KOPTU $($s.drops) kez (hamle $($s.dropAtMove), son durum $($s.state), geri verme $($s.rejoinGrants))"
        }
        else {
            "$($Nicks[$i]): KALDI (durum $($s.state)), '[CTL] koltuk N bot' $($s.ctlBotLines) satır, [GERIAL] $($s.gerialLines) satır"
        }
    }
}

# Kullanıcı Play'deyse (pause dahil) runner o editöre dokunmamalı.
function Test-EditorStopped($status) { $null -ne $status -and $status.playMode -eq 'stopped' }

# Ölçüm: giriş 60-90 sn, bir el 140-200 sn, eller arası 10 sn.
function Get-DefaultTimeoutSec([int]$Deals) { 120 + 240 * $Deals }

# Test oyuncusu [BUG] hatalarını "[BUG] <el.hamle> ..." diye yazar; ilkinin anahtarı.
function Get-FirstBugKey {
    param([object[]]$Statuses)
    foreach ($s in $Statuses) {
        if (-not $s) { continue }
        foreach ($e in @($s.errors)) {
            if ("$e" -match '^\[BUG\] (\S+) ') { return $Matches[1] }
        }
    }
    return $null
}

# Her client günlüğünden aynı el.hamle anahtarındaki satırlar (lockstep: anahtar herkeste aynı hamleyi gösterir).
function Get-JournalExcerpt {
    param([string[]]$Paths, [string]$Key)
    foreach ($p in $Paths) {
        if (-not $p -or -not (Test-Path $p)) { continue }
        Get-Content -Path $p -Encoding UTF8 | Where-Object { $_ -like "$Key |*" }
    }
}
