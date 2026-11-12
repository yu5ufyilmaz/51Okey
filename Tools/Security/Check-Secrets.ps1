# Commit öncesi gizli bilgi taraması. Bulgu satırında eşleşen metin ASLA yazılmaz.
# Kullanım: Check-Secrets.ps1 [-Paths <dosyalar>]   (Paths yoksa commit'e hazırlanmış dosyalar)
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
    'assigned-secret' = '(keystorePass|keyaliasPass|password|passwd|secret|api_key)["'']?\s*[:=]\s*["''][^"'']{6,}["'']'  # secret-scan: allow (kuralın kendisi)
}

$root = $null
if (-not $Paths) {
    # shortcut: tarar çalışma ağacındaki dosyayı, index'teki sürümü değil; kısmi staging'de upgrade et.
    $root = (git rev-parse --show-toplevel).Trim()
    $Paths = @(git -c core.quotepath=false diff --cached --name-only --diff-filter=ACM)
}

$found = 0
$scanned = 0
foreach ($p in $Paths) {
    if (-not $p) { continue }
    $full = if ($root) { Join-Path $root $p } else { $p }
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
    $scanned++

    $name = Split-Path -Leaf $p
    $ext  = [IO.Path]::GetExtension($name).ToLowerInvariant()
    if ($secretNames -contains $name -or $secretExts -contains $ext) {
        Write-Output "SECRET ${p}:0 secret-file"
        $found++
        continue
    }
    if ($binaryExts -contains $ext) { continue }

    $lines = [IO.File]::ReadAllLines((Resolve-Path -LiteralPath $full).ProviderPath)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line.Contains($markerAllow)) { continue }
        foreach ($rule in $rules.Keys) {
            if ($line -match $rules[$rule]) {
                Write-Output "SECRET ${p}:$($i + 1) $rule"
                $found++
            }
        }
    }
}

if ($found -gt 0) { exit 1 }
Write-Output "secrets: clean ($scanned files)"
exit 0
