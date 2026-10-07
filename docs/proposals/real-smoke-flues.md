# Real Smoke flues

- Status: Draft
- Created: 2026-10-06
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-245074; the mod page and shipped assets of Real Smoke 2.0.0-pre.7 (`patches/tag-porous.json`, `patches/realgaspump.json`, `patches/remove-blockbehaviorchimney.json`, the handbook text in `lang/en.json`); Real Smoke's public source at https://codeberg.org/xyrvexus/RealSmoke, branch `rs_v2` at 2.0.0-pre.7 (`SmokeFlowField.ReadCell`, `SmokeUtils.ChimneyOutputPosition`, `GasUtils.IsAdjacentSidePorous`, `EnvironmentSampler.IsPassableForRainfall`); `SidingWallBlock.GetLiquidBarrierHeightOnSide`, `GuestSealingPatches`, `blocktypes/wall.json`, `blocktypes/floor.json`, `SidingModSystem.IsHostable`; decompiled 1.22.7 `Block.GetLiquidBarrierHeightOnSide` and `Block.SideIsSolid`; vanilla `firepit.json`, `forge.json`, `clay/oven.json`, `clay/chimneycourse.json`, `wood/stickslayer.json`; decisions 0001, 0002, 0003, 0019, 0020, 0035, 0042, 0043, 0050; not played with that mod

## Summary
A player vents a kitchen through trellises so smoke gets out, which costs the room its heat, and asks for an oven or hearth built into a wall or a flue made from a trapdoor.
The smoke is probably the Real Smoke mod's, which the player did not name.
Real Smoke stops smoke at a face that dams liquid, and a Siding wall already reports that per face from its infill, so a filled wall should hold smoke with no change.
The proposal is a playtest of what reading cannot settle, and a vent built on Real Smoke's own chimney behaviour if no vanilla block already serves.

## Context
The request: "I use trellises in my kitchen area so smoke ventilates (non vs siding house) but this causes heating issues, it may be value added to look into how ovens or hearth spaces can be merged into your walls, or cannibalize the existing trap door behaviors to create a flue, so cooking can be done with out being exposed to the elements".

**What Real Smoke does, from its page.**
Fire sources emit smoke that rises, spreads and needs a way out; dense smoke puts fires out and suffocates players.
Version 2.0.0-pre.7 covers game 1.22.0 to 1.22.7, runs on both sides, and its page counts about 240,000 downloads.
"Smoke can travel through certain blocks even though they have collision boxes": those tagged `porous`, and those tagged `semiporous` slowly.
`tag-porous.json` adds `porous` to fences and their gates, ladders, the refractory grating, branchy leaves, barred and window trapdoors, and barred and crude doors, and `semiporous` to thatch roofing.
"Smoke can travel through holes as small as 4x4 voxels".
Trellises are not tagged; how one passes smoke was not looked at.

**What decides whether smoke crosses a block, from its source.**
`SmokeFlowField.ReadCell` opens every face of a block carrying either tag; for any other it keeps the block's collision boxes and seals each face where `GetLiquidBarrierHeightOnSide` reaches 1.
Solidity is read in three other places.
The flue search (`SmokeUtils.ChimneyOutputPosition`) climbs from a fire until it meets a pump, and gives up only at a block whose `SideSolid` is true on every face.
The side a fire may vent to, and the cell a pump may put smoke into, must fail `Block.SideIsSolid` on the facing side (`GasUtils.IsAdjacentSidePorous`).
Rainfall passes a block that is not `SideSolid` on the entered face or its opposite and has no sound retention on either (`EnvironmentSampler.IsPassableForRainfall`).

**What a Siding wall reports to each.**
`SidingWallBlock.GetLiquidBarrierHeightOnSide` returns 1 on the face a wall claims whenever its framing and infill retain, and 0 otherwise, glazing included (decision 0019).
So a filled wall seals its face against smoke as it does against water, and a bare frame does not.
`GuestSealingPatches` returns the same for a wall whose cell hosts furniture (decision 0035).
A wall's collision is its 4/16 panel, which leaves 12/16 of the cell open, three times the 4 voxels smoke needs.
It asks only the faces the wall claims, so a deck's top returns 0 although the deck retains there; decision 0042 left liquids on a deck's top false "until play says otherwise".
The thin floor has no override, so vanilla's default reads `floor.json`, which declares the top solid whatever is built on the joists; decision 0050 chose that, "so water rests on joists".
Every face of a wall declares `sidesolid: false`, and every face of a thin floor but its top (decision 0002), so the flue search never stops at either.
Neither block overrides `SideIsSolid`: decision 0020 left it false as cosmetic, its one vanilla consumer being how water draws an edge.

**Chimneys are a block entity behaviour.**
`realgaspump.json` adds `BlockEntityBehaviorRealGasPump` to `claybrickchimney` and to `stickslayer`, and `remove-blockbehaviorchimney.json` takes vanilla's `Chimney` behaviour off the first.
The page documents it for other mods under "Integrating new Chimneys": "Any block can be made into a chimney by adding a block behavior to it".
Its properties are `inputLocation` and `outputLocations`, each one of above, below, left, right, front, back or within, `outputDistanceFromInput`, and `amountLimit` in units of smoke a second.
The form the page recommends is a JSON patch that `dependsOn` `realsmoke`, so it costs nothing when that mod is absent.
"A chimney is detected only if the blocks between it and the fire are not solid", which in the source is the flue search above.

