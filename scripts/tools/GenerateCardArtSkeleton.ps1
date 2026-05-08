param(
    [switch]$OverwriteExistingArt,
    [switch]$OverwriteLegacyPlaceholderArt
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$cardDataDir = Join-Path $projectRoot 'data/cards'
$artRoot = Join-Path $projectRoot 'resources/textures/cards/art'

function Get-ClassAccentHex([string]$className) {
    switch ($className.ToLowerInvariant()) {
        'vanguard' { return '#e64545' }
        'psion' { return '#8f5cff' }
        'netrunner' { return '#13c6df' }
        'symbiote' { return '#23cf66' }
        'colorless' { return '#b6bec9' }
        default { return '#8da0b8' }
    }
}

function Get-TypeAccentHex([string]$typeName) {
    switch ($typeName.ToLowerInvariant()) {
        'attack' { return '#ff4a4a' }
        'skill' { return '#2f8fff' }
        'power' { return '#ffce45' }
        default { return '#cbd1d8' }
    }
}

function Test-IsLegacyPlaceholderArt([string]$content) {
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $false
    }

    $hasLegacyFrame = $content -match '<rect x="114" y="86" width="410" height="520" rx="38" fill="url\(#neutralFog\)" opacity="0\.58"\s*/>' -and
        $content -match '<rect x="138" y="118" width="362" height="456" rx="32"'
    $hasLegacyScan = $content -match '<rect x="142" y="124" width="344" height="170" fill="url\(#scan\)" opacity="0\.26"\s*/>'

    return $hasLegacyFrame -and $hasLegacyScan
}

function Get-ClassMotifMarkup([string]$className, [int]$seed, [string]$classColor, [string]$typeColor) {
    switch ($className.ToLowerInvariant()) {
        'vanguard' {
            $coreX = 300 + ($seed % 30)
            $coreY = 322 + ($seed % 34)
            $flareX = 438 + (($seed * 3) % 24)
            return @"
    <ellipse cx="$coreX" cy="$($coreY + 18)" rx="162" ry="204" fill="url(#classMist)" opacity="0.18"/>
    <path d="M170 628 C244 514, 308 424, 438 246" stroke="$classColor" stroke-opacity="0.24" stroke-width="18" fill="none" stroke-linecap="round"/>
    <path d="M224 666 C314 530, 388 420, $flareX 290" stroke="$typeColor" stroke-opacity="0.16" stroke-width="10" fill="none" stroke-linecap="round"/>
    <path d="M208 578 L412 286" stroke="#fff3cf" stroke-opacity="0.18" stroke-width="4" fill="none" stroke-linecap="round"/>
    <circle cx="$($coreX - 70)" cy="$($coreY - 40)" r="26" fill="$classColor" fill-opacity="0.12"/>
    <circle cx="$($coreX + 54)" cy="$($coreY - 6)" r="16" fill="#ffe5b4" fill-opacity="0.12"/>
    <path d="M$($coreX - 24) $($coreY - 134) L$($coreX - 6) $($coreY - 168) L$($coreX + 10) $($coreY - 128) Z" fill="#fff4cf" fill-opacity="0.14"/>
"@
        }
        'psion' {
            $centerX = 320 + ($seed % 24)
            $centerY = 332 + ($seed % 32)
            return @"
    <ellipse cx="$centerX" cy="$($centerY + 50)" rx="170" ry="218" fill="url(#classMist)" opacity="0.16"/>
    <ellipse cx="$centerX" cy="$centerY" rx="160" ry="114" fill="none" stroke="$classColor" stroke-opacity="0.18" stroke-width="4"/>
    <ellipse cx="$centerX" cy="$centerY" rx="126" ry="88" fill="none" stroke="#f7f2ff" stroke-opacity="0.16" stroke-width="2"/>
    <path d="M$centerX 208 L$($centerX + 72) 332 L$centerX 456 L$($centerX - 72) 332 Z" fill="#f7f2ff" fill-opacity="0.05" stroke="#ffffff" stroke-opacity="0.14" stroke-width="2"/>
    <circle cx="$($centerX - 118)" cy="$($centerY - 48)" r="10" fill="#ffffff" fill-opacity="0.12"/>
    <circle cx="$($centerX + 126)" cy="$($centerY + 24)" r="14" fill="$classColor" fill-opacity="0.10"/>
    <circle cx="$($centerX - 86)" cy="$($centerY + 142)" r="8" fill="$typeColor" fill-opacity="0.14"/>
    <path d="M184 610 C248 564, 378 552, 462 590" stroke="#f5efff" stroke-opacity="0.16" stroke-width="5" fill="none" stroke-linecap="round"/>
"@
        }
        'netrunner' {
            $colA = 208 + ($seed % 20)
            $colB = 304 + (($seed * 2) % 20)
            $colC = 404 + (($seed * 3) % 20)
            return @"
    <ellipse cx="320" cy="352" rx="176" ry="208" fill="url(#classMist)" opacity="0.10"/>
    <rect x="$colA" y="168" width="16" height="286" rx="8" fill="$classColor" fill-opacity="0.12" transform="rotate(-6 $colA 168)"/>
    <rect x="$colB" y="146" width="12" height="338" rx="6" fill="#f3fbff" fill-opacity="0.09" transform="rotate(4 $colB 146)"/>
    <rect x="$colC" y="188" width="14" height="250" rx="7" fill="$typeColor" fill-opacity="0.14" transform="rotate(-3 $colC 188)"/>
    <path d="M172 248 L474 248" stroke="$typeColor" stroke-opacity="0.08" stroke-width="2" stroke-dasharray="12 14"/>
    <path d="M184 334 L470 334" stroke="$classColor" stroke-opacity="0.06" stroke-width="2" stroke-dasharray="10 18"/>
    <path d="M196 602 L456 342" stroke="#f3fbff" stroke-opacity="0.10" stroke-width="3" stroke-linecap="round"/>
    <circle cx="$($colA + 36)" cy="290" r="14" fill="$classColor" fill-opacity="0.10"/>
    <circle cx="$($colB + 60)" cy="384" r="9" fill="#ffffff" fill-opacity="0.12"/>
"@
        }
        'symbiote' {
            $coreX = 316 + ($seed % 24)
            $coreY = 356 + ($seed % 30)
            return @"
    <ellipse cx="$coreX" cy="$($coreY + 20)" rx="176" ry="216" fill="url(#classMist)" opacity="0.18"/>
    <path d="M184 610 C214 500, 272 432, 338 394 C390 364, 430 314, 452 238" stroke="$classColor" stroke-opacity="0.18" stroke-width="16" fill="none" stroke-linecap="round"/>
    <path d="M222 650 C274 540, 356 486, 430 460 C486 440, 516 390, 530 322" stroke="$typeColor" stroke-opacity="0.12" stroke-width="8" fill="none" stroke-linecap="round"/>
    <ellipse cx="$($coreX - 76)" cy="$($coreY + 26)" rx="78" ry="56" fill="$classColor" fill-opacity="0.12" stroke="#f0fff0" stroke-opacity="0.06" stroke-width="2"/>
    <ellipse cx="$($coreX + 14)" cy="$($coreY - 26)" rx="98" ry="70" fill="#f4fff4" fill-opacity="0.05" stroke="#ffffff" stroke-opacity="0.08" stroke-width="2"/>
    <ellipse cx="$($coreX + 104)" cy="$($coreY + 78)" rx="56" ry="42" fill="$typeColor" fill-opacity="0.10"/>
    <circle cx="$($coreX - 30)" cy="$($coreY - 16)" r="12" fill="#ffffff" fill-opacity="0.08"/>
    <circle cx="$($coreX + 76)" cy="$($coreY + 30)" r="10" fill="$classColor" fill-opacity="0.12"/>
"@
        }
        default {
            return @"
    <ellipse cx="320" cy="352" rx="168" ry="208" fill="url(#classMist)" opacity="0.12"/>
    <circle cx="320" cy="352" r="94" fill="#ffffff" fill-opacity="0.04"/>
"@
        }
    }
}

