# Commit öncesi gizli bilgi taraması. Bulgu satırında eşleşen metin ASLA yazılmaz.
# Kullanım: Check-Secrets.ps1 [-Paths <dosyalar>]   (Paths yoksa commit'e hazırlanmış dosyalar, index sürümü)
param([string[]]$Paths)

$markerAllow = 'secret-scan: allow'
$secretNames = @('google-services.json', 'google-services-desktop.json', 'google-services.xml', 'GoogleService-Info.plist')
$secretExts  = @('.keystore', '.jks', '.p12', '.pem')
$binaryExts  = @('.png', '.jpg', '.dll', '.so', '.aar', '.jar', '.apk', '.aab')

# Sıra önemli değil; her satır her kurala karşı denenir.
$rules = [ordered]@{
    'google-api-key'  = 'AIza[0-9A-Za-z_\-]{35}'
    'private-key'     = '-----BEGIN [A-Z ]*PRIVATE KEY-----'
    'service-account' = '"type"\s*:\s*"service_account"'
    'admob-real-id'   = 'ca-app-pub-(?!3940256099942544)\d{16}'
    'assigned-secret' = '(keystorePass|keyaliasPass|storePassword|keyPassword|password|passwd|secret|api_key)["'']?\s*[:=]\s*["''][^"'']{6,}["'']'  # secret-scan: allow (kuralın kendisi)
}

# Tırnaksız değer yalnız gradle/yaml/Unity dosyalarında aranır; boş değer (keystorePass:) eşleşmez.
$unquotedExts = @('.properties', '.gradle', '.yaml', '.yml', '.asset')
$unquotedRule = '(keystorePass|keyaliasPass|storePassword|keyPassword|password|passwd|secret|api_key)["'']?\s*[:=]\s*[^\s"'']{6,}'  # secret-scan: allow (kuralın kendisi)

# Çıkış kodları: 0 temiz, 1 bulgu, 2 tarama çalışamadı (git hatası, eksik dosya).
$ErrorActionPreference = 'Stop'
$found = 0
$errors = 0
$scanned = 0
try {
    $staged = -not $Paths
    if ($staged) {
        $null = git rev-parse --show-toplevel
        if ($LASTEXITCODE -ne 0) { throw 'git rev-parse failed (not a git repository?)' }
        $Paths = @(git -c core.quotepath=false diff --cached --no-renames --name-only --diff-filter=ACM)
        if ($LASTEXITCODE -ne 0) { throw 'git diff --cached failed' }
    } else {
        # -File ile "a,b" tek metin gelir; var olmayan girdiyi virgülden böler (virgüllü dosya adı varsa o dosya önce denenir).
        $Paths = @(foreach ($x in $Paths) { if (Test-Path -LiteralPath $x) { $x } else { $x -split ',' } })
    }

    foreach ($p in $Paths) {
        if (-not $p) { continue }
        $name = Split-Path -Leaf $p
        $ext  = [IO.Path]::GetExtension($name).ToLowerInvariant()
        $isSecretName = $secretNames -contains $name -or $secretExts -contains $ext

        # Staged modda içerik index'ten (commit'e girecek blob) okunur, çalışma kopyasından değil.
        if (-not $staged -and -not (Test-Path -LiteralPath $p -PathType Leaf)) {
            Write-Output "ERROR ${p}: file not found"
            $errors++
            continue
        }
        $scanned++

        if ($isSecretName) {
            Write-Output "SECRET ${p}:0 secret-file"
            $found++
            continue
        }
        if ($binaryExts -contains $ext) { continue }

        if ($staged) {
            $lines = @(git show ":$p")
            if ($LASTEXITCODE -ne 0) { throw "git show failed for $p" }
        } else {
            $lines = [IO.File]::ReadAllLines((Resolve-Path -LiteralPath $p).ProviderPath)
        }
        for ($i = 0; $i -lt $lines.Length; $i++) {
            $line = $lines[$i]
            if ($line.Contains($markerAllow)) { continue }
            foreach ($rule in $rules.Keys) {
                if ($line -match $rules[$rule]) {
                    Write-Output "SECRET ${p}:$($i + 1) $rule"
                    $found++
                }
            }
            if (($unquotedExts -contains $ext) -and $line -match $unquotedRule) {
                Write-Output "SECRET ${p}:$($i + 1) assigned-secret"
                $found++
            }
        }
    }
} catch {
    Write-Output "ERROR: $($_.Exception.Message)"
    exit 2
}

if ($errors -gt 0) { exit 2 }
if ($found -gt 0) { exit 1 }
Write-Output "secrets: clean ($scanned files)"
exit 0
