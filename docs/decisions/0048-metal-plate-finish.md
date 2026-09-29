# 0048 — Metal plate finish

- Status: Accepted
- Created: 2026-09-29
- Reflects: branch `metal-plate-finish` (PR #70, issue #68); played; `wall.json`'s `plate-{metal}` family and `Metal` entries in `LayerSounds` and `LayerResistance`; `MaterialTextureOpacityTests.Candidates` and `AllCandidates`; decisions 0010, 0030, 0033

## Summary
A player asked for copper or iron plate as a wall finish.
One `plate-{metal}` entry in `FinishFamilies` expands over vanilla's `metalplate-{metal}` items into a finish per metal (0010), with no new C#.

## Context
`metalplate` takes its `metal` variants from `worldproperties/block/metal.json`, so all 23 metals are real items, including ones the handbook hides.
Each face consumes one plate and drops it back when peeled.

## Design
**Texture.**
The family uses `game:block/metal/sheet/{metal}1` (`wall.json:223`), the texture vanilla's `metalblock.json` uses.
`block/metal/plate/` has no `cupronickel.png`, so a plate texture would ship one metal with a missing texture.

**Layer material.**
`BlockMaterial` is `Metal`, new to `LayerSounds` and `LayerResistance` (`wall.json:235`, `:243`, 0033).
Hit and break sound like vanilla's metal blocks (`block/chute`), walking sounds like stone, and resistance is 3.0, just above stone's 2.5.
Metal is not a wood top layer, so fire does not catch on it (0043).

**Names.**
Each metal has its own `finish-plate-{metal}` key in `en.json`, as every other family does, and `EveryMaterialDisplayNameResolvesInLangFile` checks them all.

**Tests.**
The texture and lang tests read vanilla's variants through `MaterialTextureOpacityTests.Candidates`, which lists its source files by hand in `AllCandidates`.
`metalplate.json` was not in that list, and `Candidates` read the property variant's `Code` while `metal.json` writes `code`, so the first green run checked no metal at all.
Both are fixed; removing one metal's lang key now fails the test by name.

## Alternatives considered
- **`block/metal/plate/{metal}`.** Missing for cupronickel; see Texture.
- **A riveted style.** `block/metal/riveted/` covers only some metals, so it would need a per-metal fallback. Not asked for.
- **Composed names from vanilla's `material-{metal}` keys.** Would drop the 23 lang lines and name other mods' metals too, but it is a tooltip change for every family, not this one.

## Consequences & open questions
- A modded metal that adds a `metalplate` variant gets a finish automatically, but its tooltip falls back to the title-cased material key (0030), and it needs a `sheet/{metal}1` texture.
- `AllCandidates` is still a hand-kept list: a new family's source file has to be added there, or its tests pass without checking it.
