# Hot Pixels & Saturation

Two unrelated artifacts pollute star measurements: single bright **hot pixels** that masquerade as tiny stars, and **saturated** (clipped) cores that flatten the bright peak of an otherwise good star. Hocus Focus handles them up front in the detection pipeline. Hot pixels are cleaned out before star structures are found, and a star whose core is clipped is measured for HFR but gets no PSF model.

![The Hotpixel Threshold and Saturation Threshold settings highlighted in the advanced list](../assets/screenshots/advanced-hotpixel-saturation.png){ width=400 }

*Hotpixel Threshold and Saturation Threshold control hot-pixel rejection and the saturation cutoff.*

These options live in the **Advanced** star-detection settings. In Simple mode only Hotpixel Filtering follows a preset: it switches on unless **Noise Level** is set to None. Hotpixel Threshold is held at its 0.1% default, and the other three keep their stored values.

## Settings at a glance

| Setting | Default | Range | Effect |
|---|---|---|---|
| Hotpixel Filtering | On | On / Off | Clean hot pixels out of the frame before detection |
| Measurement Hotpixel Repair | Off for existing settings | On / Off | Repair only isolated hot pixels on the measurement image instead of running a median over it |
| Use Hotpixel Thresholding | On | On / Off | On the structure image, replace only pixels that differ sharply from the median instead of blurring everything |
| Hotpixel Threshold | 0.1% (0.001) | (0, 100%] | On the structure image, how far a pixel must sit from its 3×3 median (as a fraction of full well) to count as a hot pixel |
| Saturation Threshold | 99% (0.99) | (0, 100%] | Pixels at or above this fraction of full well count as saturated; a star containing any gets no PSF model |
| Exclude Saturated Stars From HFR | On | On / Off | Leave partially-saturated stars out of the per-frame HFR average; they stay detected and counted |

---

## Hotpixel Filtering

**What it does:** cleans isolated hot pixels out of the frame before star structures are detected.

> Uses a 3x3 box median convolution to filter out hotpixels. This should be on, unless you're working with a calibrated image with hotpixels removed

- **Default:** On
- **Range:** On / Off

A hot pixel is a single sensor cell that reads anomalously high regardless of incoming light. Left in the image it forms a tiny, sharp, one-pixel "star" that survives structure detection and contaminates HFR and star-count statistics. The median filter radius is fixed at 1 (a 3×3 window); only that size is supported.

When noise reduction is in play, hot-pixel filtering also runs first so the hot pixels are not smeared into their neighbors by the noise-reduction blur. What that filtering *is* depends on **Measurement Hotpixel Repair**, below.

---

## Measurement Hotpixel Repair

**What it does:** repairs only isolated hot pixels on the image HFR and the PSF are measured from, instead of running a 3×3 median over all of it.

- **Default:** On for a new install. Off for any profile that predates this option, and for any settings file that does not mention it. Restore Defaults and applying an optimization turn it on.
- **Range:** On / Off

Detection works on two derived images. The **structure image** is what star candidates are found in; it always takes the 3×3 median described by the settings above, because candidate formation needs that smoothing. The **measurement image** is what each star's HFR and PSF model are measured from, and this setting decides how it is treated.

With this **off**, the measurement image takes the same median. That median is an expensive filter for numbers measured off it: on a real frame it drops a bright star's peak by about a fifth, widens its half-maximum width, and biases every fitted FWHM about 6% high, which in turn flattens roughly a third of the real focus gradient across the sensor.

