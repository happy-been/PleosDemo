param(
    [string]$UnityEditor,
    [string]$AndroidSdk,
    [string]$JavaHome,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$taskRepository = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'android-environment.ps1')
$UnityEditor = Get-ParksideUnityEditor $taskRepository $UnityEditor
$taskSourceSdk = Get-ParksideAndroidSdk $taskRepository $AndroidSdk
$taskGradleJavaHome = Get-ParksideJavaHome $taskSourceSdk $JavaHome
$taskProject = Join-Path $taskRepository 'unity\InputProbe'
$taskOutput = Join-Path $taskRepository 'build\unity-probe'
$taskEditorData = Join-Path (Split-Path -Parent $UnityEditor) 'Data'
$taskAndroidTools = Join-Path $taskEditorData 'PlaybackEngines\AndroidPlayer'
$taskJava = Join-Path $taskAndroidTools 'OpenJDK\bin\java.exe'
if (-not (Test-Path -LiteralPath $taskJava)) { throw '선택한 Editor에 Android Build Support와 OpenJDK가 필요합니다.' }
$taskGradleDirectory = Join-Path $taskAndroidTools 'Tools\gradle\lib'
if (-not (Test-Path -LiteralPath $taskGradleDirectory)) { throw '선택한 Editor에 Android Gradle 도구가 없습니다.' }
$taskGradleLauncher = Get-ChildItem -LiteralPath $taskGradleDirectory -Filter 'gradle-launcher-*.jar' | Select-Object -First 1
if (-not $taskGradleLauncher) { throw 'Unity 내장 Gradle을 찾을 수 없습니다.' }
foreach ($taskRequired in @('platforms\android-34\android.jar','build-tools\34.0.0\aapt2.exe','licenses\android-sdk-license')) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskSourceSdk $taskRequired))) { throw ('필요한 Android 도구가 없습니다: ' + $taskRequired) }
}
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskPreviousJavaHome = $env:JAVA_HOME
$taskPreviousAndroidHome = $env:ANDROID_HOME
$taskPreviousProbeSdk = $env:PARKSIDE_ANDROID_SDK
Push-Location $taskRepository
try {
    $env:JAVA_HOME = $taskGradleJavaHome
    $env:ANDROID_HOME = $taskSourceSdk
    $taskNativeTasks = @(':vehicle-bridge:exportForUnity')
    if (-not $SkipTests) { $taskNativeTasks += @(':vehicle-bridge:testDebugUnitTest', ':app:testDebugUnitTest') }
    & .\gradlew.bat @taskNativeTasks --console=plain
    if ($LASTEXITCODE -ne 0) { throw 'Kotlin 입력 라이브러리 빌드가 실패했습니다.' }

    # Keep Unity 2022's Java 11-compatible command-line tools together with API 34.
    # All staging stays inside the ignored build folder; installed editors are not modified.
    $taskProbeSdk = Join-Path $taskOutput 'android-sdk'
    $taskSdkCopies = @(
        @((Join-Path $taskSourceSdk 'platforms\android-34'), 'platforms\android-34', 'android.jar'),
        @((Join-Path $taskSourceSdk 'build-tools\34.0.0'), 'build-tools\34.0.0', 'aapt2.exe'),
        @((Join-Path $taskSourceSdk 'platform-tools'), 'platform-tools', 'adb.exe'),
        @((Join-Path $taskSourceSdk 'licenses'), 'licenses', 'android-sdk-license'),
        @((Join-Path $taskAndroidTools 'SDK\cmdline-tools\6.0'), 'cmdline-tools\latest', 'bin\sdkmanager.bat')
    )
    foreach ($taskSdkCopy in $taskSdkCopies) {
        $taskSdkDestination = Join-Path $taskProbeSdk $taskSdkCopy[1]
        if (-not (Test-Path -LiteralPath (Join-Path $taskSdkDestination $taskSdkCopy[2]))) {
            if (-not (Test-Path -LiteralPath $taskSdkCopy[0])) { throw ('필요한 Android 도구가 없습니다: ' + $taskSdkCopy[0]) }
            New-Item -ItemType Directory -Path $taskSdkDestination -Force | Out-Null
            Get-ChildItem -LiteralPath $taskSdkCopy[0] -Force | Copy-Item -Destination $taskSdkDestination -Recurse -Force
        }
    }
    $env:PARKSIDE_ANDROID_SDK = $taskProbeSdk
    $taskUnityLog = Join-Path $taskOutput 'unity-export.log'
    $taskUnityArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $taskProject + '"'),
        '-buildTarget', 'Android', '-executeMethod', 'Parkside.Editor.ParksideAndroidBuild.BuildAndroid', '-logFile', ('"' + $taskUnityLog + '"'))
    $taskUnityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $taskUnityArguments -WindowStyle Hidden -PassThru
    # Wait for the Editor itself, not persistent ADB/licensing child processes.
    $taskUnityProcess.WaitForExit()
    if ($taskUnityProcess.ExitCode -ne 0) {
        Get-Content -LiteralPath $taskUnityLog -Tail 60
        throw 'Unity Android 내보내기가 실패했습니다. 같은 프로젝트가 Editor에서 열려 있으면 닫고 다시 실행해 주세요.'
    }
    $taskExport = Join-Path $taskOutput 'android-export'
    $taskNativePreference = $ErrorActionPreference
    try {
        # Windows PowerShell can treat a successful Java command's stderr warnings as terminating errors.
        $ErrorActionPreference = 'Continue'
        & $taskJava -classpath $taskGradleLauncher.FullName org.gradle.launcher.GradleMain -p $taskExport :launcher:assembleDebug --console=plain --no-daemon 2>&1 |
            ForEach-Object { $_.ToString() } | Tee-Object -FilePath (Join-Path $taskOutput 'gradle-apk.log')
        $taskGradleExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $taskNativePreference }
    if ($taskGradleExit -ne 0) { throw 'Unity APK 패키징이 실패했습니다.' }
    $taskApk = Join-Path $taskOutput 'parkside-input-probe.apk'
    Copy-Item -LiteralPath (Join-Path $taskExport 'launcher\build\outputs\apk\debug\launcher-debug.apk') -Destination $taskApk -Force
    Write-Output "APK: $taskApk"
} finally {
    $env:JAVA_HOME = $taskPreviousJavaHome
    $env:ANDROID_HOME = $taskPreviousAndroidHome
    $env:PARKSIDE_ANDROID_SDK = $taskPreviousProbeSdk
    Pop-Location
}
