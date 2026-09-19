# Siding cellar strength

- Status: Accepted
- Created: 2026-09-18
- Reflects: playtest on branch `stone-infill` at ece631e; vanilla 1.21 `RoomRegistry` and `BlockEntityContainer.GetPerishRate`, vanilla 1.21 `ChunkIlluminator.SpreadSunlightAt` (decompiled from `VSEssentials.dll`/`VintagestoryLib.dll`); graduated on branch `siding-cellar-strength` at 4896363

## Summary
A sealed, dark siding cellar got about half the spoilage bonus of an identical rammed-earth cellar.
The cause was skylight, sampled from the wrong cell; a Harmony transpiler on the room registry's sample fixes it.

## Context
Stone infill's playtest found three problems; that PR fixed two of them.
Rooms went stale after packing infill (fixed with `ExchangeBlock`), and sealed walls let sunlight through (fixed by making a sealed wall absorb light).

The comparison that was left: two buildings with the same outside size, rammed-earth floors and ceilings, and the same door.
The only difference is the walls, siding with stone infill against rammed earth.
Both rooms are green under `/debug rooms hi` (no exits), and both are small (siding 5/3/6 including the dead space, rammed earth 3/3/4).
The siding vessel spoiled at about halfway between outside and the rammed-earth cellar.
The status HUD still shows "room", not "cellar", for the siding room; it reads the client's room registry, so it can disagree with the server.

## Design

**Found by decompiling, then confirmed in play.**
`RoomRegistry.FindRoomForPosition` samples sunlight once per column, at the first cell its flood fill visits in that column, and counts the column as sky if that sample is `>= SunBrightness - 1`.
The lighting engine (`ChunkIlluminator.SpreadSunlightAt`) stores incoming light in a cell and subtracts the cell's own absorption only from the light it passes on to its neighbours, not from what it stores.
So a sealed wall (absorption 99) sitting beside sunlit outdoor air (24) still stores 23 in its own cell — exactly the threshold.
The dead-space columns inside a siding room are all wall cells, so 18 of the 30 columns in the playtest room counted as sky.
Cellar strength in a small room is `1 − 0.4 × skylight share − 0.5 × min(warm / cold, 1)`, so that alone accounts for up to `0.4 × 0.6 = 0.24` of the lost strength, and the same skylight share raises the light-warming factor by up to `1.75 × 0.6`.
Rammed-earth walls never enter the flood at all, so that room only ever samples interior columns.
After the fix, `/sidingroom` in the stone siding cellar reads `cooling 123, warm 7, sky 1/30, exits 0, small True`, the HUD shows a cellar, and the vessel gets the cellar bonus.
A wattle room also shows as a cellar (`cooling 19, warm 61, sky 1/20`), which is vanilla's rule: any small sealed room is one, and the warm walls halve its strength to about 0.48.
Its vessel spoils slower than outside but faster than the stone or clay cellars, as expected.

**Fix: a Harmony transpiler on `RoomRegistry.FindRoomForPosition` (VSEssentials, private).**
It replaces the method's one `IBlockAccessor.GetLightLevel(BlockPos, EnumLightLevelType)` callvirt with a static `SidingWallBlock.RoomSunlight(IBlockAccessor, BlockPos, EnumLightLevelType)`, which returns 0 when the cell's block is a `SidingWallBlock` whose `GetLightAbsorption(accessor, pos) > 0`, and the vanilla value otherwise.
Unsealed frames keep vanilla light — they're exits anyway, so counting them as sky is correct.
Patched once in `SidingModSystem.Start`, guarded by `!Harmony.HasAnyPatches("vssiding")` (singleplayer runs both sides in one process, so `Start` runs twice), unpatched in `Dispose`.
The transpiler throws unless it rewrites exactly one call site; a test asserts that against the real method's IL, so a game update that changes the call count fails our build instead of silently patching the wrong thing.
At runtime `Start` catches that throw and logs an error, so players on an old release after a game update keep the mod and lose only this fix.

**`/sidingroom` (controlserver) stays.**
It prints the room counts and the sunlight level at the player's feet, for the next playtest that questions a room.

## Alternatives considered
- **Sealing both faces of a wall, so the flood never enters a wall's cell.** Rejected in the playtest: it pushes the dead space out of the room, which is the space `furniture-against-thin-walls` wants inside it.
- **Guessing a fix without the counts.** Two plausible readings of the code (room size, then warm ceilings outweighing the walls) were each disproved in play.
- **Postfix on `FindRoomForPosition` that recounts skylight from the room's cells afterward.** Can't know which cell of a column vanilla's flood actually sampled, so a general recount would change vanilla's own rooms too, not just siding ones.
- **No hook without Harmony.** The light a wall's cell stores comes from its neighbour's flood-fill pass, not from anything the wall itself controls or can veto; there's no vanilla event to intercept.

## Consequences & open questions
- The HUD's label came from the HUD Clock mod, which calls a room a greenhouse when more columns are sky than not, and a cellar when it's small; the skylight fix was enough to change it.
- The same stored sunlight still shows as a visible glow inside a sealed siding room; see the `sealed-wall-glow` proposal.
- A glass roof over a wall column now reads dark for that column even though it's genuinely lit; interior columns (not wall cells) still count as sky normally, so this only affects columns the flood enters through a wall.
