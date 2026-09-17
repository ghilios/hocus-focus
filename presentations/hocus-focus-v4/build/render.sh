#!/usr/bin/env bash
# Renders a .pptx to PNGs via Windows PowerPoint. Stages through a Windows temp dir because PowerPoint
# opens files on the WSL filesystem in Protected View.
# Usage: ./render.sh <deck.pptx> <out-dir> [comma-separated slide numbers]
set -euo pipefail
deck="$(realpath "$1")"; out="$(realpath -m "$2")"; slides="${3:-}"
here="$(cd "$(dirname "$0")" && pwd)"
stage="/mnt/c/Users/ghili/AppData/Local/Temp/hf-render"
rm -rf "$stage"; mkdir -p "$stage/out" "$out"
cp "$deck" "$stage/deck.pptx"; cp "$here/render.ps1" "$stage/render.ps1"
args=(-Pptx 'C:\Users\ghili\AppData\Local\Temp\hf-render\deck.pptx' -OutDir 'C:\Users\ghili\AppData\Local\Temp\hf-render\out')
[ -n "$slides" ] && args+=(-Slides "$slides")
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\ghili\AppData\Local\Temp\hf-render\render.ps1' "${args[@]}"
cp "$stage"/out/*.png "$out"/
ls "$out" | wc -l