**A behaviour or a tag cannot follow the infill.**
Both attach to a block code, and a wall's code carries its `layout` and `side` and nothing else.
Framing, infill and finishes are block entity state (decision 0001).
So either one on the wall block would apply to a stone-filled wall and a bare frame alike.

**What the player lacks.**
An opening that lets smoke out and keeps the room.
A trellis does the first and not the second.
A Siding wall reports its own retention to vanilla's room test, per face and from its infill (decisions 0002, 0003), so it can do both.

**Fires in a wall's cell.**
A firepit, a forge and a clay oven each declare `sidesolid: false` on every face, so none fails the solidity test in `IsHostable` (decision 0035).
None has been hosted in play, with or without Real Smoke.

## Design
**Play what reading cannot settle.**
One sealed Siding room with a firepit, under Real Smoke 2.0.0-pre.7, `/sidingroom` run before lighting it.
- That a filled wall, a glazed cell and a wall hosting furniture hold smoke, and a bare frame passes it, as the source reads.
- The thin floor. Its top reads as sealed before it is filled, so bare joists may hold smoke under them.
- A wall's deck. Its top reads as open after it is filled, so smoke may rise through a deck beside a floor that holds it.
- A `claybrickchimney` or a stick layer in the storey above a thin floor. The flue search passes any block not solid on every side, so it should draw a fire through a filled floor.
- A fire beside a filled wall. The sideways vent calls `SideIsSolid`, which is false on every wall face, so the fire may count the wall's cell as a place to vent.
- Whether a window trapdoor, a barred one or a stick layer, each already a way out for smoke, keeps the room sealed when set in the roof. If one does, it is the flue the player asked for and Siding adds nothing.
- Whether a firepit, forge or oven hosts in a wall's cell, where its smoke starts from, and whether a wood layer beside it catches (decision 0043).

**Fix what the playtest shows wrong.**
A floor that holds smoke on bare joists needs the override the wall has, and a deck that passes it needs the wall's override to ask the top.
Both change what water does too, since the method is vanilla's liquid test, so each revisits a row decisions 0050 and 0042 settled for water alone (issue #101).
A fire that vents into a sealed wall is a second consumer of `SideIsSolid`, which an override returning the liquid barrier's answer would settle.
A flue that draws through a sealed floor reads the `SideSolid` field, which no override reaches.

**A vent, if nothing above already serves.**
A wall drawn with louvres between the timbers, which seals as any filled wall does and carries `BlockEntityBehaviorRealGasPump` from its room side to its far side.
The pump is the route because it is the one Real Smoke documents for other mods, it moves smoke one way at a set rate, and the wall's face stays sealed to water, to the room test and to the smoke field.
It attaches to a block code, so the vented wall needs a code of its own: one more state on the wall block, or a separate block.
Decision 0001 keeps material out of variants, and one state for "vented" is the least this can cost.

## Alternatives considered
- **The `porous` tag in place of the pump.** One patch line on the same block code, so the same cost. Smoke then drifts both ways through the cell at no set rate. It suits a lattice drawn open more than a flue.
- **Tag or pump the wall block itself.** No new block code, and every Siding wall then passes smoke whatever fills it.
- **A hook on Real Smoke's side that queries the block at a position.** The infill could then determine it with no new block code. The page lists an "Improved API" as planned; nothing of the kind ships today.
- **A flue block of Siding's own, apart from the wall.** The page allows for it and the pump makes it cheap. It would stand in a cell beside the wall where the request is for something in the wall, and vanilla's chimney and stick layer already fill that place.
- **An oven or hearth as a wall layer.** The request's first idea. Hosting already puts a fire block in a wall's cell (decision 0035), so a built-in hearth is the playtest's last question and no new layer.
- **Simulate the heat a vent loses.** Heat beyond vanilla's room retention is out of scope for the mod.
- **Do nothing and name a vanilla workaround.** Right if a porous trapdoor or a stick layer keeps the room, which the playtest checks.

## Consequences & open questions
- Confirm with the player that the smoke is Real Smoke's, and which fire blocks their kitchen uses.
- Real Smoke's repository has no licence file. Its source was read to learn which members it calls; nothing is copied from it, and a vent uses only the JSON patch its page publishes.
- Real Smoke 2.0.0 is in pre-release and its page says more changes are coming, so what the source reads may move before release.
- A vent that keeps heat and passes smoke is not physical. Vanilla's chimney sits above the room, so the question does not arise there. Whether that is acceptable is a call for after the playtest.
- Which way a pump's `front` and `back` point on a wall whose `side` is its only orientation; not read.
- A pump puts smoke only into a cell that fails `SideIsSolid` towards it, so a vent's far side must face open air or a block that does.
- Whether the thin floor needs the same vent, as a hatch over a hearth.