function Get-TypeOverlayMarkup([string]$typeName, [int]$seed, [string]$typeColor) {
    switch ($typeName.ToLowerInvariant()) {
        'attack' {
            $y = 214 + ($seed % 44)
            return @"
    <path d="M156 $($y + 292) L474 $y" stroke="$typeColor" stroke-opacity="0.16" stroke-width="20" stroke-linecap="round"/>
    <path d="M212 $($y + 322) L520 $($y + 34)" stroke="#ffffff" stroke-opacity="0.09" stroke-width="5" stroke-linecap="round"/>
    <circle cx="460" cy="$($y + 24)" r="18" fill="$typeColor" fill-opacity="0.08"/>
"@
        }
        'skill' {
            $gridY = 232 + ($seed % 28)
            return @"
    <path d="M170 $gridY L474 $gridY" stroke="$typeColor" stroke-opacity="0.08" stroke-width="2" stroke-dasharray="10 14"/>
    <path d="M184 $($gridY + 88) L460 $($gridY + 88)" stroke="$typeColor" stroke-opacity="0.06" stroke-width="2" stroke-dasharray="8 16"/>
    <path d="M220 $($gridY - 54) L220 $($gridY + 194)" stroke="#ffffff" stroke-opacity="0.06" stroke-width="2" stroke-dasharray="8 14"/>
    <path d="M392 $($gridY - 24) L392 $($gridY + 206)" stroke="$typeColor" stroke-opacity="0.06" stroke-width="2" stroke-dasharray="10 16"/>
    <circle cx="220" cy="$gridY" r="6" fill="$typeColor" fill-opacity="0.18"/>
    <circle cx="392" cy="$($gridY + 88)" r="7" fill="#ffffff" fill-opacity="0.12"/>
"@
        }
        'power' {
            return @"
    <ellipse cx="320" cy="330" rx="192" ry="138" fill="none" stroke="$typeColor" stroke-opacity="0.12" stroke-width="10"/>
    <ellipse cx="320" cy="330" rx="136" ry="96" fill="none" stroke="#fff4ce" stroke-opacity="0.12" stroke-width="4"/>
    <ellipse cx="320" cy="330" rx="84" ry="58" fill="$typeColor" fill-opacity="0.05"/>
"@
        }
        default {
            return ''
        }
    }
}

