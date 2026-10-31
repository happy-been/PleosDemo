function Get-ParksideLocalProperty {
    param([string]$RepositoryRoot, [string]$Name)
    $taskProperties = Join-Path $RepositoryRoot 'local.properties'
    if (-not (Test-Path -LiteralPath $taskProperties)) { return $null }
    $taskPattern = '^\s*' + [regex]::Escape($Name) + '\s*=(.*)$'
    foreach ($taskLine in Get-Content -LiteralPath $taskProperties) {
        if ($taskLine -match $taskPattern) { return ($Matches[1].Trim() -replace '\\([:\\ =])', '$1') }
    }
    return $null
}

function Get-ParksideAndroidSdk {
    param([string]$RepositoryRoot, [string]$ExplicitPath)
    if ($ExplicitPath) { $taskCandidates = @($ExplicitPath) }
    else {
        $taskCandidates = @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, (Get-ParksideLocalProperty $RepositoryRoot 'sdk.dir'))
        if ($env:LOCALAPPDATA) { $taskCandidates += (Join-Path $env:LOCALAPPDATA 'Android\Sdk') }
    }
    foreach ($taskCandidate in $taskCandidates) {
        if ($taskCandidate -and [System.IO.File]::Exists([System.IO.Path]::Combine($taskCandidate, 'platform-tools\adb.exe'))) {
            return [System.IO.Path]::GetFullPath($taskCandidate)
        }
    }
    throw 'Android SDK를 찾을 수 없습니다. -AndroidSdk, ANDROID_HOME 또는 local.properties의 sdk.dir를 설정해 주세요.'
}

function Get-ParksideJavaHome {
    param([string]$AndroidSdk, [string]$ExplicitPath)
    if ($ExplicitPath) { $taskCandidates = @($ExplicitPath) }
    else {
        $taskCandidates = @($env:JAVA_HOME, $env:ANDROID_STUDIO_JDK)
        # Support both a standard Android Studio install and an SDK/Studio sibling layout.
        $taskCandidates += (Join-Path (Split-Path -Parent $AndroidSdk) 'AndroidStudio\jbr')
        if ($env:ProgramFiles) { $taskCandidates += (Join-Path $env:ProgramFiles 'Android\Android Studio\jbr') }
        $taskJavaCommand = Get-Command java -ErrorAction SilentlyContinue
        if ($taskJavaCommand) { $taskCandidates += (Split-Path -Parent (Split-Path -Parent $taskJavaCommand.Source)) }
    }
    foreach ($taskCandidate in $taskCandidates) {
        if ($taskCandidate -and [System.IO.File]::Exists([System.IO.Path]::Combine($taskCandidate, 'bin\java.exe'))) {
            return [System.IO.Path]::GetFullPath($taskCandidate)
        }
    }
    throw 'Gradle용 JDK를 찾을 수 없습니다. -JavaHome 또는 JAVA_HOME을 Android Studio의 jbr 경로로 지정해 주세요.'
}

function Get-ParksideUnityEditor {
    param([string]$RepositoryRoot, [string]$ExplicitPath)
    $taskEditor = $ExplicitPath
    if (-not $taskEditor) { $taskEditor = $env:UNITY_EDITOR_PATH }
    if (-not $taskEditor) {
        $taskVersionFile = Join-Path $RepositoryRoot 'unity\InputProbe\ProjectSettings\ProjectVersion.txt'
        $taskVersionLine = Get-Content -LiteralPath $taskVersionFile | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
        $taskVersion = ($taskVersionLine -replace '^m_EditorVersion:\s*', '').Trim()
        $taskEditor = Join-Path $env:ProgramFiles ('Unity\Hub\Editor\' + $taskVersion + '\Editor\Unity.exe')
    }
    if (-not (Test-Path -LiteralPath $taskEditor -PathType Leaf)) {
        throw 'Unity Editor를 찾을 수 없습니다. -UnityEditor 또는 UNITY_EDITOR_PATH로 Unity.exe 경로를 지정해 주세요.'
    }
    return [System.IO.Path]::GetFullPath($taskEditor)
}
