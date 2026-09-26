# AF-bank verification -- 22 run(s)

generated: 20260916T184101Z  |  detector commit: before  |  NoiseClip default = 4  |  pixel-scale mode: header  |  NC sweep: 4

profile: `Default-2026-09-16T12:39:25 (5902da3a-df3b-4d86-9e06-c9557ebbf679)`  |  settings: `\\wsl.localhost\Ubuntu-20.04\tmp\claude-1000\-home-ghilios-src-hocus-focus\62319d24-d189-4732-93f8-fe9d2a22498d\scratchpad\val\bank_settings.json` (pinned)

> Comparable to another report only when the profile and settings lines above match it.

## NoiseClippingMultiplier sweep (C0 as-default -- the honest recall reference)

| NC | median recall@SNR>=12 | median recall@all | median precision | median AF sigma_focus | median sensor R^2 | runs |
|---|---|---|---|---|---|---|
| 4 | 0.921 | 0.652 | 1 | 0.695 | 0.02 | 22 |

**Recommended default NoiseClippingMultiplier: 4** -- NC=4 (recall@high 0.92, precision 1.00) is the lowest swept value and still clears the precision floor 0.60 -- recall is still rising at the bottom of the range, so a lower / per-frame-adaptive NC may help

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
| D01_ultrawide_40mm/attempt01 | 6248x4176 | 110384 (62411) | False | C0@nc4 | 4 | 0.133 | 0.195 | 1 | 0.19 | 0.966 | 0 | 0.714 | 0.908 | 0.004 | 0.288 | 0.019 | 0 | 1606 | 6/9 |
| D02_rich_135mm/attempt01 | 6248x4176 | 31144 (26328) | False | C0@nc4 | 4 | 0.455 | 0.53 | 1 | 0.067 | 0.989 | 0 | 0.232 | 0.907 | 0 | 0.147 | 0.005 | 0 | 2130 | 6/9 |
| D03_redcat_250mm/attempt01 | 3008x3008 | 7102 (4627) | False | C0@nc4 | 4 | 0.4 | 0.445 | 1 | 0.034 | 0.991 | 0 | 1.101 | 0.908 | 0.023 | 0.493 | 0.046 | 0.001 | 354 | 6/9 |
| D04_esprit_550mm/attempt01 | 6248x4176 | 45823 (22109) | False | C0@nc4 | 4 | 0.814 | 0.682 | 1 | 0.115 | 0.981 | 0 | 0.281 | 0.999 | 0 | 0.572 | 0.061 | 0 | 2878 | 9/9 |
| D05_tec140_1000mm/attempt01 | 9576x6388 | 11523 (4413) | False | C0@nc4 | 4 | 0.986 | 0.797 | 1 | 0.014 | 0.995 | 0 | 0.201 | 1 | 0.007 | 0.811 | 0.116 | 0.001 | 809 | 9/9 |
| D06_sparse_1000mm/attempt01 | 3008x3008 | 356 (167) | False | C0@nc4 | 4 | 0.964 | 0.817 | 1 | 0 | 0.986 | 0 | 0.259 | 1 | 0.103 | 0.869 | 0.207 | 0.006 | 27 | 9/9 |
| D07_rc10_2000mm/attempt01 | 3008x3008 | 1471 (593) | True | C0@nc4 | 4 | 0.985 | 0.773 | 1 | 0.032 | 0.949 | 0 | 0.112 | 1 | 0.02 | 0.81 | 0.078 | 0.002 | 103 | 9/9 |
| D08_c11_2800mm/attempt01 | 6248x4176 | 315 (101) | True | C0@nc4 | 4 | 1 | 0.797 | 1 | 0 | 0.996 | 0 | 1.092 | 0.999 | 0.226 | 1.926 | 0.589 | 0.005 | 21 | 5/9 |
| D09_c14_3800mm/attempt01 | 4144x2822 | 234 (90) | True | C0@nc4 | 4 | 0.911 | 0.568 | 1 | 0 | 1 | 0 | 1.72 | 0.999 | 0.557 | 0.483 | 0.025 | 0.014 | 11 | 5/9 |
| D10_rc16_3250mm_sparse/attempt01 | 3008x3008 | 115 (58) | True | C0@nc4 | 4 | 1 | 0.713 | 1 | 0 | 1 | 0 | 1.407 | 0.999 | NaN | NaN | NaN | NaN | 0 | 6/9 |
| D11_rc10_585_afbin2/attempt01 | 1920x1080 | 333 (133) | True | C0@nc4 | 4 | 0.887 | 0.592 | 1 | 0 | 0.985 | 0 | 0.227 | 1 | 0.06 | 0.404 | 0.051 | 0.003 | 19 | 8/9 |
| D12_c14_585_afbin2/attempt01 | 1920x1080 | 329 (107) | True | C0@nc4 | 4 | 0.888 | 0.486 | 1 | 0 | 0.952 | 0 | 1.42 | 0.999 | 0.212 | 0.788 | 0.098 | 0.011 | 11 | 5/9 |
| D13_apo200_1800mm/attempt01 | 6248x4176 | 790 (277) | False | C0@nc4 | 4 | 1 | 0.895 | 1 | 0.003 | 0.985 | 0 | 3.221 | 0.998 | 0.008 | 2.433 | 0.367 | 0.001 | 59 | 9/9 |
| D14_cdk14_2563mm_e47/attempt01 | 9576x6388 | 1197 (384) | True | C0@nc4 | 4 | 0.979 | 0.756 | 1 | 0.001 | 0.992 | 0 | 0.94 | 0.999 | 0.044 | 0.815 | 0.072 | 0.001 | 67 | 9/9 |
| D15_cdk20_3454mm_e47/attempt01 | 6248x4176 | 254 (87) | True | C0@nc4 | 4 | 0.931 | 0.622 | 1 | 0 | 0.981 | 0 | 1.656 | 0.997 | 0.516 | 0.461 | 0.047 | 0.003 | 10 | 5/9 |
| D16_esprit550_ha3/attempt01 | 6248x4176 | 818 (184) | False | C0@nc4 | 4 | 0.87 | 0.427 | 1 | 0 | 0.986 | 0 | 0.653 | 0.995 | 0.026 | 0.739 | 0.109 | 0.001 | 34 | 9/9 |
| D17_cdk14_oiii5/attempt01 | 3008x3008 | 208 (61) | True | C0@nc4 | 4 | 1 | 0.798 | 1 | 0 | 0.988 | 0 | 0.677 | 0.999 | 0.129 | 1.089 | 0.094 | 0.003 | 14 | 5/9 |
| D18_m24_deep_shed/attempt01 | 6248x4176 | 82825 (21847) | False | C0@nc4 | 4 | 0.795 | 0.37 | 1 | 0.248 | 0.961 | 0 | 0.273 | 0.999 | 0 | 0.569 | 0.059 | 0 | 2736 | 9/9 |
| D19_cygnus_deep_shed/attempt01 | 9576x6388 | 12683 (4873) | False | C0@nc4 | 4 | 0.985 | 0.732 | 1 | 0.014 | 0.988 | 0 | 0.238 | 1 | 0.003 | 0.7 | 0.085 | 0 | 836 | 9/9 |
| D20_m24_bright_control/attempt01 | 6248x4176 | 10029 (8265) | False | C0@nc4 | 4 | 0.953 | 0.961 | 1 | 0.019 | 0.993 | 0 | 0.066 | 1 | 0.005 | 0.271 | 0.018 | 0 | 1013 | 9/9 |
| D21_widefield_60mm/attempt01 | 6248x4176 | 57133 (33805) | False | C0@nc4 | 4 | 0.114 | 0.181 | 1 | 0.089 | 0.968 | 0 | 1.092 | 0.804 | 0.004 | 0.335 | 0.026 | 0 | 524 | 6/9 |
| D22_widefield_100mm/attempt01 | 3008x3008 | 13672 (4204) | False | C0@nc4 | 4 | 0.161 | 0.105 | 1 | 0.037 | 0.973 | 0 | 0.972 | 0.534 | 0.019 | 0.336 | 0.027 | 0.001 | 131 | 6/9 |

**null** is the precision the same detections earn after being translated with wraparound -- chance alone. It is the floor this metric can read; a precision of 1.000 means the detector found no false positives only when null is near 0. **scored** is the fraction of detections that entered the precision ratio at all (the rest sit on reference objects that cannot be judged). **viol** counts scored false positives sitting on a real rendered star and MUST be 0 -- see F31.
