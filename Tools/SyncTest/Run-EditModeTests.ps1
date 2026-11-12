# Ana editörde Refresh + recompile, sonra -Filter'a uyan EditMode testlerini koşar. Derleme hatası ya da başarısız test: exit 1.
# Örnek: powershell -ExecutionPolicy Bypass -File Tools\SyncTest\Run-EditModeTests.ps1 -Filter OkeyRulesTests
param([Parameter(Mandatory = $true)][string]$Filter)
$u = 'C:\Users\osman\AppData\Local\Unity\bin\unity.exe'; $p = 'F:\Documents\UnityProjects\51Okey'
function C([string[]]$a) { for ($i = 0; $i -lt 15; $i++) { $o = & $u command @a --project-path $p --result-only --no-banner 2>&1 | Out-String; if ($o -notmatch 'No Pipeline instance found') { return $o }; Start-Sleep 2 }; $o }
C @('editor', 'invoke_menu', '--path', 'Assets/Refresh') | Out-Null
C @('script', 'recompile') | Out-Null
do { Start-Sleep 2; $s = C @('script', 'recompile_status') } while ($s -notmatch '"status":\s*"(completed|up_to_date)"')
$st = $s | ConvertFrom-Json
if ($st.compilationFailed) { Write-Host 'COMPILE FAILED'; $st.errors | Select-Object -First 8 | ForEach-Object { Write-Host $_ }; exit 1 }
$r = C @('test', 'run', '--mode', 'EditMode', '--filter', $Filter, '--timeout', '300')
$parsed = $r | ConvertFrom-Json
# Test başarısız olunca pipeline sonuç dizisi yerine hata zarfı döner ("Tests Failed: N of M ...").
if ($parsed -is [pscustomobject] -and $parsed.PSObject.Properties['success'] -and -not $parsed.success) {
    Write-Host "FAIL: $($parsed.errors[0].message)"; exit 1
}
$res = @($parsed | ForEach-Object { $_ })
$bad = @($res | Where-Object { $_.status -ne 'Passed' })
$bad | ForEach-Object { Write-Host "FAIL $($_.fullName): $((("$($_.message)") -split "`n")[0])" }
Write-Host "passed $($res.Count - $bad.Count)/$($res.Count)"
if ($res.Count -eq 0 -or $bad.Count -gt 0) { exit 1 }
