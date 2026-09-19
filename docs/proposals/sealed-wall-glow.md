# Sealed wall glow

- Status: Draft
- Created: 2026-09-18
- Reflects: playtest on branch `siding-cellar-strength` at 63ea9ae; vanilla 1.21 `ChunkIlluminator.SpreadSunlightAt` and `ServerWorldMap`/`ClientWorldMap.UpdateLightingAfterAbsorptionChange` (decompiled from `VintagestoryLib.dll`)

## Summary
A sealed siding room reads sunlight 0 at the player's feet, but daylight visibly glows in at the corners and where the walls meet the roof.
Find out which of two causes it is, then fix that one.

## Context
Decision 0015 found that a sealed wall's own cell stores the sunlight flowing in from outside (about 23), because absorption only cuts the light a cell passes on.
The room registry no longer counts that light as sky, but it's still in the cells.

What the playtest showed:
- Rooms built before stone infill (#16) are lit like outside. Those walls sealed before walls absorbed light at all, so their stored light is stale and nothing has relit it.
- A room built on the current build is dark, and `/sidingroom` reads light 0, but there's enough glow at the corners and along the roof line to see by.
- The glow is weaker at night, and placing blocks nearby at night clears most of it.

Spoilage reads the light at the container's cell, which is 0, so this is cosmetic.

## Design

**First, split the two suspects.**
Place and break a block next to a glowing corner during the day.

- **The glow goes: stale meshes.** `OnInfillChanged` calls `MarkDirty` and `ExchangeBlock` straight away, but `MarkAbsorptionChanged` only queues a lighting task. If the neighbouring chunks re-tessellate before that task runs, they bake the pre-seal light into their meshes and keep it until something redraws them. Placing a block redraws them, which fits what the playtest saw. Fix: redraw the neighbourhood after the relight lands, not before.
- **The glow stays: smooth lighting reads the wall cells.** Now I'm guessing at the tessellator: a face's corner brightness averages the light in the cells around it, skipping cells it treats as opaque. Our walls report no solid or opaque face (decision 0002), so the averaging would pick up the ~23 stored in each wall cell. That would show at corners and roof edges, where faces border wall cells diagonally. Fix: find the flag the averaging checks and set it on a sealed wall's claimed face, without changing face culling.

**Stale light in old rooms.**
A one-off relight of each existing sealed wall, for example when its chunk loads, or a note in the handbook to re-pack the infill.
Only worth doing if players have rooms from before #16.

## Alternatives considered
- **Stop the wall's cell storing light.** Not possible: the engine sets the stored light from the neighbour, and nothing on the block can veto it.
- **Fixing it without the split test.** Both suspects fit "corners and roof edges" and "weaker at night"; only the redraw test tells them apart.

## Consequences & open questions
- If it's smooth lighting, the flag that fixes it may also change how neighbouring faces cull against the wall; decision 0002 set every face non-solid on purpose.
- Whether rooms from before #16 exist in anyone's world besides the playtest save.
