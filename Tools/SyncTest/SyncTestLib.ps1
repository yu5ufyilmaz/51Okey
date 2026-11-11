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

# Statuses[0] host'tur. Öncelik: eksik status > DESYNC > Error > zaman aşımı > yakalanan hata.
function Get-SyncTestVerdict {
    param([object[]]$Statuses, [string[]]$Nicks, [bool]$TimedOut)
    $fail = { param($reason) [pscustomobject]@{ Pass = $false; Reason = $reason } }

    for ($i = 0; $i -lt $Nicks.Count; $i++) {
        if ($null -eq $Statuses[$i]) { return & $fail "$($Nicks[$i]): status yok" }
    }
    $host0 = $Statuses[0]
    if ($host0.sync.desync -gt 0) { return & $fail "DESYNC: $($host0.sync.first)" }
    for ($i = 0; $i -lt $Nicks.Count; $i++) {
        $s = $Statuses[$i]
        if ($s.state -eq 'Error') { return & $fail "$($Nicks[$i]): Error: $(@($s.errors)[0])" }
    }
    if ($TimedOut) {
        for ($i = 0; $i -lt $Nicks.Count; $i++) {
            $s = $Statuses[$i]
            if ($s.state -ne 'Done') {
                return & $fail "zaman aşımı: $($Nicks[$i]) $($s.state) (deal $($s.deal), moves $($s.myMoves))"
            }
        }
    }
    for ($i = 0; $i -lt $Nicks.Count; $i++) {
        $s = $Statuses[$i]
        if (@($s.errors).Count -gt 0) { return & $fail "$($Nicks[$i]): $(@($s.errors)[0])" }
    }
    [pscustomobject]@{ Pass = $true; Reason = "$($host0.deal) el, ok $($host0.sync.ok), desync 0" }
}