With this **on**, a pixel on the measurement image is rewritten to its 3×3 median only when it stands more than five local noise sigmas above the local background *and* its brightest neighbor sits below a third of that amplitude. A hot pixel passes both tests, because its neighbors stay at background. A star core fails the second one, because its neighbors carry most of its light. The count of pixels rewritten appears as **Repaired Hotpixels** in the [Star Detection Results panel](index.md#reading-the-results-the-star-detection-results-panel).

!!! warning "Why it is off for existing settings"
    Turning this on also makes the measurement image's noise estimate the frame's honest noise rather than a median-suppressed one, and makes every star measure a smaller HFR. **Brightness Sensitivity** is expressed in multiples of that noise and **Min HFR** is an absolute floor, so both become effectively stricter at the same numbers, and a rig tuned without this may detect fewer faint stars. That is why it never switches itself on for an existing profile: it arrives with **Restore Defaults**, or with applying an optimization, both of which re-derive those gates at the same time. Loading a settings file that does not mention it leaves it off. A brand-new profile has nothing to preserve, so it starts with the current defaults.

!!! tip "When this helps"
    Turn it **on** if you care about FWHM, eccentricity or the aberration inspector's focus gradient, and re-run the optimization wizard afterwards so the gates are calibrated against it. Measured across the auto-focus bank it tightens the frame-to-frame FWHM spread on 16 of 19 runs. Structure detection is unaffected either way, and autofocus accuracy is unchanged.

![Raw frame with a hot pixel versus the same frame after a 3×3 median filter](../assets/figures/hot-pixel.png){ width=620 }
*A single hot pixel (left) reads far above its neighbors; the 3×3 median (right) replaces it with the local median while leaving the real star untouched.*

!!! tip "When this helps"
    Leave this **on** for raw or uncalibrated frames. Almost every sensor has some hot pixels, and any that survive read as spurious one-pixel "stars." Turn it **off** only when you are feeding already-calibrated images whose hot pixels have been removed (e.g., by dark subtraction or a defect map), so you avoid a redundant filtering pass.

---

## Use Hotpixel Thresholding

**What it does:** on the structure image, restricts the median replacement to pixels that differ sharply from their local median, instead of replacing every pixel and blurring the whole image.

> A more sophisticated version of hotpixel filtering that limits pixel replacement to those where the median is far off of the pixel value. This prevents the whole image from being blurred, which can have a negative effect on HFR and PSF measurement accuracy

- **Default:** On
- **Range:** On / Off

A plain 3×3 median replaces *every* pixel with its neighborhood median, which is effectively a light blur across the whole structure image. Thresholded filtering compares each pixel against its 3×3 median and only swaps it when the difference exceeds **Hotpixel Threshold**, so pixels that match their surroundings are left exactly as-is. It costs slightly more compute than the unconditional median: the same 3x3 median, plus a per-pixel comparison against it.

This setting governs the **structure image only**. The measurement image, where HFR and the PSF are measured, always uses the isolation test described above, so neither of these settings can soften a star core there.

!!! tip "When this helps"
    Leave this **on** in almost all cases. Turning it **off** reverts the structure image to an unconditional median, which softens every pixel instead of only the outliers, and the only thing you gain is a slightly cheaper filtering pass.

!!! note
    When thresholding is **on**, the filter replaces only outlier pixels and does not blur, so Simple mode adds 1 to the noise-reduction radius to compensate. That extra radius applies only when hot-pixel filtering and thresholding are both on, which is the default. With thresholding **off**, the plain median already blurs, so no extra radius is added.

---

## Hotpixel Threshold

**What it does:** sets how far a pixel must sit from its 3×3 median (as a fraction of full well) before it is replaced as a hot pixel.

> A percentage representing the cutoff threshold for detecting a hotpixel. It's the percentage of full well difference between the 3x3 median blurred value and the pixel value

- **Default:** 0.1% (0.001)
- **Range:** greater than 0% up to and including 100% (the editor accepts 0–100%; values must be within \((0, 1]\) as a fraction)

This setting only takes effect when **Use Hotpixel Thresholding** is on. A pixel is replaced when

\[
\left| \text{pixel} - \text{median}_{3\times 3} \right| \;\ge\; \text{HotpixelThreshold} \times (\text{full well})
\]

so at the 0.1% default a pixel must exceed its local median by one part in a thousand of the full ADU range to be treated as a defect. Lower values are more aggressive (more pixels replaced, risking real star cores); higher values are more permissive (only the most extreme outliers are touched).

!!! tip "When this helps"
    Leave the default unless you have a specific reason. **Lower** the threshold if obvious hot pixels are surviving and being detected as stars; **raise** it if the filter is clipping the bright cores of real, well-sampled stars. Because the threshold is a fixed fraction of full well rather than a multiple of the frame's noise, the same value sits many times the noise at low gain and below the noise at high gain, so re-check it if you change gain or exposure substantially.

!!! note
    Setting this very low makes the structure image nearly an unconditional median, since almost every pixel then differs from its own 3x3 median by more than the threshold. That softens the image candidates are found in. It cannot reach HFR or the PSF, which are measured from the separate measurement image.

---

## Saturation Threshold

**What it does:** marks pixels at or above this fraction of full well as saturated; a star containing any is still detected and measured for HFR, but gets no PSF model.

> A percentage representing the cutoff threshold for detecting a saturated pixel. Star candidates containing saturated pixels are still detected and measured, but no PSF model is fit to them

- **Default:** 99% (0.99)
- **Range:** greater than 0% up to and including 100% (the editor accepts 0–100%; values must be within \((0, 1]\) as a fraction)

When a star's core clips at the sensor's full-well limit, its peak flattens into a plateau and the true profile is lost in those pixels. Hocus Focus does **not** reject such a star: it keeps its position, its structure and its own HFR. What it does not do is fit a PSF to it.

The reason is that a clipped core leaves the fit with wings only, and the wings alone cannot determine a width. Masking the clipped pixels out and fitting the rest sounds reasonable, and it is what earlier versions did, but the remaining samples are consistent with a wide range of widths: the solver settles wherever its amplitude limit puts it. On one measured frame the five saturated stars came out anywhere between 15% narrower and 54% wider than their unsaturated neighbors, with the sign depending on how much of the core survived, and no goodness-of-fit gate separated them from a genuinely good fit on a bright star. Leaving them unfitted keeps those numbers out of the frame's FWHM, sigma and eccentricity, which are medians over the stars that do have a model.

Saturated stars are counted as **Saturated** in the detection metrics, and the total number of saturated pixels as **Saturated Pixels**.

![A saturated star with a flat-topped core and its profile clipping at the saturation threshold](../assets/figures/saturated-star.png){ width=620 }
*The saturated core (left) reads a flat plateau; its horizontal cut (right) clips at the threshold. With the peak gone, the surviving wings no longer pin down the star's width, so no PSF model is fit.*

!!! tip "When this helps"
    Leave the default (99%) for most setups. **Lower** it if your sensor or processing introduces non-linearity or blooming just below the full-well point, so stars tainted by those near-saturation pixels also stop contributing a PSF. **Raise** it toward 100% only if you are confident your sensor stays linear right up to the clip point. Setting it too low costs you PSF measurements on perfectly good bright stars.

---

## Exclude Saturated Stars From HFR

**What it does:** keeps partially-saturated stars out of the per-frame HFR average, while still detecting and counting them.

> When enabled (default), partially-saturated stars (those whose peak reaches the Saturation Threshold) are left out of the per-frame HFR average — their flat, saturated cores bias HFR high and can pull the focus curve. The stars are still detected and counted; only the HFR average excludes them, and only while enough unsaturated stars remain. Disable to include every star's HFR as before.

- **Default:** On
- **Range:** On / Off

A saturated core clips flat at the full-well limit, so the half-flux radius measured from it reads larger than the star's true size. That inflated HFR drags the frame's aggregate HFR upward and can distort the focus curve the optimizer fits to find best focus. This setting drops such stars from the per-frame **HFR average** (and its standard deviation), so the curve point reflects the well-behaved stars instead.

It does not reject the star. A partially-saturated star is still a real detection with a valid position and structure, so it stays in the accepted set: the total star count, the star centers, and each star's own measured HFR are unchanged. Only the per-frame average leaves it out. Rejecting it outright would discard a genuine star, and on a frame with few stars that loss matters most. For that reason the exclusion applies only while **at least three unsaturated stars remain** on the frame; below that floor every star is kept, so a bright, saturation-heavy frame still produces an HFR. A star counts as saturated by the same test the rest of the pipeline uses: its background plus peak brightness reaches the **Saturation Threshold** above. With nothing saturated, or with this option off, the HFR average is exactly what it was before.

!!! tip "When this helps"
    Leave it **on**. It matters most on frames that hold a bright, clipped star next to fainter ones, where that single star would otherwise pull the HFR curve. Turn it **off** only to restore the legacy behavior, where every accepted star contributes to the HFR average regardless of saturation.
