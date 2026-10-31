param([int]$Port = 8090, [string]$AndroidSdk, [string]$Serial)
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'android-environment.ps1')
$taskAdb = Get-Command adb -ErrorAction SilentlyContinue
if ($taskAdb -and -not $AndroidSdk) {
    $taskAdbPath = $taskAdb.Source
} else {
    $taskSdk = Get-ParksideAndroidSdk $taskRepository $AndroidSdk
    $taskAdbPath = Join-Path $taskSdk 'platform-tools\adb.exe'
}
$taskAdbArguments = @()
if ($Serial) { $taskAdbArguments += @('-s', $Serial) }
& $taskAdbPath @taskAdbArguments forward tcp:8088 tcp:8088
if ($LASTEXITCODE -ne 0) { throw 'Pleos AVD를 켜고 다시 실행해 주세요.' }
$taskProxy = Join-Path $PSScriptRoot 'parking_console.py'
Write-Host "브라우저에서 http://127.0.0.1:$Port/ 을 열어 주세요. 종료: Ctrl+C"
if (Get-Command py -ErrorAction SilentlyContinue) {
    & py -3 $taskProxy --port $Port
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    & python $taskProxy --port $Port
} else {
    throw 'Python 3.9 이상이 필요합니다.'
}
