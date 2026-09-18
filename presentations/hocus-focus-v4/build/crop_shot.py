#!/usr/bin/env python3
"""Crop a raw NINA capture into presentations/hocus-focus-v4/screenshots/<name>.png.

Reports the two things that actually go wrong here: an aspect that fights the slide's
box (the deck letterboxes rather than resizing the card), and too few native pixels for
the on-slide width. The deck renders 1600px for a 13.333in slide => 120 px per slide-inch,
and T.card() insets the box by 0.12in on every side.
"""
import sys, os
from PIL import Image

PX_PER_IN = 1600 / 13.333
PAD_IN = 0.12

# Outer box (inches) per shot, from build/slides/*.js
BOXES = {
    "optimizer-start":      (7.00, 4.55),
    "optimizer-summary":    (7.30, 4.30),
    "settings-import":      (4.88, 3.66),
    "donut-frame-off":      (3.62, 2.41),
    "donut-frame-on":       (3.62, 2.41),
    "af-review-controls":   (4.30, 1.60),
    "af-review-good":       (7.28, 4.55),
    "af-review-bad":        (5.50, 4.55),
    "wizard-setup":        (11.93, 3.30),
    "wizard-step":          (5.50, 2.60),
    "wizard-result":        (5.90, 4.55),
    "guidance":            (11.33, 2.60),
    "tilt-history":         (7.28, 4.55),
    "eat-connection":       (6.40, 4.55),
    "eat-review-commands":  (6.63, 4.55),
    "sim-frame-focused":    (3.75, 2.75),
    "sim-frame-defocused":  (3.75, 2.75),
    "sim-tilt-panel":       (3.75, 2.75),
}

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "screenshots")

def main():
    if len(sys.argv) < 3:
        sys.exit("usage: crop_shot.py <src.png> <name> [left top right bottom]")
    src, name = sys.argv[1], sys.argv[2]
    if name not in BOXES:
        sys.exit(f"unknown shot '{name}'; known: {', '.join(sorted(BOXES))}")
    im = Image.open(src)
    if len(sys.argv) >= 7:
        box = tuple(int(v) for v in sys.argv[3:7])
        im = im.crop(box)
    w, h = im.size
    bw, bh = BOXES[name]
    inner_w, inner_h = bw - 2 * PAD_IN, bh - 2 * PAD_IN
    box_ar, img_ar = inner_w / inner_h, w / h
    # The image is fitted into the inner box preserving aspect: the binding dimension decides
    # the on-slide width, and the other axis letterboxes.
    if img_ar >= box_ar:
        on_slide_w = inner_w * PX_PER_IN
        waste = (1 - box_ar / img_ar) * 100
        axis = "vertical"
    else:
        on_slide_w = inner_h * PX_PER_IN * img_ar
        waste = (1 - img_ar / box_ar) * 100
        axis = "horizontal"
    scale = w / on_slide_w
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.normpath(os.path.join(OUT, name + ".png"))
    im.save(dst)
    print(f"{name}: {w}x{h}  image AR {img_ar:.2f}  box AR {box_ar:.2f}")
    print(f"  on-slide width {on_slide_w:.0f}px -> native scale {scale:.2f}x"
          + ("  OK" if scale >= 1.0 else "  *** UNDER 1x: will be upscaled ***"))
    if waste > 8:
        print(f"  *** {waste:.0f}% {axis} letterbox - consider re-cropping or nudging the box in slides/ ***")
    print(f"  wrote {dst}")

if __name__ == "__main__":
    main()
