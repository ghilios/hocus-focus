# AF-bank verification -- 19 run(s)

generated: 20260916T164029Z  |  detector commit: after  |  NoiseClip default = 4  |  pixel-scale mode: header  |  NC sweep: 4

## NoiseClippingMultiplier sweep (C0 as-default -- the honest recall reference)

| NC | median recall@SNR>=12 | median recall@all | median precision | median AF sigma_focus | median sensor R^2 | runs |
|---|---|---|---|---|---|---|
| 4 | 0.668 | 0.439 | 0.993 | 3.595 | 0.915 | 19 |

**Recommended default NoiseClippingMultiplier: 4** -- NC=4 (recall@high 0.67, precision 0.99) is the lowest swept value and still clears the precision floor 0.60 -- recall is still rising at the bottom of the range, so a lower / per-frame-adaptive NC may help

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
| CWhiteFocus/AutoFocus_20220429_000244/attempt01 | 9576x6388 | 27875 (26845) | False | C0@nc4 | 4 | 0.778 | 0.763 | 0.987 | NaN | 1 | 0 | 3.595 | 0.999 | 0.205 | 2.735 | 1.351 | 0.007 | 1617 | 9/9 |
| FlyData/AutoFocus_20260607_030642/attempt01 | 3830x2555 | 3195 (2045) | False | C0@nc4 | 4 | 0.627 | 0.407 | 0.915 | NaN | 0.983 | 0 | 1.653 | 1 | 0.102 | 4.171 | 1.221 | 0.007 | 102 | 9/9 |
| LinwoodFocus/AutoFocus_20220329_211008/attempt01 | 9576x6388 | 716 (230) | True | C0@nc4 | 4 | 0.248 | 0.084 | 0.581 | NaN | 0.992 | 0 | 0.79 | 0.996 | 0.981 | 0.207 | 0.023 | 0.001 | 11 | 3/10 |
| Panos/attempt01 | 6252x4176 | 7178 (6733) | True | C0@nc4 | 4 | 0.19 | 0.178 | 0.998 | NaN | 1 | 0 | 137.432 | 0.999 | 0.991 | 35.659 | 1.199 | 1.606 | 66 | 5/7 |
| SorenVance/AutoFocus_20260711_014141/attempt01 | 6248x4176 | 0 (0) | True | C0@nc4 | 4 | NaN | NaN | NaN | NaN | NaN | 0 | 70.63 | 0.999 | NaN | NaN | NaN | NaN | 0 | 4/6 |
| bobp/AutoFocus_20260626_215910/attempt01 | 5936x3966 | 2504 (1648) | False | C0@nc4 | 4 | 0.545 | 0.37 | 0.998 | NaN | 1 | 0 | 0.418 | 0.999 | 0.775 | 0.35 | 0.033 | 0.007 | 82 | 9/9 |
| bobp_m101/AutoFocus_20260626_225408/attempt01 | 5936x3966 | 3079 (2107) | False | C0@nc4 | 4 | 0.598 | 0.424 | 0.998 | NaN | 1 | 0 | 0.556 | 0.999 | 0.849 | 0.269 | 0.019 | 0.008 | 123 | 10/10 |
| caboose/AutoFocus_20220803_211249/attempt01 | 6248x4176 | 554 (260) | False | C0@nc4 | 4 | 0.954 | 0.451 | 1 | NaN | 1 | 0 | 3.557 | 0.999 | 0.93 | 1.249 | 0.349 | 0.037 | 17 | 6/7 |
| cwhite_2026/AutoFocus_20260621_214312/attempt01 | 9576x6388 | 7875 (3032) | False | C0@nc4 | 4 | 0.47 | 0.181 | 0.831 | NaN | 1 | 0 | 3.445 | 0.962 | 0.982 | 0.719 | 0.123 | 0.028 | 97 | 9/9 |
| fmeschia_Focus/AutoFocus_20220422_220015/attempt01 | 4144x2822 | 868 (588) | False | C0@nc4 | 4 | 0.947 | 0.645 | 1 | NaN | 1 | 0 | 18.182 | 0.992 | 0.414 | 4.425 | 1.932 | 0.037 | 18 | 9/9 |
| lumos/AutoFocus_20260708_231255/attempt01 | 9576x6388 | 0 (0) | True | C0@nc4 | 4 | NaN | NaN | NaN | NaN | NaN | 0 | 556.729 | 0.981 | 0.987 | 82.365 | 0.391 | 0.164 | 24 | 7/23 |
| mccomiskey/attempt01 | 11664x8750 | 39702 (38450) | False | C0@nc4 | 4 | 0.862 | 0.837 | 0.986 | NaN | 1 | 0 | 3.074 | 0.99 | 0.925 | 0.835 | 0.14 | 0.003 | 2765 | 9/9 |
| mufti/AutoFocus_20260607_030642-20260615T214722Z-3-001/attempt01 | 9576x6388 | 5612 (5145) | True | C0@nc4 | 4 | 0.267 | 0.246 | 0.981 | NaN | 1 | 0 | 9.272 | 0.985 | 0.994 | 0.815 | 0.156 | 0.081 | 74 | 7/7 |
| muggsie/AutoFocus_20220526_025003/attempt01 | 4144x2822 | 4421 (3260) | False | C0@nc4 | 4 | 0.91 | 0.7 | 0.991 | NaN | 1 | 0 | 18.978 | 0.998 | 0.911 | 7.083 | 1.087 | 0.146 | 238 | 9/9 |
| standard_example1/attempt01 | 9576x6388 | 7540 (6580) | False | C0@nc4 | 4 | 0.867 | 0.785 | 0.993 | NaN | 1 | 0 | 20.6 | 0.996 | 0.919 | 11.897 | 3.449 | 0.214 | 549 | 8/8 |
| timmer/5 AutoFocus_20260614_021244/attempt01 | 6248x4176 | 112190 (109830) | False | C0@nc4 | 4 | 0.299 | 0.293 | 0.995 | NaN | 1 | 0 | 0.932 | 0.997 | 0.73 | 0.625 | 0.08 | 0.001 | 2195 | 9/9 |
| toml999/attempt01 | 4656x3520 | 9096 (7979) | False | C0@nc4 | 4 | 0.848 | 0.819 | 0.905 | NaN | 1 | 0 | 0.462 | 0.999 | 0.191 | 0.423 | 0.044 | 0.002 | 661 | 9/9 |
| uneven/attempt01 | 9576x6388 | 6318 (4992) | False | C0@nc4 | 4 | 0.974 | 0.782 | 0.997 | NaN | 1 | 0 | 11.464 | 1 | 0.704 | 12.422 | 2.663 | 0.073 | 361 | 10/10 |
| vsn07/AutoFocus_20260714_213204/attempt01 | 6252x4176 | 2864 (1881) | False | C0@nc4 | 4 | 0.668 | 0.439 | 1 | NaN | 1 | 0 | 4.004 | 0.991 | 0.989 | 0.521 | 0.071 | 0.053 | 80 | 7/7 |

**null** is the precision the same detections earn after being translated with wraparound -- chance alone. It is the floor this metric can read; a precision of 1.000 means the detector found no false positives only when null is near 0. **scored** is the fraction of detections that entered the precision ratio at all (the rest sit on reference objects that cannot be judged). **viol** counts scored false positives sitting on a real rendered star and MUST be 0 -- see F31.
