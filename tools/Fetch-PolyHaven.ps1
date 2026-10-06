<#
.SYNOPSIS
    Downloads the Poly Haven (CC0) textures, models and HDRIs listed in tools/nature-assets.json into Assets/Art
    (ARCHITECTURE §25.3) and rewrites the Poly Haven block of CREDITS.md.

.DESCRIPTION
    Incremental: a file already present is skipped. Every file is downloaded to a fresh temporary folder first and only
    known asset types (.jpg .png .exr .fbx .hdr) are moved into the project; DirectX normals and packed ARM maps are
    never taken (the importer expects OpenGL normals and separate maps).

    Texture sets  -> Assets/Art/Textures/Nature/<folder>/<id>_<map>_<res>.jpg
    Models        -> Assets/Art/Models/Nature/<id>/<id>_<res>.fbx (+ textures/)
    HDRIs         -> Assets/Art/Sky/HDRI/<id>_<res>.hdr

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/Fetch-PolyHaven.ps1
#>
param(
    [string]$Manifest = (Join-Path $PSScriptRoot 'nature-assets.json'),
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$UserAgent = 'HotPatata-AssetFetch/1.0'
$Api = 'https://api.polyhaven.com'
$Allowed = @('.jpg', '.png', '.exr', '.fbx', '.hdr')
$TextureMaps = [ordered]@{ Diffuse = 'diff'; nor_gl = 'nor_gl'; Rough = 'rough'; AO = 'ao'; Displacement = 'disp' }

$Scratch = Join-Path ([IO.Path]::GetTempPath()) ('polyhaven-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Scratch | Out-Null

function Get-Json([string]$Url) {
    Invoke-RestMethod -Uri $Url -UserAgent $UserAgent
}

$script:Fetched = 0
$script:Skipped = 0
$script:Bytes = 0

function Save-File([string]$Url, [string]$Destination) {
    if (Test-Path $Destination) { $script:Skipped++; return }
    $ext = [IO.Path]::GetExtension($Destination).ToLowerInvariant()
    if ($Allowed -notcontains $ext) { Write-Warning "skipped $Url (type $ext not allowed)"; return }
    if ($Destination -match '_arm_|_nor_dx_') { return }
    $temp = Join-Path $Scratch ([Guid]::NewGuid().ToString('N') + $ext)
    Invoke-WebRequest -Uri $Url -UserAgent $UserAgent -OutFile $temp -UseBasicParsing
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
    Move-Item $temp $Destination
    $script:Fetched++
    $script:Bytes += (Get-Item $Destination).Length
    Write-Host ("  + " + $Destination.Substring($ProjectRoot.Length + 1))
}

$manifestData = Get-Content $Manifest -Raw | ConvertFrom-Json
$credits = New-Object System.Collections.Generic.List[string]

function Add-Credit([string]$Kind, [string]$Id, [string]$Used) {
    $info = Get-Json "$Api/info/$Id"
    $authors = ($info.authors.PSObject.Properties | ForEach-Object { $_.Name }) -join ', '
    $credits.Add("| $Kind | $($info.name) | [$Id](https://polyhaven.com/a/$Id) | $authors | CC0 1.0 | $Used |")
}

try {
    Write-Host 'Textures'
    foreach ($t in $manifestData.textures) {
        $files = Get-Json "$Api/files/$($t.id)"
        $dir = Join-Path $ProjectRoot "Assets/Art/Textures/Nature/$($t.folder)"
        foreach ($map in $TextureMaps.Keys) {
            $entry = $files.$map
            if ($null -eq $entry) { continue }
            $res = $entry.($t.res)
            if ($null -eq $res -or $null -eq $res.jpg) { Write-Warning "$($t.id): no $map at $($t.res)"; continue }
            $url = $res.jpg.url
            Save-File $url (Join-Path $dir ([IO.Path]::GetFileName($url)))
        }
        Add-Credit 'Texture' $t.id "``Textures/Nature/$($t.folder)`` ($($t.res))"
    }

    Write-Host 'Models'
    foreach ($m in $manifestData.models) {
        $files = Get-Json "$Api/files/$($m.id)"
        $fbx = $files.fbx.($m.res).fbx
        if ($null -eq $fbx) { Write-Warning "$($m.id): no FBX at $($m.res)"; continue }
        $dir = Join-Path $ProjectRoot "Assets/Art/Models/Nature/$($m.id)"
        if (-not $m.texturesOnly) { Save-File $fbx.url (Join-Path $dir ([IO.Path]::GetFileName($fbx.url))) }
        foreach ($inc in $fbx.include.PSObject.Properties) {
            Save-File $inc.Value.url (Join-Path $dir $inc.Name)
        }
        $used = if ($m.texturesOnly) { 'texture atlas only (generated foliage)' } else { "``Models/Nature/$($m.id)`` ($($m.res) FBX)" }
        Add-Credit 'Model' $m.id $used
    }

    Write-Host 'HDRIs'
    foreach ($h in $manifestData.hdris) {
        $files = Get-Json "$Api/files/$($h.id)"
        $hdr = $files.hdri.($h.res).hdr
        if ($null -eq $hdr) { Write-Warning "$($h.id): no HDR at $($h.res)"; continue }
        Save-File $hdr.url (Join-Path $ProjectRoot "Assets/Art/Sky/HDRI/$([IO.Path]::GetFileName($hdr.url))")
        Add-Credit 'HDRI' $h.id "PatataWilds act $($h.act) sky ($($h.res))"
    }
}
finally {
    Remove-Item -Recurse -Force $Scratch -ErrorAction SilentlyContinue
}

# Rewrite the Poly Haven block of CREDITS.md (everything else in the file is kept).
$creditsPath = Join-Path $ProjectRoot 'CREDITS.md'
$begin = '<!-- polyhaven:begin (written by tools/Fetch-PolyHaven.ps1, do not edit by hand) -->'
$end = '<!-- polyhaven:end -->'
$block = @($begin, '', '| Kind | Name | Asset | Authors | Licence | Used as |', '|---|---|---|---|---|---|') + $credits + @('', $end)
$text = if (Test-Path $creditsPath) { [IO.File]::ReadAllText($creditsPath) } else { '' }
$pattern = [regex]::Escape($begin) + '[\s\S]*?' + [regex]::Escape($end)
if ($text -match $pattern) {
    $text = [regex]::Replace($text, $pattern, [Text.RegularExpressions.MatchEvaluator] { param($x) ($block -join "`n") })
} else {
    $text = $text.TrimEnd() + "`n`n" + ($block -join "`n") + "`n"
}
[IO.File]::WriteAllText($creditsPath, $text, (New-Object Text.UTF8Encoding $false))

Write-Host ("Done: {0} fetched ({1:N1} MB), {2} already present." -f $script:Fetched, ($script:Bytes / 1MB), $script:Skipped)
