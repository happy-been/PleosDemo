param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $repo 'unity\InputProbe\Assets\_Parkside\Lookdev'
$staging = Join-Path $repo 'build\parking-lookdev\sources'
$headers = @{ 'User-Agent' = 'PARKSIDE-lookdev/0.1' }
$records = [System.Collections.Generic.List[object]]::new()
function Fetch-File($url, $target, $asset) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    if (-not (Test-Path -LiteralPath $target)) {
        & curl.exe -f -s -S -L --retry 2 $url -o $target
        if ($LASTEXITCODE -ne 0) { throw ('Download failed: ' + $url) }
    }
    $records.Add(@{ asset=$asset; source=$url; file=[IO.Path]::GetRelativePath($repo,$target); bytes=(Get-Item -LiteralPath $target).Length; sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() })
}
foreach ($asset in @('asphalt_03','concrete_floor','brown_planks_03')) {
    $data = Invoke-RestMethod ('https://api.polyhaven.com/files/' + $asset) -Headers $headers
    $resolution = if ($asset -eq 'brown_planks_03') { '1k' } else { '2k' }
    foreach ($channel in @('Diffuse','nor_gl','arm')) {
        $file = $data.$channel.$resolution.jpg
        Fetch-File $file.url (Join-Path $destination ('Textures\' + $asset + '\' + [IO.Path]::GetFileName(([uri]$file.url).AbsolutePath))) $asset
    }
    Write-Output ('Downloaded texture: ' + $asset)
}
foreach ($asset in @('covered_car','shrub_01','tree_small_02')) {
    $data = Invoke-RestMethod ('https://api.polyhaven.com/files/' + $asset) -Headers $headers
    $model = $data.fbx.'1k'.fbx
    Fetch-File $model.url (Join-Path $staging ($asset + '\source.fbx')) $asset
    foreach ($entry in $model.include.PSObject.Properties) {
        if ($entry.Name -match '(diff|nor_gl|arm|alpha).*\.(png|jpg)$') {
            Fetch-File $entry.Value.url (Join-Path $destination ('Textures\' + $asset + '\' + [IO.Path]::GetFileName($entry.Name))) $asset
        }
    }
    $extraChannels = if ($asset -eq 'tree_small_02') { @('branch_arm','leaves_arm') } else { @('nor_gl','arm') }
    foreach ($channel in $extraChannels) {
        $file = $data.$channel.'1k'.jpg
        if ($file) { Fetch-File $file.url (Join-Path $destination ('Textures\' + $asset + '\' + [IO.Path]::GetFileName(([uri]$file.url).AbsolutePath))) $asset }
    }
    if ($asset -eq 'tree_small_02') {
        $alpha = $data.leaves_alpha.'1k'.jpg
        if ($alpha) { Fetch-File $alpha.url (Join-Path $destination ('Textures\' + $asset + '\' + [IO.Path]::GetFileName(([uri]$alpha.url).AbsolutePath))) $asset }
    }
    Write-Output ('Downloaded model source: ' + $asset)
}
$sky = Invoke-RestMethod 'https://api.polyhaven.com/files/kloofendal_48d_partly_cloudy_puresky' -Headers $headers
Fetch-File $sky.hdri.'2k'.hdr.url (Join-Path $destination 'Textures\Sky\CourtyardSky.hdr') 'kloofendal_48d_partly_cloudy_puresky'
$records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'AssetSources.json') -Encoding utf8
Write-Output ('Asset files recorded: ' + $records.Count)