function New-CardSvg([pscustomobject]$card) {
    $classColor = Get-ClassAccentHex $card.class
    $typeColor = Get-TypeAccentHex $card.type
    $seed = ([int[]][char[]]$card.id | Measure-Object -Sum).Sum
    $classMotif = Get-ClassMotifMarkup $card.class $seed $classColor $typeColor
    $typeOverlay = Get-TypeOverlayMarkup $card.type $seed $typeColor

@"
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 640 896">
  <defs>
    <radialGradient id="classMist" cx="50%" cy="42%" r="56%">
      <stop offset="0%" stop-color="$classColor" stop-opacity="0.24"/>
      <stop offset="72%" stop-color="$classColor" stop-opacity="0.04"/>
      <stop offset="100%" stop-color="$classColor" stop-opacity="0"/>
    </radialGradient>
  </defs>

  <!-- transparent placeholder art generated by GenerateCardArtSkeleton.ps1 -->
  <!-- card: $($card.id) | class: $($card.class) | type: $($card.type) -->
  <g>
$classMotif
$typeOverlay
  </g>
</svg>
"@
}

$cardFiles = Get-ChildItem -Path $cardDataDir -Filter '*.json' | Sort-Object Name
$totalCards = 0
$updatedCards = 0
$createdArt = 0
$overwrittenLegacyArt = 0
$forceOverwrittenArt = 0
$skippedArt = 0

New-Item -ItemType Directory -Path $artRoot -Force | Out-Null

foreach ($file in $cardFiles) {
    $raw = Get-Content -Path $file.FullName -Raw -Encoding UTF8
    $cards = $raw | ConvertFrom-Json

    foreach ($card in $cards) {
        $totalCards++
        $className = $card.class.ToString()
        $cardId = $card.id.ToString()
        $relativeArtPath = "$className/$cardId.svg"
        $classArtDir = Join-Path $artRoot $className
        New-Item -ItemType Directory -Path $classArtDir -Force | Out-Null

        $artFile = Join-Path $classArtDir "$cardId.svg"
        $artFileExists = Test-Path $artFile
        $shouldWriteArt = $OverwriteExistingArt -or -not $artFileExists
        $overwritingLegacy = $false

        if (-not $shouldWriteArt -and $OverwriteLegacyPlaceholderArt) {
            $existingArt = Get-Content -Path $artFile -Raw -Encoding UTF8
            if (Test-IsLegacyPlaceholderArt $existingArt) {
                $shouldWriteArt = $true
                $overwritingLegacy = $true
            }
        }

        if ($shouldWriteArt) {
            Set-Content -Path $artFile -Value (New-CardSvg $card) -Encoding UTF8
            if (-not $artFileExists) {
                $createdArt++
            }
            elseif ($overwritingLegacy) {
                $overwrittenLegacyArt++
            }
            else {
                $forceOverwrittenArt++
            }
        }
        else {
            $skippedArt++
        }

        if (-not $card.PSObject.Properties.Name.Contains('artPath')) {
            $nameEn = [regex]::Escape($card.nameEn.ToString())
            $classEscaped = [regex]::Escape($className)
            $pattern = '"nameEn"\s*:\s*"' + $nameEn + '",\r?\n(?<indent>\s*)"class"\s*:\s*"' + $classEscaped + '"'
            $replacement = '"nameEn": "' + $card.nameEn + '",' + [Environment]::NewLine + '${indent}"artPath": "' + $relativeArtPath.Replace('\', '/') + '",' + [Environment]::NewLine + '${indent}"class": "' + $className + '"'
            $updated = [regex]::Replace($raw, $pattern, $replacement, 1)
            if ($updated -ne $raw) {
                $raw = $updated
                $updatedCards++
            }
        }
    }

    Set-Content -Path $file.FullName -Value $raw -Encoding UTF8
}

Write-Host "Card files processed: $($cardFiles.Count)"
Write-Host "Cards discovered: $totalCards"
Write-Host "artPath fields inserted: $updatedCards"
Write-Host "Art placeholders created: $createdArt"
Write-Host "Legacy placeholder art overwritten: $overwrittenLegacyArt"
Write-Host "Forced art overwrites: $forceOverwrittenArt"
Write-Host "Art placeholders skipped: $skippedArt"