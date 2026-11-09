# 0092 — Oil Lamps Anywhere's ceiling lamp

- Status: Accepted; a decked cell now takes a block that fits under the deck, and a deck lifts hangers, see 0094
- Created: 2026-11-08
- Reflects: issue #134 and mod page comment https://mods.vintagestory.at/vssiding#cmt-250046; branch `134-oil-lamps-anywhere`; `SidingModSystem.IsHanger` and the particle-patch loop in `SidingModSystem.Start`, `IsHangerTests`; Oil Lamps Anywhere 1.1.0's decompiled DLL (`CeilingAssets`, `BlockCeilingLamp`, `CeilingPlacementPatch`, `CeilingSupport`); vanilla 1.22 `BlockGroundAndSideAttachable.TryPlaceBlock`; decision 0054; unit tests pass; played on 2026-11-08

## Summary
A player asked whether oil lamps hang from a thin floor the way Oil Lamps Anywhere lets them hang from a ceiling.
A vanilla oil lamp never hangs, so the hanger shift (0054) never met one.
With Oil Lamps Anywhere installed, its ceiling lamp now counts as a hanger, matched by its code, and its flame moves with it.

## Context
`BlockOilLamp` places through `BlockGroundAndSideAttachable.TryPlaceBlock`, which attaches to a clicked top or side and skips `DOWN` in its fallback, so its `-down` variant is never placed.
Oil Lamps Anywhere prefixes that method: a click on a block's underside places `game:oillampceilingfix-{color}-down-{heading}-{normal|raised}`, a `BlockCeilingLamp`, when `CeilingSupport.TryGetHeight` passes.
A framed thin floor answers `CanAttachBlockAt` for `DOWN` (0054) and is not a slab, so the lamp places as `normal`, 12/16 below the floor's underside, because `IsHanger` matched only `OmniAttachable` and `UnstableFalling`.

## Design
**Matched by code.**
`IsHanger` also answers true for a block in the `game` domain whose path starts `oillampceilingfix-`, both mounts.
`CeilingAssets.Install` adds these blocktypes in `AssetsLoaded` at ExecuteOrder 0.1, after JSON patches have run, so a JSON attribute set by a patch, #133's `vssidingHanger` idea, can never reach them.
`oillampwallfix-*`, the mod's side-mounted lamp, is not a hanger.

**Its flame.**
`BlockCeilingLamp.OnAsyncClientParticleTick` sets its particles' position itself and never calls base, as `BlockOilLamp`'s does (0054).
The particle-patch loop takes `OilLampCeilingFix.BlockCeilingLamp` by name beside `Block` and `BlockOilLamp`, with the same `ParticleSpawnTranspiler`, and skips it when the mod is absent.

## Alternatives considered
- **A `vssidingHanger` JSON opt-in (#133).** Cannot reach blocks added from code after patches; it stays a separate issue.
- **Leave it.** The lamp would hang 12/16 below the floor's underside, the gap 0054 closed for lanterns.

## Consequences & open questions
- Played under a framed thin floor: the lamp sits against the underside.
The flame's position was not separately reported.
- Played on a siding wall's face: an oil lamp places there as it does without Oil Lamps Anywhere, whose own wall lamps go on slabs and fences only.
- Under a deck nothing places, since decision 0042 cancels every placement into a decked cell; filed as #164.
- A vanilla oil lamp hosted on a wall's face breaks only from a click on its small selection box: its plate and rope lie outside the box, and a break on the hosted cell's panel is discarded (0035).
The same happens without Oil Lamps Anywhere; filed as #163.
- On an Oil Lamps Anywhere update, recheck the `oillampceilingfix-` code and `BlockCeilingLamp.OnAsyncClientParticleTick`; a changed override logs the patch as skipped and the flame burns unshifted.
