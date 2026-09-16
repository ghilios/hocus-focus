# AF-bank verification -- 19 run(s)

generated: 20260916T165754Z  |  detector commit: before  |  NoiseClip default = 4  |  pixel-scale mode: header  |  NC sweep: 4

## NoiseClippingMultiplier sweep (C0 as-default -- the honest recall reference)

| NC | median recall@SNR>=12 | median recall@all | median precision | median AF sigma_focus | median sensor R^2 | runs |
|---|---|---|---|---|---|---|
| 4 | 0.702 | 0.424 | 0.994 | 4.247 | 0.883 | 19 |

**Recommended default NoiseClippingMultiplier: 4** -- NC=4 (recall@high 0.70, precision 0.99) is the lowest swept value and still clears the precision floor 0.60 -- recall is still rising at the bottom of the range, so a lower / per-frame-adaptive NC may help

> Recall is still rising at the bottom of the swept range with acceptable precision, so a **per-frame adaptive** NoiseClippingMultiplier (derived from each frame's measured noise floor during optimization) may beat any single global default. See the per-run precision spread below.

Donut effect (A vs B over 0 run(s) with both optimized configs): donut-aware tightened AF sigma in **0** and loosened the sensor fit in **0**.

## Per-run pixel scale & match radius (V-P1 / V-P2)

| run | pixelScale (arcsec/px) | source | matchRadius (px) | source |
|---|---|---|---|---|
| CWhiteFocus/AutoFocus_20220429_000244/attempt01 | 1.326 | frame header | 12 | CLI/default |
| FlyData/AutoFocus_20260607_030642/attempt01 | 0.66 | frame header | 12 | CLI/default |
| LinwoodFocus/AutoFocus_20220329_211008/attempt01 | 0.277 | frame header | 12 | CLI/default |
| Panos/attempt01 | 1.939 | frame header | 12 | CLI/default |
| SorenVance/AutoFocus_20260711_014141/attempt01 | 2.03 | frame header | 12 | CLI/default |
| bobp/AutoFocus_20260626_215910/attempt01 | 1.524 | frame header | 12 | CLI/default |
| bobp_m101/AutoFocus_20260626_225408/attempt01 | 1.524 | frame header | 12 | CLI/default |
| caboose/AutoFocus_20220803_211249/attempt01 | 1.086 | frame header | 12 | CLI/default |
| cwhite_2026/AutoFocus_20260621_214312/attempt01 | 0.88 | frame header | 12 | CLI/default |
| fmeschia_Focus/AutoFocus_20220422_220015/attempt01 | 1.049 | frame header | 12 | CLI/default |
| lumos/AutoFocus_20260708_231255/attempt01 | 2.35 | frame header | 12 | CLI/default |
| mccomiskey/attempt01 | 0.289 | frame header | 12 | CLI/default |
| mufti/AutoFocus_20260607_030642-20260615T214722Z-3-001/attempt01 | 0.68 | frame header | 12 | CLI/default |
| muggsie/AutoFocus_20220526_025003/attempt01 | 1.034 | frame header | 12 | CLI/default |
| standard_example1/attempt01 | 1.105 | frame header | 12 | CLI/default |
| timmer/5 AutoFocus_20260614_021244/attempt01 | 5.966 | frame header | 12 | CLI/default |
| toml999/attempt01 | 0.739 | frame header | 12 | CLI/default |
| uneven/attempt01 | 0.712 | frame header | 12 | CLI/default |
| vsn07/AutoFocus_20260714_213204/attempt01 | 2.02 | frame header | 12 | CLI/default |

## Per-run (config rows)

| run | camera | golden (>=12) | donutAware | config | NC | recall@>=12 | recall@all | precision | null | scored | viol | AF sigma | AF R^2 | sensor R^2 | RMS um | sChi | tilt deg | stars | aligned |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| CWhiteFocus/AutoFocus_20220429_000244/attempt01 | 9576x6388 | 27875 (26845) | False | C0@nc4 | 4 | 0.768 | 0.748 | 0.994 | NaN | 1 | 0 | 4.247 | 0.999 | 0.162 | 11.368 | 3.035 | 0.025 | 1591 | 9/9 |
| FlyData/AutoFocus_20260607_030642/attempt01 | 3830x2555 | 3195 (2045) | False | C0@nc4 | 4 | 0.583 | 0.377 | 0.914 | NaN | 0.989 | 0 | 4.474 | 1 | 0.121 | 20.36 | 3.428 | 0.011 | 92 | 8/9 |
| LinwoodFocus/AutoFocus_20220329_211008/attempt01 | 9576x6388 | 716 (230) | True | C0@nc4 | 4 | 0.241 | 0.083 | 0.598 | NaN | 0.991 | 0 | 0.798 | 0.996 | 0.975 | 0.996 | 0.38 | 0.004 | 10 | 3/10 |
| Panos/attempt01 | 6252x4176 | 7178 (6733) | True | C0@nc4 | 4 | 0.189 | 0.177 | 0.998 | NaN | 1 | 0 | 143.208 | 0.999 | 0.997 | 152.61 | 2.439 | 5.986 | 66 | 5/7 |
| SorenVance/AutoFocus_20260711_014141/attempt01 | 6248x4176 | 0 (0) | True | C0@nc4 | 4 | NaN | NaN | NaN | NaN | NaN | 0 | 70.63 | 0.999 | NaN | NaN | NaN | NaN | 0 | 4/6 |
| bobp/AutoFocus_20260626_215910/attempt01 | 5936x3966 | 2504 (1648) | False | C0@nc4 | 4 | 0.545 | 0.37 | 0.998 | NaN | 1 | 0 | 0.418 | 0.999 | 0.775 | 1.47 | 0.43 | 0.027 | 84 | 9/9 |
| bobp_m101/AutoFocus_20260626_225408/attempt01 | 5936x3966 | 3079 (2107) | False | C0@nc4 | 4 | 0.598 | 0.424 | 0.998 | NaN | 1 | 0 | 0.556 | 0.999 | 0.851 | 1.001 | 0.207 | 0.031 | 121 | 10/10 |
| caboose/AutoFocus_20220803_211249/attempt01 | 6248x4176 | 554 (260) | False | C0@nc4 | 4 | 0.808 | 0.384 | 1 | NaN | 1 | 0 | 1.309 | 1 | 0.661 | 13.932 | 3.526 | 0.07 | 16 | 6/7 |
| cwhite_2026/AutoFocus_20260621_214312/attempt01 | 9576x6388 | 7875 (3032) | False | C0@nc4 | 4 | 0.306 | 0.118 | 0.839 | NaN | 1 | 0 | 3.427 | 0.981 | 0.983 | 3.085 | 1.003 | 0.104 | 79 | 9/9 |
| fmeschia_Focus/AutoFocus_20220422_220015/attempt01 | 4144x2822 | 868 (588) | False | C0@nc4 | 4 | 0.702 | 0.478 | 1 | NaN | 1 | 0 | 9.785 | 0.999 | 0.387 | 23.599 | 1.763 | 0.107 | 18 | 9/9 |
| lumos/AutoFocus_20260708_231255/attempt01 | 9576x6388 | 0 (0) | True | C0@nc4 | 4 | NaN | NaN | NaN | NaN | NaN | 0 | 503.559 | 0.992 | 0.975 | 543.369 | 0.919 | 2.743 | 25 | 6/23 |
| mccomiskey/attempt01 | 11664x8750 | 39702 (38450) | False | C0@nc4 | 4 | 0.847 | 0.825 | 0.99 | NaN | 1 | 0 | 3.155 | 0.99 | 0.932 | 3.603 | 0.83 | 0.013 | 2762 | 9/9 |
| mufti/AutoFocus_20260607_030642-20260615T214722Z-3-001/attempt01 | 9576x6388 | 5612 (5145) | True | C0@nc4 | 4 | 0.262 | 0.243 | 0.975 | NaN | 1 | 0 | 8.296 | 0.989 | 0.995 | 2.925 | 1.01 | 0.307 | 72 | 7/7 |
| muggsie/AutoFocus_20220526_025003/attempt01 | 4144x2822 | 4421 (3260) | False | C0@nc4 | 4 | 0.842 | 0.639 | 0.995 | NaN | 1 | 0 | 15.143 | 0.998 | 0.915 | 25.27 | 1.001 | 0.518 | 220 | 9/9 |
| standard_example1/attempt01 | 9576x6388 | 7540 (6580) | False | C0@nc4 | 4 | 0.858 | 0.775 | 0.994 | NaN | 1 | 0 | 22.934 | 0.995 | 0.94 | 44.085 | 2.645 | 0.802 | 548 | 8/8 |
| timmer/5 AutoFocus_20260614_021244/attempt01 | 6248x4176 | 112190 (109830) | False | C0@nc4 | 4 | 0.299 | 0.293 | 0.995 | NaN | 1 | 0 | 0.932 | 0.997 | 0.765 | 2.472 | 0.55 | 0.005 | 2176 | 9/9 |
| toml999/attempt01 | 4656x3520 | 9096 (7979) | False | C0@nc4 | 4 | 0.819 | 0.769 | 0.95 | NaN | 1 | 0 | 0.423 | 0.999 | 0.19 | 1.615 | 0.552 | 0.006 | 619 | 9/9 |
| uneven/attempt01 | 9576x6388 | 6318 (4992) | False | C0@nc4 | 4 | 0.901 | 0.72 | 0.999 | NaN | 1 | 0 | 9.758 | 1 | 0.664 | 49.752 | 3.529 | 0.255 | 349 | 10/10 |
| vsn07/AutoFocus_20260714_213204/attempt01 | 6252x4176 | 2864 (1881) | False | C0@nc4 | 4 | 0.726 | 0.486 | 0.98 | NaN | 1 | 0 | 2.474 | 0.996 | 0.987 | 2.391 | 0.805 | 0.203 | 95 | 7/7 |

**null** is the precision the same detections earn after being translated with wraparound -- chance alone. It is the floor this metric can read; a precision of 1.000 means the detector found no false positives only when null is near 0. **scored** is the fraction of detections that entered the precision ratio at all (the rest sit on reference objects that cannot be judged). **viol** counts scored false positives sitting on a real rendered star and MUST be 0 -- see F31.
