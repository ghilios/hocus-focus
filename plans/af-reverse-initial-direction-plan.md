# Plan: "Reverse Initial Direction" AutoFocus option

## Context

The blind AutoFocus walk always starts on the **high** side of the current position. It overshoots to
`initial + (offset+1)*step` and then measures `offset` seed points with **decreasing** moves
(`StartBlindFocusPoints`, `AutoFocus/AutoFocusEngine.cs:1385`). The trend walk keeps stepping left (decreasing)
until the left trend is established, and then fills in the right side. The live fixed sweep (`RunFixedSweep`,
~`:2346`) copies this approach: overshoot high, then step down.

On SCTs that focus by moving the primary mirror, NINA's backlash compensation (overshoot model) runs on every
move in one direction. When that is the decreasing direction, every seed and walk step pays the extra cost, so
AF takes much longer. The new option mirrors the whole approach: the run seeds from the **low** side with
**increasing** moves, and the walk prefers stepping right. The expensive direction is then used only for the
final bracket fill-in.

## Design

A user-facing **bool** option, `ReverseInitialDirection` (default `false`, so behavior is byte-identical
unless enabled). It is global (not per-filter), because it describes the focuser mechanics.

Mirroring rule: `seedSign = reverse ? +1 : -1` (the direction of every seed move).

### Engine changes (`AutoFocus/AutoFocusEngine.cs`)

1. **`StartBlindFocusPoints`**
   - Seed start: `initial - seedSign*(offsetSteps+1)*stepSize`. Each seed move goes to `target + seedSign*stepSize`.
     The limit check becomes direction-aware (reverse: throw if `actual <= previous`).
   - `lastStepDirection` initial value: `reverse ? WalkDirection.Left : WalkDirection.Right` (the side the seeds sit
     on). Update the comment.
   - Default step preference: normal `stepLeft = leftTrendCount < offsetSteps`; reverse
     `stepLeft = !(rightTrendCount < offsetSteps)`. Behavior B's forced-direction override is unchanged.
   - Bracket-completion branches (left-trend-complete → extend right; right-trend-complete → extend left): when
     both are true, evaluate the seed-direction branch first in reverse mode (implemented as two booleans,
     `leftTrendComplete` / `rightTrendComplete`; the default-mode branch order is unchanged).
   - Log the chosen direction once at the start of the walk.
2. **`ComputeSweepPositions(initial, offsetSteps, stepSize, reverse = false)`**: return ascending
   positions when `reverse` is true. **`RunFixedSweep`** overshoots to `initial - (offset+1)*step` in reverse
   mode, so the live sweep also approaches every point from the configured side.
3. Update the `WalkDirection` enum comment (~`:553`), which says the seeds always sit on the high side.

Retry/re-center (`sweepCenter`), `ApplyFinalSymmetricWindow`, the final-validation moves, and Rerun/replay need
no changes. Rerun works on saved frames and never walks the focuser.

### Options plumbing (follow `MaxBlindStepsPerDirection` exactly)

- `Interfaces/IAutoFocusOptions.cs`: `bool ReverseInitialDirection { get; set; }`
- `AutoFocus/AutoFocusOptions.cs`: backing field, `GetValueBoolean(nameof(...), false)` in `InitializeOptions`,
  property with `SetValueBoolean` + `RaisePropertyChanged`, and a reset in `ResetDefaults`.
- `Interfaces/IAutoFocusEngine.cs` `AutoFocusEngineOptions`: `public bool ReverseInitialDirection { get; set; }`
- `AutoFocusEngine.GetOptions` (~`:3023`): `ReverseInitialDirection = autoFocusOptions.ReverseInitialDirection`
- **Not** added to `AutoFocusOptionsSnapshot` / replay mapper. That snapshot holds only fields that affect
  detection or fitting of an already-captured run, and direction doesn't. The run's `HocusFocusReport` already
  serializes the whole `HocusFocusAutoFocusOptions` object, so the setting is recorded per run automatically.
- Test fake `Tests/Inspection/FakeSensorModelOptions.cs` (implements `IAutoFocusOptions`): add the property.

### UI (project invariant)

- `Resources/OptionsDataTemplates.xaml`: add a tooltip resource `ReverseInitialDirection_Tooltip` next to
  `MaxBlindStepsPerDirection_Tooltip` (~`:539`). Suggested text: "Start AutoFocus below the current focuser
  position and take the initial measurements with increasing focuser moves, instead of starting above it with
  decreasing moves. Enable this when your focuser's backlash compensation fires on decreasing moves (common on
  SCTs that move the primary mirror) so most AutoFocus moves avoid it."
- Add a new grid row after "Max Blind Steps Per Direction" (row 16 → new row 17, append a `RowDefinition`, and
  shift later rows only if any follow in that grid). It holds a label "Reverse Initial Direction" plus a
  `CheckBox` bound to `AutoFocusOptions.ReverseInitialDirection`, both with the tooltip.

### Docs

- `documentation/docs/overview/autofocus.md`: add a short entry next to the Max Blind Steps description that
  explains the option and the SCT/backlash use case. Follow `.claude/docs/documentation-style.md`.

## Tests (`Joko.NINA.Plugins.HocusFocus.Tests`)

- `AutoFocus/AutoFocusEngineTests.cs`: `ComputeSweepPositions` with `reverse: true` returns ascending
  `initial-N*step … initial+N*step`. The existing descending tests stay as they are.
- `AutoFocus/AutoFocusEngineSweepBatteryTests.cs` (uses `SweepHarness` + `MovingFocuserMediator.MoveHistory`):
  - **Reverse seeds approach from below:** with `ReverseInitialDirection = true`, the N seed moves are
    `start-N*step … start-step` (strictly increasing), and the run converges to the same focus (within
    tolerance) as the default direction on a clean scenario.
  - **Mirror symmetry:** a scenario whose true focus is below the start converges in default mode, and the
    mirrored scenario (focus above the start) converges in reverse mode with a mirrored move count.
  - **Default unchanged:** an existing default-mode assertion on the move history still passes; the option
    defaults to false in `DefaultSweepOptions`.
  - **Behavior B under reverse:** with the cap on and focus placed on the far (low) side, the run reverses
    once and still converges.

## Verification

1. `rtk dotnet build Joko.NINA.Plugins/Joko.NINA.Plugins.sln -c Debug --nologo` (errors=0).
2. `rtk dotnet test Joko.NINA.Plugins/Joko.NINA.Plugins.sln -c Debug --nologo`: all green, including the new
   tests.
3. Manual (user, in NINA): enable the option, run AF against the simulator focuser or the SCT, and confirm in
   the log that the seed moves increase from `start-(N+1)*step`, the run converges, and the report JSON
   contains `ReverseInitialDirection: true`.

## Git

First step of execution: copy this plan to `plans/af-reverse-initial-direction-plan.md` (the project rule for
where plans live). Feature branch `ghilios/af-reverse-initial-direction`. Commit with the privacy email per CLAUDE.md, push, and
open a PR to `develop`.
