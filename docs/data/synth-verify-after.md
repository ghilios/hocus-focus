# AF-bank verification -- 22 run(s)

generated: 20260916T182709Z  |  detector commit: after  |  NoiseClip default = 4  |  pixel-scale mode: header  |  NC sweep: 4

profile: `Default-2026-09-16T12:39:25 (5902da3a-df3b-4d86-9e06-c9557ebbf679)`  |  settings: `\\wsl.localhost\Ubuntu-20.04\tmp\claude-1000\-home-ghilios-src-hocus-focus\62319d24-d189-4732-93f8-fe9d2a22498d\scratchpad\val\bank_settings.json` (pinned)

> Comparable to another report only when the profile and settings lines above match it.

## NoiseClippingMultiplier sweep (C0 as-default -- the honest recall reference)

| NC | median recall@SNR>=12 | median recall@all | median precision | median AF sigma_focus | median sensor R^2 | runs |
|---|---|---|---|---|---|---|
| 4 | 0.904 | 0.63 | 1 | 0.663 | 0.023 | 22 |

**Recommended default NoiseClippingMultiplier: 4** -- NC=4 (recall@high 0.90, precision 1.00) is the lowest swept value and still clears the precision floor 0.60 -- recall is still rising at the bottom of the range, so a lower / per-frame-adaptive NC may help

