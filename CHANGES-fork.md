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

## Round 2 — lifelike dynamics

Addresses: decay too slow (constant moaning at the top), arousal sometimes
rising too fast, edging state unreachable, "easy to arouse / hard to finish"
personalities, boredom of a repeated pace, conditional perpetual/multi-orgasm,
and resetting learned state on seed regen.

* **Climax gate.** Arousal now plateaus *below* 1.0 and only tips into orgasm
  once a separate `climaxReadiness` accumulator fills. Readiness builds from
  high arousal **with variety** and **without boredom**, scaled by the new
  `Orgasmicity` trait — a low-Orgasmicity person can hover at the edge almost
  indefinitely unless you change pace/depth. This makes the **Edging** state
  reachable (it's now "near the top but the gate is shut") and gives real
  control over how hard someone is to finish.
* **Boredom / novelty.** A new `boredom` signal rises when the pace/depth stays
  unvarying at high engagement (scaled by `NoveltyCraving`); it lowers the
  sustainable ceiling, speeds decay, and blocks the climax gate. A genuine
  pace/depth change fires a `noveltyPulse` that re-excites and relieves boredom.
  So an unchanging rhythm makes her cool off and demand variety.
* **Breathing plateau + real decay.** The plateau "breathes" with procedural
  noise so high arousal is never a flat line, and Mood now uses a dynamic
  brain-supplied `FalloffRate` (much faster than the old fixed `-0.01/s`, faster
  still when bored) instead of freezing near the ceiling.
* **Conditional perpetual & multi-orgasm.** `DoOrgasm` fires a sustained
  perpetual vocal loop for multi-orgasmic types under strong continued
  stimulation, and bounded multi-orgasm chains roll straight into another wave
  while stimulation stays high.
* **Reward / drift from past experience.** Matching her learned depth/pace still
  rewards; after a couple of orgasms on the same pattern her learned preferences
  drift and boredom seeds higher, so the identical motion gets less effective.
* **Seed regen resets learning.** `SetSeed` now calls `ResetLearnedState()`,
  wiping all accumulators/learned values (a fresh seed is a fresh person).
* **New traits** (append-only so existing seeds keep their other traits):
  `Orgasmicity`, `NoveltyCraving`, `PaceChangeReward`, `PreferredDepth`,
  `PreferredPace`, `ClimaxBuildRate`, `ArousalFalloff`, `BoredomRate`. All new
  signals are exposed in the AI-tab debug readout (boredom, variety,
  climaxReady, gateCap, falloff, orgasmCount).

Note: the climax gate governs the brain-driven (toy/dildo) interactions the
ArousalSystem reads; legacy zone-driven sex still reaches climax through the
original path, so existing scenes are not regressed.

## Round 3 — reactive sound system + plugin integrations

A configurable, positional, intensity-driven sound event system, plus official
integrations for Foost.SexyFluids, Foost.DildoLanguage (extended) and
Skynet.OrificeDynamics when loaded on the Cue person atom.

### Sound sets (`project/src/Sound/SoundSet.cs`)
* Named clip banks with **3 or 5 intensity bands**; clips load from a **folder**
  of .wav/.ogg/.mp3 or an **.assetbundle** of AudioClips (async, no hitching).
* Band assignment by filename hint (`soft/low`, `med/mid`, `hard/high/fast`,
  `_1.._5`) with even distribution as fallback; random in-band pick with
  anti-repeat.

### Positional playback (`project/src/Sound/SoundPlayer.cs`)
* Pool of 16 fully-3D AudioSources placed **at the event position** (impact
  point, orifice). Each concurrent play uses its own source, so sounds layer
  freely; volume and pitch scale with event intensity. Zero steady-state
  allocation.

### Event detection (`project/src/Sound/SoundEvents.cs`)
* **Impacts**: tracks configured body parts (glutes, thighs, breasts, head, …)
  against other persons' hands/feet and the penetrator CUA; a contact-range
  crossing with real closing speed fires a hit whose intensity follows the
  closing speed (slap volume tracks how hard you slap).
* **Penetration**: entry (speed-banded), exit, depth-threshold deep-thrust, and
  **tongue/throat contact** (Mouth orifice + deep crossing) — all orifice-aware
  via DildoLanguage's `penetration:orifice`.
* **Fingering**: hand-to-genital proximity state machine with hysteresis;
  entry in slow/medium/fast bands, single-band exit. Works for self and others.
* **Rules UI** (`Sounds` person tab): per-rule trigger dropdown, body-part and
  orifice filters, sound-set dropdown, volume/pitch/jitter, intensity→volume
  amount, min interval, depth threshold, enable toggle. Sets and rules persist
  with the scene.
* Heavy resolution runs every 2 s; the per-frame path is plain vector maths on
  pre-resolved parts.

### Integrations (`project/src/Integration/FoostPlugins.cs`)
* **SexyFluids** (same atom): orgasm fires `squirt:start`, perpetual orgasms
  use `squirt:startEndless`/`stop`, aftershocks pulse `squirt:burst` — all
  gated by the per-seed `SquirtPropensity` trait, so squirting is a personality
  feature rather than universal.
* **OrificeDynamics**: presence detection + plane params as a stretch hint.
* **DildoLanguage**: reader now also reports the penetrated orifice.

## Files touched

* `project/src/Person/Person.cs`
* `project/src/Person/Personality.cs`
* `project/src/Person/Excitement/Excitement.cs`
* `project/src/AI/ArousalNet.cs`
* `project/src/AI/UrgeActuator.cs`
* `project/src/Integration/VAMMoan2.cs`
