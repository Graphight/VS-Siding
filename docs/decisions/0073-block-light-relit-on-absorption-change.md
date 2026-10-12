# 0073 — Block light is relit when a wall's absorption changes

- Status: Accepted
- Created: 2026-10-12
- Reflects: branch `hosted-light-and-wattle`; issue #93; `GuestLightPatches.SunLightPostfix`, `SidingWallBlock.MarkAbsorptionChanged`; `LightScenarios.RoomLight_Should_StopAtAFrameOnceItIsFilled_And_PassAgainOnceItIsEmptied`; `RoomSkylightPatchTests.AbsorptionRelightMethodsStillTakeTheSameArguments`; decisions 0034, 0072, 0074; vanilla 1.22 `ChunkIlluminator`, `ServerSystemRelight`, `ClientSystemRelight`, `WorldChunk` (decompiled); not yet played

## Summary
A wall's layers change its light absorption without changing its block, and vanilla relights only sunlight for that.
A postfix now relights block light too, so a torch's light stops at a frame once it is filled and passes again once it is emptied.

## Context
`IBlockAccessor.MarkAbsorptionChanged` queues an absorption-changed task.
Vanilla 1.22 `ServerSystemRelight.ProcessLightingTask` and `ClientSystemRelight.ProcessLightingTask` run only `ChunkIlluminator.UpdateSunLight` for one.
`UpdateBlockLight` runs only for a block change whose two blocks differ in absorption.

So filling or emptying a frame beside a torch left the torch light as it was.
The bug predates this branch.
`LightScenarios.RoomLight_Should_StopAtAFrameOnceItIsFilled_And_PassAgainOnceItIsEmptied` failed on this branch before the postfix, with infill alone: the clay went in and the cell outside stayed lit for 100 ticks.
1.3.0's `OnInfillChanged` makes the same two calls, `MarkAbsorptionChanged` and `ExchangeBlock` to its own id, so the release has the bug by reading; the scenario was not run on the 1.3.0 tag.
Issue #93 and decision 0034 both read as if infill already relit block light.
It relit sunlight only.

## Design
`GuestLightPatches.SunLightPostfix` runs after `ChunkIlluminator.UpdateSunLight`, the call both relight systems make for an absorption-changed task, and whose returned set of chunks they then mark.
When the old and new absorption differ and the cell holds a siding wall, a siding floor or a guest wall, it runs `UpdateBlockLight` with the same old and new absorption and adds the chunks that returns to the set.
A cell holding any other block is left alone, so another mod's `MarkAbsorptionChanged` is untouched.

`AbsorptionRelightMethodsStillTakeTheSameArguments` fails `just test` if either method drops the argument names the postfix relies on.

## Alternatives considered
- **A fake block-change task with a stand-in block id.** The block-change path runs `UpdateBlockLight`, but it needs two blocks whose absorption differs, and a stand-in id would reach everything else that reads the task.
- **Postfixes on both `ProcessLightingTask` methods.** Marking the touched chunks from there needs the client's internal world map.
- **Calling the illuminator from the main thread.** The illuminator is not safe for that.

## Consequences & open questions
- A block change that vanilla already relit runs the block light update twice at a siding cell, to the same result.
- Floors are included by the same check.
- Finish edits, layout upgrades and client syncs now mark an absorption change when a finish moves it (decision 0074), and this postfix is what turns that into a block light relight.
- Not yet played.
