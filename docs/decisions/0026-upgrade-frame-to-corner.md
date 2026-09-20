# 0026 — Upgrade frame to corner

- Status: Accepted
- Created: 2026-09-16
- Reflects: branch `upgrade-frame-to-corner`; `SidingWallBlock.ResolveCornerUpgrade` and `OnBlockInteractStart`; `PlaceWallFrame.ToolModeOf`; `IBlockAccessor.ExchangeBlock` and `BlockEntity.Block` in `VintagestoryAPI.xml` 1.21; decisions 0006, 0008 and 0009; the `upgrade-frame-to-corner` proposal

## Summary
In `corner` tool mode, clicking an existing framing-only wall turns it into a `cornerout` in place, with the new leg on the end of the wall you clicked.
Free, and frames only.

## Context
Playtesting found the T-junction gap from `cornerout-second-front`: walls built on the outer faces of their cells, a partition meeting one, and a 0.75-wide slot between rooms in the join cell.
The fix is a `cornerout` in that cell, but the cell already holds the outer wall.
Today, clicking that wall with planks in `corner` mode doesn't touch it: `SidingWallBlock.OnBlockInteractStart` finds no infill match for planks and falls through, and `PlaceWallFrame` sees an occupied cell and places a new corner in the *neighbouring* cell.
The only way to get the corner where it belongs is to break the wall and re-place it.

Players usually find out a wall needs to be a corner when the partition reaches it, so the upgrade has to work on a wall that's already there.

## Design

**Where: `SidingWallBlock.OnBlockInteractStart`, in the existing `Infill == null` branch, before the infill match.**
Conditions: saw in off hand (decision 0006), held item matches a `Framings` entry, the held stack's tool mode resolves to `cornerout` (`ResolveLayout`), this block's `layout` is `wall`, `Framing != null`, and the click landed on the wall's front or back face (`ResolveFinishFace` returns non-null).
Anything else falls through exactly as today.

**End, top, and bottom faces keep placing a new corner next door.**
Today a corner-mode click on a frame's end face falls through to `PlaceWallFrame`, which puts a `cornerout` in the neighbouring cell; that's how a corner goes on the end of a run, and the frame it's placed against is nearly always bare.
The upgrade only claims front and back clicks, which today place a corner in the cell in front of the wall, rarely what anyone wants.

**Frames only.**
A `cornerout`'s legs share `Framing`, `Infill`, and `Back` (decision 0007, `cornerout-second-front`).
Converting a filled or finished wall would build the new leg's infill and finish for free, or charge for materials the player isn't holding.
On a bare frame only framing matters, and the build order stays frame → corner → fill.
A filled wall that should have been a corner is broken and rebuilt; breaking already returns every built layer (decision 0003), so that costs time, not material.

**Which corner: the end of the wall the player clicked.**
A wall with side `S` is one leg of two possible corners: `cornerout-S`, which adds the leg `CorneroutSecondFace[S]`, or `cornerout-X` where `CorneroutSecondFace[X] == S`, which adds the leg on the other end.
`BlockSelection.HitPosition` says which end was nearer.

Those two corners are exactly the two ends `RunNeighbours(S)` already returns, so `ResolveCornerUpgrade` reuses it rather than carrying a second per-side table: the `left` end gives `cornerout-S`, the `right` end gives `cornerout-{right.Code}`, and the new leg is always on the end clicked.
Which end is nearer is the sign of the hit point's offset from the cell's centre along the `left` facing's normal.
It has to be a dot product rather than a fixed "coordinate below 0.5", because both the run's axis and its direction change with `S`: a `west` wall's `left` is north, so a hit at z = 0.2 is the near end, but an `east` wall's `left` is *south*, so the same z = 0.2 is the far one.
Dead centre goes right, arbitrarily but consistently.

Pure function `ResolveCornerUpgrade(string side, Vec3d hitPosition) → string cornerSide`, tested for all four sides at both ends.

**Swap: `IBlockAccessor.ExchangeBlock`**, which sets the block "without calling OnBlockRemoved or OnBlockPlaced, which prevents any block entity from being removed or placed" — so `Framing` survives.
The entity's `Block` reference needed checking, since `OnTesselation` reads `Block.Variant["layout"]`, and it turns out to need no help: `VintagestoryAPI.xml` on `BlockEntity.Block` says "this property is updated by the engine if ExchangeBlock is called".
So the swap is `ExchangeBlock`, then `MarkDirty(true)` to re-tesselate.

`MarkNeighboursDirty` follows it, which the proposal missed.
Whether a cell draws its plates depends on the cells above and below sharing its `layout` (decision 0008, via `SameRun`), and the swap just changed this cell's — without it the neighbours keep drawing yesterday's join until something else disturbs them.

**No charge.**
A fresh `wall` frame and a fresh `cornerout` frame cost the same `Consumes` and drop the same `Drops`, so an upgrade adds nothing to pay or refund.
The held planks aren't consumed and their material doesn't matter; the existing `Framing` stays.

**Retention is untouched.**
A frame has no infill, so neither face is claimed until it's filled (decision 0003).

## Alternatives considered
- **Auto-corners** (walls pick their corner piece from neighbours). Parked: fighting a tool that rewrites your blocks is worse than placing corners yourself.
- **Convert filled and finished walls too.** Free material for the new leg, or a charge for materials the player isn't holding (above).
- **Upgrade picks the corner from the player's position instead of the hit point.** The player is usually standing square to the wall, so position doesn't say which end they mean; the click does.
- **Charge framing for the new leg.** Placing the same corner fresh costs the same as a wall, so charging here would make the upgrade dearer than getting it right first time.

## Consequences & open questions
- `framing-only-collision` (decision 0008) skips plates when the block above or below has the same `layout` and `side`. A corner upgraded in the middle of a wall stack brings its plates back at both joins. That rule probably wants "the neighbour covers this wall's face", which a `cornerout` over a `wall` does; settle it in whichever of this and `finish-style-choice` ships second.
- Front and back clicks in corner mode stop placing a corner in the cell in front of the wall. To do that deliberately, click the ground there instead.
- `window-frames`' `window` layout can't be upgraded; only `wall` can.
- Downgrading a corner back to a wall isn't planned. Break and re-place; ask again if it turns out to be common.
- The same "build from the room side needs no T-junction corner" note belongs in the handbook page, so players don't need this at all for most builds. There is no handbook page yet.
