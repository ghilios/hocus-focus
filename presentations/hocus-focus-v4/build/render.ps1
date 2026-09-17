# Exports slides of a .pptx to PNG using the installed PowerPoint (COM automation).
# Usage: powershell.exe -ExecutionPolicy Bypass -File render.ps1 -Pptx C:\path\deck.pptx -OutDir C:\path\out [-Slides 1,5,7] [-Width 1600]
param(
    [Parameter(Mandatory = $true)][string]$Pptx,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$Slides = "",
    [int]$Width = 1600
)
$ErrorActionPreference = "Stop"
$want = @(); if ($Slides -ne "") { $want = $Slides.Split(",") | ForEach-Object { [int]$_ } }
if (!(Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
$app = New-Object -ComObject PowerPoint.Application
try {
    # ReadOnly, no title, no window
    $pres = $app.Presentations.Open($Pptx, $true, $false, $false)
    $height = [int]($Width * $pres.PageSetup.SlideHeight / $pres.PageSetup.SlideWidth)
    foreach ($slide in $pres.Slides) {
        if ($want.Count -gt 0 -and ($want -notcontains $slide.SlideIndex)) { continue }
        $name = "slide-{0:D2}.png" -f $slide.SlideIndex
        $slide.Export((Join-Path $OutDir $name), "PNG", $Width, $height)
    }
    Write-Output ("Exported from {0} slides" -f $pres.Slides.Count)
    $pres.Close()
}
finally {
    # Only quit PowerPoint if we are the sole user of it
    if ($app.Presentations.Count -eq 0) { $app.Quit() }
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($app) | Out-Null
}
