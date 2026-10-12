# 0074 — Porous infill

- Status: Accepted
- Created: 2026-10-12
- Reflects: branch `hosted-light-and-wattle`; mod page comment https://mods.vintagestory.at/vssiding#cmt-243541; `SidingWallBlock.ComputeLightAbsorption`/`IsPorous`/`IsFinished`/`AbsorptionOf`, `SidingWallEntity.FromTreeAttributes`, the wattle entry of `config/materials.json`; `SidingWallBlockRetentionTests.AnUnfinishedPorousWallPassesLightAndNothingElseChanges` and `AWallIsFinishedOnceItsBackOrItsOuterFacesAreCovered`; `LightScenarios.BareWattle_Should_PassRoomLightUntilAFaceIsFinished` and `HostedTorch_Should_LightBothSides_When_TheWallIsBareWattle`; decisions 0015, 0018, 0019, 0034, 0072, 0073; vanilla 1.22 `ChunkIlluminator`, `ServerSystemRelight`, `ClientSystemRelight`, `WorldChunk` (decompiled); not yet played

## Summary
Wattle infill passes light until a face is finished, and a finished wall stays dark.
The flag is `Porous: true` on the infill's entry in `config/materials.json`, and wattle is the only entry that sets it.

## Context
Mod page comment 243541 asked for wattle to let light through while it is bare.
Decision 0019 lets glazing pass light by marking an infill `Transparent`.
Wattle passes light only until a face is finished, so it needs a flag of its own.

## Design
`SidingWallBlock.ComputeLightAbsorption` gains a `finished` argument, default true.
Absorption is 99 when the wall retains, its infill is not `Transparent`, and either the wall is finished or its infill is not `Porous`.
Otherwise it is 0, the same as an open frame and glazing.

`SidingWallBlock.IsFinished` decides per layout.
A `wall` or a `diagonal` is finished when `Back` or `Front` is set.
A `cornerout` is finished when `Back` is set, or when `Front` and `SecondFront` are both set, since its two outer faces are separate layers.
`AbsorptionOf` reads the entity's finishes and passes the answer.

**Floors and decks stay opaque once filled.**
`SidingFloorBlock` calls the static `ComputeLightAbsorption` with the default, so it never reads a finish.
Sunlight falls straight down through absorption 0 at full strength, so a bare wattle floor used as a roof would read as open sky.

**Rooms, cellars and greenhouses do not change.**
`SidingWallBlock.RoomSunlight` (decision 0015) calls the static with the default too, so it keeps the infill-only answer.
By decision 0015's numbers, sunlit outdoor air at 24 leaves 23 in the wall's cell and 22 in the room cell beside it, under the registry's sky threshold of 23.
That figure is derived, not measured; `RoomScenarios` passes unchanged.

**Side AO and the 0018 patches** treat a bare wattle wall as they treat glazing, since `IsSealed` reads the wall's absorption.

**Relight.**
Finish edits (`SetFinish`, `RemoveLayer`), a layout upgrade (`TryUpgradeLayout`) and a client reading a synced tree (`SidingWallEntity.FromTreeAttributes`) read the absorption before and after, and call `MarkAbsorptionChanged` and `MarkNeighboursDirty` when it differs.
Decision 0073's postfix turns that into a block light relight.

**Existing saves.**
Stored light is not recomputed on load, so a standing bare wattle wall passes light only after the next relight near it.

## Alternatives considered
- **Marking wattle `Transparent`.** `IsTransparent` also moves the infill's mesh to the transparent render pass and merges neighbouring cells as glazing, and it has no finished state.
- **A partial absorption, so bare wattle dims the light.** `IsSealed` and `RoomSunlight` read any absorption above zero as an opaque wall, so each consumer would need its own answer for a value in between.
- **Bare wattle floors and decks passing light.** Ruled out above.
- **Cloth as a second porous infill.** One line in `config/materials.json`; nobody has asked for it.

## Consequences & open questions
- A torch hosted on bare wattle lights both sides, since decision 0072 marks only a wall with absorption above zero.
- Daub on a face puts the light out, and peeling the daub brings it back.
- Not yet played.
