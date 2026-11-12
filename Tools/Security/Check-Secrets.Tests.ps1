$script = Join-Path $PSScriptRoot 'Check-Secrets.ps1'
function Scan([string]$name, [string]$text) {
    $f = Join-Path $TestDrive $name
    Set-Content -Path $f -Value $text -Encoding UTF8
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $script -Paths $f 2>&1 | Out-String
    @{ Code = $LASTEXITCODE; Out = $out }
}

Describe 'Check-Secrets' {
    # Sahte değerler parça parça kurulur: bu dosyanın kendisi taramaya takılmasın.
    $apiKey = 'AI' + 'za' + ('x' * 35)

    It 'temiz dosyada 0 döner' { (Scan 'a.cs' 'int coins = 5;').Code | Should Be 0 }
    It 'Google API anahtarını yakalar ve değeri yazdırmaz' {
        $r = Scan 'a.cs' "string k = `"$apiKey`";"
        $r.Code | Should Be 1
        $r.Out | Should Match 'google-api-key'
        $r.Out | Should Not Match $apiKey
    }
    It 'özel anahtar bloğunu yakalar' { (Scan 'k.txt' ('-----BEGIN ' + 'PRIVATE KEY-----')).Code | Should Be 1 }
    It 'servis hesabı JSON''unu yakalar' { (Scan 's.json' ('{ "type": "service' + '_account" }')).Code | Should Be 1 }
    It 'keystore dosyasını adından yakalar' { (Scan 'x.keystore' 'bin').Code | Should Be 1 }
    It 'google-services.json dosyasını adından yakalar' { (Scan 'google-services.json' '{}').Code | Should Be 1 }
    It 'gerçek AdMob kimliğini yakalar' { (Scan 'ads.cs' ('"ca-app-pub-' + '1234567890123456/1234567890"')).Code | Should Be 1 }
    It 'Google test AdMob kimliğine izin verir' { (Scan 'ads.cs' '"ca-app-pub-3940256099942544/5224354917"').Code | Should Be 0 }
    It 'şifre atamasını yakalar' { (Scan 'b.cs' ('string keystore' + 'Pass = "hunter2222";')).Code | Should Be 1 }
    It 'izin işaretli satırı atlar' { (Scan 'b.cs' ('string pass' + 'word = "ornek-deger"; // secret-scan: allow')).Code | Should Be 0 }
}

# Çıkış kodları: 0 temiz, 1 bulgu, 2 tarama çalışamadı.
$psExe = (Get-Command powershell).Source
function NewDir([bool]$git) {
    $d = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $d | Out-Null
    if ($git) { & git -C $d init -q }
    $d
}
function RunIn([string]$dir, [string[]]$extra) {
    Push-Location $dir
    try {
        $out = & $psExe -NoProfile -ExecutionPolicy Bypass -File $script @extra 2>&1 | Out-String
        @{ Code = $LASTEXITCODE; Out = $out }
    } finally { Pop-Location }
}

Describe 'Check-Secrets staged mode and failures' {
    $apiKey = 'AI' + 'za' + ('x' * 35)

    It 'hazırlanmış dosyadaki anahtarı yakalar' {
        $d = NewDir $true
        Set-Content -Path (Join-Path $d 'c.cs') -Value "string k = `"$apiKey`";" -Encoding UTF8
        & git -C $d add c.cs 2>$null
        $r = RunIn $d @()
        $r.Code | Should Be 1
        $r.Out | Should Match 'c.cs:1 google-api-key'
        $r.Out | Should Not Match $apiKey
    }
    It 'hazırlanmış temiz dosyada 0 döner' {
        $d = NewDir $true
        Set-Content -Path (Join-Path $d 'c.cs') -Value 'int coins = 5;' -Encoding UTF8
        & git -C $d add c.cs 2>$null
        $r = RunIn $d @()
        $r.Code | Should Be 0
        $r.Out | Should Match 'secrets: clean \(1 files\)'
    }
    It 'çalışma kopyası temizlense de hazırlanmış (index) sürümü tarar' {
        $d = NewDir $true
        Set-Content -Path (Join-Path $d 'c.cs') -Value "string k = `"$apiKey`";" -Encoding UTF8
        & git -C $d add c.cs 2>$null
        Set-Content -Path (Join-Path $d 'c.cs') -Value 'int coins = 5;' -Encoding UTF8
        (RunIn $d @()).Code | Should Be 1
    }
    It 'olmayan -Paths girdisinde 2 döner' {
        (RunIn (NewDir $false) @('-Paths', 'nope.cs')).Code | Should Be 2
    }
    It 'git deposu dışında -Paths olmadan 2 döner' {
        $dir = NewDir $false
        $old = $env:GIT_CEILING_DIRECTORIES
        $env:GIT_CEILING_DIRECTORIES = $TestDrive
        try { $r = RunIn $dir @() } finally { $env:GIT_CEILING_DIRECTORIES = $old }
        $r.Code | Should Be 2
    }
    It 'PATH içinde git yoksa -Paths olmadan 2 döner' {
        $dir = NewDir $false
        $old = $env:PATH
        $env:PATH = Split-Path $psExe
        try { $r = RunIn $dir @() } finally { $env:PATH = $old }
        $r.Code | Should Be 2
    }
    It 'virgülle ayrılmış -Paths değerini ayırır' {
        $d = NewDir $false
        Set-Content -Path (Join-Path $d 'a.cs') -Value 'int a;' -Encoding UTF8
        Set-Content -Path (Join-Path $d 'b.cs') -Value 'int b;' -Encoding UTF8
        $r = RunIn $d @('-Paths', 'a.cs,b.cs')
        $r.Code | Should Be 0
        $r.Out | Should Match 'secrets: clean \(2 files\)'
    }
}
