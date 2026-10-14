<#
.SYNOPSIS
    Downloads the CC0 Sketchfab scans listed in tools/sketchfab-assets.json, reduces each one to a game prop with Blender
    (tools/blender/sketchfab_to_fbx.py) into Assets/Art/Models/Nature (ARCHITECTURE §25.3) and rewrites the Sketchfab
    block of CREDITS.md.

.DESCRIPTION
    Needs a Sketchfab API token (free account, Settings > Password & API) in SKETCHFAB_API_TOKEN, read from the process
    or the user environment and never written anywhere. A model whose licence is not CC0 is refused.

    Incremental: a model whose FBX is already present is skipped (its credit line is still written). Every glb is
    downloaded to a fresh temporary folder, converted there, and only .fbx and .jpg files are moved into the project:

    Models -> Assets/Art/Models/Nature/<id>/<id>_<res>.fbx (+ textures/<id>_diff|nor_gl|rough_<res>.jpg)

    -Preview also writes a front and a side render of each converted prop to that temporary folder's sibling
    'sketchfab-previews' (outside the project) for review.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/Fetch-Sketchfab.ps1
#>
param(
    [string]$Manifest = (Join-Path $PSScriptRoot 'sketchfab-assets.json'),
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Blender = '',
    [switch]$Preview
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Api = 'https://api.sketchfab.com/v3'
$Allowed = @('.fbx', '.jpg')
$Converter = Join-Path $PSScriptRoot 'blender/sketchfab_to_fbx.py'

$token = $env:SKETCHFAB_API_TOKEN
if (-not $token) { $token = [Environment]::GetEnvironmentVariable('SKETCHFAB_API_TOKEN', 'User') }
if (-not $token) { throw 'SKETCHFAB_API_TOKEN is not set (Sketchfab > Settings > Password & API).' }
$Headers = @{ Authorization = "Token $token" }

if (-not $Blender) {
    $found = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object { [version]($_.Directory.Name -replace '[^0-9.]', '') } -Descending | Select-Object -First 1
    if ($null -eq $found) { throw 'Blender not found: pass -Blender <path to blender.exe>.' }
    $Blender = $found.FullName
}

$Scratch = Join-Path ([IO.Path]::GetTempPath()) ('sketchfab-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Scratch | Out-Null
$PreviewDir = Join-Path ([IO.Path]::GetTempPath()) 'sketchfab-previews'

$manifestData = Get-Content $Manifest -Raw | ConvertFrom-Json
$credits = New-Object System.Collections.Generic.List[string]
$fetched = 0
$skipped = 0

try {
    foreach ($m in $manifestData.models) {
        $info = Invoke-RestMethod -Uri "$Api/models/$($m.uid)" -Headers $Headers
        if ($info.license.slug -ne 'cc0') { Write-Warning "$($m.id): licence '$($info.license.slug)' is not CC0, refused"; continue }
        $dir = Join-Path $ProjectRoot "Assets/Art/Models/Nature/$($m.id)"
        $fbx = Join-Path $dir "$($m.id)_$($m.res).fbx"
        $credits.Add("| Model | $($info.name) | [$($m.id)]($($info.viewerUrl)) | $($info.user.displayName) | CC0 1.0 | ``Models/Nature/$($m.id)`` ($($m.res) FBX, $($m.used)) |")
        if (Test-Path $fbx) { $skipped++; continue }

        Write-Host "  $($m.id)"
        $work = Join-Path $Scratch $m.id
        $download = Join-Path $work 'download'
        $out = Join-Path $work 'out'
        New-Item -ItemType Directory -Force -Path $download, $out | Out-Null
        $links = Invoke-RestMethod -Uri "$Api/models/$($m.uid)/download" -Headers $Headers
        $glb = Join-Path $download 'model.glb'
        Invoke-WebRequest -Uri $links.glb.url -OutFile $glb -UseBasicParsing

        $rotate = if ($m.rotate) { $m.rotate } else { @(0, 0, 0) }
        $blenderArgs = @('-b', '--factory-startup', '--python-exit-code', '1', '-P', $Converter, '--', $glb, $out, $m.id,
                         [string]$m.tris, $m.res, [string]$m.height, [string]$rotate[0], [string]$rotate[1], [string]$rotate[2])
        if ($Preview) {
            New-Item -ItemType Directory -Force -Path $PreviewDir | Out-Null
            $blenderArgs += (Join-Path $PreviewDir "$($m.id).png")
        }
        $log = Join-Path $work 'blender.log'
        $ErrorActionPreference = 'Continue'   # Blender writes warnings to stderr
        & $Blender @blenderArgs 2>&1 | Out-File -FilePath $log -Encoding utf8
        $exit = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($exit -ne 0) { Write-Warning "$($m.id): Blender failed with $exit (log: $log)"; continue }

        foreach ($file in Get-ChildItem $out -Recurse -File) {
            if ($Allowed -notcontains $file.Extension.ToLowerInvariant()) { continue }
            $dest = Join-Path $dir $file.FullName.Substring($out.Length + 1)
            New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
            Move-Item -Force $file.FullName $dest
            Write-Host ("    + " + $dest.Substring($ProjectRoot.Length + 1))
        }
        $fetched++
    }
}
finally {
    Remove-Item -Recurse -Force $Scratch -ErrorAction SilentlyContinue
}

# Rewrite the Sketchfab block of CREDITS.md (everything else in the file is kept).
$creditsPath = Join-Path $ProjectRoot 'CREDITS.md'
$begin = '<!-- sketchfab:begin (written by tools/Fetch-Sketchfab.ps1, do not edit by hand) -->'
$end = '<!-- sketchfab:end -->'
$block = @($begin, '', '| Kind | Name | Asset | Author | Licence | Used as |', '|---|---|---|---|---|---|') + $credits + @('', $end)
$text = if (Test-Path $creditsPath) { [IO.File]::ReadAllText($creditsPath) } else { '' }
$pattern = [regex]::Escape($begin) + '[\s\S]*?' + [regex]::Escape($end)
if ($text -match $pattern) {
    $text = [regex]::Replace($text, $pattern, [Text.RegularExpressions.MatchEvaluator] { param($x) ($block -join "`n") })
} else {
    $text = $text.TrimEnd() + "`n`n" + ($block -join "`n") + "`n"
}
[IO.File]::WriteAllText($creditsPath, $text, (New-Object Text.UTF8Encoding $false))

Write-Host ("Done: {0} converted, {1} already present." -f $fetched, $skipped)