> Recall is still rising at the bottom of the swept range with acceptable precision, so a **per-frame adaptive** NoiseClippingMultiplier (derived from each frame's measured noise floor during optimization) may beat any single global default. See the per-run precision spread below.

Donut effect (A vs B over 0 run(s) with both optimized configs): donut-aware tightened AF sigma in **0** and loosened the sensor fit in **0**.

## Per-run pixel scale & match radius (V-P1 / V-P2)

| run | pixelScale (arcsec/px) | source | matchRadius (px) | source |
|---|---|---|---|---|
| D01_ultrawide_40mm/attempt01 | 19.389 | frame header | 12 | synthetic_meta.json |
| D02_rich_135mm/attempt01 | 5.745 | frame header | 12 | synthetic_meta.json |
| D03_redcat_250mm/attempt01 | 3.102 | frame header | 12 | synthetic_meta.json |
| D04_esprit_550mm/attempt01 | 1.41 | frame header | 12 | synthetic_meta.json |
| D05_tec140_1000mm/attempt01 | 0.776 | frame header | 12 | synthetic_meta.json |
| D06_sparse_1000mm/attempt01 | 0.776 | frame header | 12 | synthetic_meta.json |
| D07_rc10_2000mm/attempt01 | 0.388 | frame header | 12 | synthetic_meta.json |
| D08_c11_2800mm/attempt01 | 0.277 | frame header | 12 | synthetic_meta.json |
| D09_c14_3800mm/attempt01 | 0.251 | frame header | 12 | synthetic_meta.json |
| D10_rc16_3250mm_sparse/attempt01 | 0.239 | frame header | 12 | synthetic_meta.json |
| D11_rc10_585_afbin2/attempt01 | 0.598 | frame header | 12 | synthetic_meta.json |
| D12_c14_585_afbin2/attempt01 | 0.315 | frame header | 12 | synthetic_meta.json |
| D13_apo200_1800mm/attempt01 | 0.431 | frame header | 12 | synthetic_meta.json |
| D14_cdk14_2563mm_e47/attempt01 | 0.303 | frame header | 12 | synthetic_meta.json |
| D15_cdk20_3454mm_e47/attempt01 | 0.225 | frame header | 12 | synthetic_meta.json |
| D16_esprit550_ha3/attempt01 | 1.41 | frame header | 12 | synthetic_meta.json |
| D17_cdk14_oiii5/attempt01 | 0.303 | frame header | 12 | synthetic_meta.json |
| D18_m24_deep_shed/attempt01 | 1.41 | frame header | 12 | synthetic_meta.json |
| D19_cygnus_deep_shed/attempt01 | 0.776 | frame header | 12 | synthetic_meta.json |
| D20_m24_bright_control/attempt01 | 1.41 | frame header | 12 | synthetic_meta.json |
| D21_widefield_60mm/attempt01 | 12.926 | frame header | 12 | synthetic_meta.json |
| D22_widefield_100mm/attempt01 | 7.756 | frame header | 12 | synthetic_meta.json |

## Per-run (config rows)

| run | camera | golden (>=12) | donutAware | config | NC | recall@>=12 | recall@all | precision | null | scored | viol | AF sigma | AF R^2 | sensor R^2 | RMS um | sChi | tilt deg | stars | aligned |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| D01_ultrawide_40mm/attempt01 | 6248x4176 | 110384 (62411) | False | C0@nc4 | 4 | 0.158 | 0.22 | 1 | 0.192 | 0.968 | 0 | 0.657 | 0.911 | 0.005 | 0.224 | 0.012 | 0 | 2057 | 7/9 |
| D02_rich_135mm/attempt01 | 6248x4176 | 31144 (26328) | False | C0@nc4 | 4 | 0.541 | 0.604 | 1 | 0.068 | 0.991 | 0 | 0.599 | 0.768 | 0 | 0.104 | 0.003 | 0 | 2867 | 6/9 |
| D03_redcat_250mm/attempt01 | 3008x3008 | 7102 (4627) | False | C0@nc4 | 4 | 0.399 | 0.448 | 1 | 0.035 | 0.99 | 0 | 0.701 | 0.863 | 0.038 | 0.327 | 0.023 | 0.001 | 341 | 6/9 |
| D04_esprit_550mm/attempt01 | 6248x4176 | 45823 (22109) | False | C0@nc4 | 4 | 0.665 | 0.608 | 1 | 0.112 | 0.979 | 0 | 0.544 | 0.998 | 0.002 | 0.352 | 0.028 | 0 | 2179 | 9/9 |
| D05_tec140_1000mm/attempt01 | 9576x6388 | 11523 (4413) | False | C0@nc4 | 4 | 0.986 | 0.8 | 1 | 0.014 | 0.995 | 0 | 0.245 | 1 | 0.016 | 0.706 | 0.096 | 0.001 | 794 | 9/9 |
| D06_sparse_1000mm/attempt01 | 3008x3008 | 356 (167) | False | C0@nc4 | 4 | 0.964 | 0.82 | 1 | 0 | 0.986 | 0 | 0.222 | 1 | 0.141 | 0.929 | 0.244 | 0.006 | 27 | 9/9 |
| D07_rc10_2000mm/attempt01 | 3008x3008 | 1471 (593) | True | C0@nc4 | 4 | 0.985 | 0.774 | 1 | 0.032 | 0.948 | 0 | 0.123 | 1 | 0.049 | 0.775 | 0.072 | 0.003 | 103 | 9/9 |
| D08_c11_2800mm/attempt01 | 6248x4176 | 315 (101) | True | C0@nc4 | 4 | 1 | 0.8 | 1 | 0 | 0.996 | 0 | 1.098 | 0.999 | 0.35 | 1.496 | 0.446 | 0.004 | 19 | 5/9 |
| D09_c14_3800mm/attempt01 | 4144x2822 | 234 (90) | True | C0@nc4 | 4 | 0.911 | 0.581 | 1 | 0 | 1 | 0 | 1.598 | 0.999 | 0.513 | 0.594 | 0.055 | 0.018 | 11 | 5/9 |
| D10_rc16_3250mm_sparse/attempt01 | 3008x3008 | 115 (58) | True | C0@nc4 | 4 | 1 | 0.739 | 1 | 0 | 1 | 0 | 1.595 | 0.999 | NaN | NaN | NaN | NaN | 0 | 7/9 |
| D11_rc10_585_afbin2/attempt01 | 1920x1080 | 333 (133) | True | C0@nc4 | 4 | 0.887 | 0.61 | 1 | 0 | 0.985 | 0 | 0.259 | 1 | 0.132 | 0.358 | 0.042 | 0.006 | 19 | 7/9 |
| D12_c14_585_afbin2/attempt01 | 1920x1080 | 329 (107) | True | C0@nc4 | 4 | 0.897 | 0.502 | 1 | 0 | 0.954 | 0 | 0.924 | 1 | 0.023 | 0.428 | 0.04 | 0.002 | 11 | 5/9 |
| D13_apo200_1800mm/attempt01 | 6248x4176 | 790 (277) | False | C0@nc4 | 4 | 1 | 0.895 | 1 | 0.003 | 0.985 | 0 | 3.195 | 0.998 | 0.014 | 2.334 | 0.311 | 0.002 | 58 | 9/9 |
| D14_cdk14_2563mm_e47/attempt01 | 9576x6388 | 1197 (384) | True | C0@nc4 | 4 | 0.979 | 0.758 | 1 | 0.001 | 0.992 | 0 | 0.935 | 0.999 | 0.065 | 0.774 | 0.068 | 0 | 67 | 9/9 |
| D15_cdk20_3454mm_e47/attempt01 | 6248x4176 | 254 (87) | True | C0@nc4 | 4 | 0.931 | 0.65 | 1 | 0 | 0.976 | 0 | 1.33 | 0.998 | 0.419 | 0.63 | 0.068 | 0.003 | 11 | 7/9 |
| D16_esprit550_ha3/attempt01 | 6248x4176 | 818 (184) | False | C0@nc4 | 4 | 0.679 | 0.379 | 1 | 0 | 0.984 | 0 | 0.669 | 0.996 | 0.253 | 0.418 | 0.051 | 0.003 | 22 | 9/9 |
| D17_cdk14_oiii5/attempt01 | 3008x3008 | 208 (61) | True | C0@nc4 | 4 | 1 | 0.803 | 1 | 0 | 0.988 | 0 | 0.934 | 0.999 | 0.166 | 1.069 | 0.092 | 0.004 | 14 | 6/9 |
| D18_m24_deep_shed/attempt01 | 6248x4176 | 82825 (21847) | False | C0@nc4 | 4 | 0.658 | 0.334 | 1 | 0.231 | 0.957 | 0 | 0.506 | 0.995 | 0.001 | 0.358 | 0.029 | 0 | 2117 | 9/9 |
| D19_cygnus_deep_shed/attempt01 | 9576x6388 | 12683 (4873) | False | C0@nc4 | 4 | 0.985 | 0.734 | 1 | 0.014 | 0.988 | 0 | 0.279 | 1 | 0.004 | 0.625 | 0.07 | 0 | 837 | 9/9 |
| D20_m24_bright_control/attempt01 | 6248x4176 | 10029 (8265) | False | C0@nc4 | 4 | 0.858 | 0.882 | 1 | 0.02 | 0.993 | 0 | 0.194 | 1 | 0.006 | 0.195 | 0.009 | 0 | 981 | 9/9 |
| D21_widefield_60mm/attempt01 | 6248x4176 | 57133 (33805) | False | C0@nc4 | 4 | 0.101 | 0.173 | 1 | 0.088 | 0.966 | 0 | 0.626 | 0.811 | 0.023 | 0.286 | 0.018 | 0 | 411 | 6/9 |
| D22_widefield_100mm/attempt01 | 3008x3008 | 13672 (4204) | False | C0@nc4 | 4 | 0.14 | 0.098 | 1 | 0.033 | 0.973 | 0 | 2.598 | 0.548 | 0.002 | 0.246 | 0.015 | 0 | 102 | 6/9 |

**null** is the precision the same detections earn after being translated with wraparound -- chance alone. It is the floor this metric can read; a precision of 1.000 means the detector found no false positives only when null is near 0. **scored** is the fraction of detections that entered the precision ratio at all (the rest sit on reference objects that cannot be judged). **viol** counts scored false positives sitting on a real rendered star and MUST be 0 -- see F31.
