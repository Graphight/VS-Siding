# 0018 — Sealed cell light

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `sealed-cell-light` at c08e4d7; vanilla 1.22 `ChunkTesselator.BuildExtendedChunkData`, `TCTCache.CalcBlockFaceLight` and `CornerAoRGB` (decompiled from `VintagestoryLib.dll`); playtests in a flat creative world at y≈4

## Summary
A sealed siding room still glowed in a band along the floor at the wall base, brightest in the corners.
The light came from the sunlit cell *outside* the thin wall, averaged in by smooth lighting — not from the wall's own cell.
Faces onto a sealed wall cell now take smooth lighting's flat path, reading only that cell, and that cell now holds the light of whatever the wall's dead space opens onto.

## Context
Decision 0015 found that a sealed wall's cell stores the sunlight flowing in from outside (about 23), because absorption is subtracted only from what a cell passes on.
Decision 0016 stopped the walls themselves glowing with side AO, and left the floor edges and corners for the `sealed-cell-light` proposal.

That proposal assumed the remaining glow was the wall cell's stored light showing through the face that samples it, and offered two ways to empty that cell: the tessellator or the lighting engine.
**Both would have failed, and the split test proved it.**
Blacking out all 204 siding cells' entries in `currentChunkRgbsExt` changed nothing on screen.

## Design

**What actually lights that floor square.**
`TCTCache.CalcBlockFaceLight` has two paths.
With smooth shadows on, each of a face's four corners is `CornerAoRGB`'s average of the face's own light sample and up to three of the eight cells ringing it.
For the floor square in the dead space, the face's own sample is the wall cell, and the ring is that cell's eight horizontal neighbours — which, because the wall is one cell thick, includes the cell immediately outside in full daylight.
So the corner came out around `(0 + 24 + 24 + 24) / 4`: emptying our own cell moved one term in four.
The gradient across the square, brightest at the wall, and the double-bright corners where two walls meet are exactly what that average predicts.

In vanilla this face does not exist: a solid wall block's opaque bottom culls the floor's top face.
A siding wall is `sidesolid: false` on every face (decision 0002), so the face is drawn, and it sits on the inside/outside boundary where the ring reaches daylight.

**Two Harmony patches, client-side rendering only, registered together because neither works alone.**
- A postfix on `ChunkTesselator.BuildExtendedChunkData` rewrites `currentChunkRgbsExt` at each sealed wall cell with the light of the cell its dead space opens onto — `side.Opposite` for a `wall`, the diagonal `side.Opposite + second.Opposite` for a `cornerout` (`SidingWallBlock.OpenSide`) — and records the cell in a thread-local mask.
- A prefix on `TCTCache.CalcBlockFaceLight` returns the flat path for any face whose neighbour is a masked cell: all four corners get that cell's light, no ring.

The open side rather than the proposal's "dimmest neighbour": the dead space is continuous with the cell it opens onto, so it should look like it.
Dimmest kills a torch inside (the outside's block light is 0) and darkens sunlit ground when the panels face inward.

The mask is `[ThreadStatic]`, alongside the rgb array the postfix saw.
Tessellation runs one chunk at a time per thread, so a chunk's own face-light calls read what its postfix just recorded, and no lock or reflection is needed.

**Cost.** The postfix scans 39,304 block references per chunk mesh; the prefix adds a bool lookup to every face-light call, which is the hot one. No stutter was visible in play; no profile was taken.

## Alternatives considered
- **Emptying the sealed cell's light, by tessellator or by lighting engine (the proposal's two options).** Disproved by the split test above. The leaked light is the outdoor cell's, which neither patch touches.
- **Dimmest neighbour for the cell's light.** Wrong in the two cases named above.
- **Making a sealed wall's faces opaque so the floor face culls as vanilla's does.** The wall's mesh is thin, so the culled face would show a hole through the floor from inside the dead space.
- **A per-chunk list of wall positions instead of the scan.** The scan is microseconds against a multi-millisecond mesh build; add the list if a profile ever disagrees.

## Consequences & open questions
- Faces onto a sealed wall lose smooth shading and render flat. In play this reads as the wall base being evenly dark rather than subtly graded.
- Side AO from decision 0016 is now likely redundant — the flat path ignores the ring the side AO was there to suppress — but it was not removed or retested. Removing it would also drop the odd pre-roof corner shading 0016 noted.
- A sealed cell whose open side is a doorway takes the doorway's daylight, which may show as a bright sliver beside an opening. Seen in a night playtest, not diagnosed; the fix would be to take the darker of the open side and the room.
- An open neighbour that is itself a sealed wall (a double-thickness wall) reads order-dependent light.
- The postfix ignores `BuildExtendedChunkData`'s `skipChunkCenter`, so on the edge-only rebuild path it scans interior entries vanilla left stale from a previous chunk and looks up block entities for any siding blocks it finds there.
  Nothing drawn on that path samples the interior — the refreshed shell is deeper than the one cell an edge face reaches — so this is wasted microseconds, not a wrong pixel.
  Taking the parameter would mean reproducing vanilla's shell condition, and getting that subtly wrong brings the glow back; left until a profile asks for it.
- A sealed wall cell in the extended array's border ring whose open side leaves the array is skipped.
  That cell opens away from this chunk, so the floor face showing its dead space belongs to the neighbouring chunk, where the same cell is interior and is darkened normally.
- `RoomSkylightPatchTests` and `SealedCellLightTests` assert the patched methods and fields still exist, so a game update fails the build rather than silently dropping the fix.
