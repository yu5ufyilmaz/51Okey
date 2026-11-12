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
