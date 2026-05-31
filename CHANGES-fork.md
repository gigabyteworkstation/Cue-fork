# Cue fork — realism & persistence changes

This fork builds on rutheniumm/Cue-fork and targets the issues below. All code
is kept within VaM's constraints (Unity 2018.1.9f2 / C# 6.0, no new external
dependencies) and loads through `Cue.cslist` like the rest of the plugin.

## 1. State persistence (seeds + personality)

Previously the personality and arousal state were effectively never saved:

* `Personality.Load()` was a no-op and `Personality.ToJSON()` only wrote the
  personality name, so any value tuned through the AI tab was lost on a scene
  save or a plugin reset.
* Every person was constructed with a **hard-coded arousal seed of `12345`**, so
  all people shared identical traits, and the seed was never written to or read
  back from the save.

Changes:

* `Personality.ToJSON()` / `Personality.Load()` now round-trip every float/bool
  that differs from the personality's on-disk defaults (diffed against a fresh
  clone), keyed by name so it stays forward-compatible.
* Each person now gets a **unique, non-negative, persisted arousal seed**
  (`Person.cs`). The seed and the brain's slow-moving runtime memory
  (habituation, frustration, edge pressure, urge, learned depth/pace,
  familiarity, …) are serialised via `ArousalSystem.ToJSON()` /
  `ArousalBrain.ToJSON()` and restored on load, so a saved scene resumes
  mid-arousal and traits stay identical across save/load and plugin resets.

## 2. Arousal that actually moves (and is personality-driven)

The advanced `ArousalBrain` previously only modulated the excitement **rate** by
±50% and was still clamped under the legacy zone-based ceiling, so it barely
affected the real arousal value and couldn't build at all in toy/dildo scenes.

* The brain now contributes a **direct drive term** to the excitement rate and
  can **lift the excitement ceiling** (`Excitement.Max`) toward what the current
  stimulation can sustain — it only ever raises the ceiling above the legacy
  zone maximum, so existing scenes are unaffected while toy stimulation can now
  build and climax.
* The final approach is gated through the brain's **climax barrier**, so arousal
  plateaus near the edge and then releases (edging) instead of snapping to 100%.
* Each person derives a distinct high-level temperament (sensitivity,
  responsiveness, stamina, inhibition) from their persisted seed, so two people
  no longer respond identically.

## 3. Reaction system

The reaction trigger compared a single-frame delta of an already heavily
smoothed signal against a large threshold (so real spikes almost never crossed
it) and was gated behind "only if a one-liner is pending or in the first few
seconds." It now uses an **EMA-baseline surge detector**: a reaction fires when
live stimulation surges above its own recent average, with the odds shaped by
inhibition/responsiveness and a short cooldown — so reactions fire reliably when
they should.

## 4. Moaning

The breathing/moaning balance now tracks arousal continuously
(`UpdateDynamicMoanRatio`), starting from the user-configured baseline ratio and
shifting toward mostly-moaning as she builds, instead of staying fixed.

## Files touched

* `project/src/Person/Person.cs`
* `project/src/Person/Personality.cs`
* `project/src/Person/Excitement/Excitement.cs`
* `project/src/AI/ArousalNet.cs`
* `project/src/AI/UrgeActuator.cs`
* `project/src/Integration/VAMMoan2.cs`
