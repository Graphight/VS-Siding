# 0049 — Beds against thin walls

- Status: Accepted
- Created: 2026-09-29
- Reflects: branch `beds-against-thin-walls` (commits `bb3ea95`, `4c7e6a4`, `de11ffb`, `b6c1713`, `6ece996`); vanilla 1.22 decompiled source

## Summary
A bed can now be placed with its head or its whole length against a thin wall.
Each wall in the bed's footprint becomes a guest as in decision 0035, and the bed is drawn, collided with and slept in 4/16 off the panel.

## Context
A mod page comment asked for beds against walls.
Decisions 0035 and 0036 excluded beds, because `IsHostable` refused any block with a `part` variant.
`BlockBed.TryPlaceBlock` puts the feet at `blockSel.Position` and the head at the feet plus the player's facing, and both parts carry `side` set to the opposite of that facing.
Each cell's `CanPlaceBlock` reaches the wall's `IsReplacableBy`, which returned false for a bed, so a bed against a wall stood a full cell back.
Vanilla's `BlockBed` and `BlockEntityBed` hold no spawn-point logic, so hosting a bed touches no spawn behaviour.

## Design
**The footprint rule.** `BlockBed` is not a `BlockBehaviorMultiblock`, so the trunk postfix of decision 0036 never sees a bed, and each cell's `CanPlaceBlock` has no view of the other cell.
A prefix on `BlockBed.TryPlaceBlock` works out both cells the way vanilla does, from `Block.SuggestedHVOrientation`, and refuses with `notenoughspace` unless `SidingModSystem.BedFootprintHosts` accepts them.
There are two accepted footprints, and a footprint with no wall in it is left to vanilla:
- **Side-on.** A bed runs along its `side` axis where a trunk runs across it, so this reuses `FootprintHosts` with the bed's side turned a quarter: both cells are `layout: wall` sharing one side along the bed's long axis.
- **Head-on.** The head is in a straight wall that claims the headboard's face (`FromCode(side).Opposite`), and the feet cell is not a wall.

**Why head-on takes wall plus air.** Decision 0036 refused wall plus air for a trunk, because the shift would have to come from whichever cell holds a guest.
A bed is the natural case for it: headboard to the wall is the usual way to place a bed.
The feet cell has no guest, so `GapShiftAt` resolves a feet block through `BedHeadPos`, vanilla's own feet-to-head step, to the head's guest.
The shift therefore has a single source, and render and collision both pick it up, since both go through `GapShiftAt`.

**The panel click.** A right-click on a wall's inner face, through `TryHost` or, with a saw in the off hand, through vanilla's `OnBlockBuild`, names the wall's own cell, which would make it the feet cell and push the head through the wall.
In the same prefix, `BedFeetPos` moves the feet one cell back when `blockSel.Position` is a straight wall and the player faces into its panel, so the head lands in the wall's cell.
Both click paths reach `TryPlaceBlock`, so one retarget covers them.

**The sleeper.** `BlockEntityBed.Position` places the sleeper at the unshifted cell edge, 4/16 off the drawn bed.
A postfix on the getter adds `GapShiftAt(Pos, Block)` to the returned `EntityPos`; the getter rebuilds it from `Pos` on every call, so adding in place is safe.

**Getting up.** `BlockEntityBed.DidUnmount` tries each cell beside the head, then the feet, and teleports the sleeper to the first one their box does not collide with.
The cell past a thin wall's panel is open, so it passed, and a sleeper with no free spot on the room side got up outside.
A transpiler swaps both `CollisionTester.IsColliding` calls for `BedExitBlocked`, which also counts a step across a panel as a collision: a panel on the face the step leaves by, or on the face of the cell it lands in.

**What came free.** `HostChangePrefix` writes each half's guest on its `SetBlock` with no change.
The bed's own `OnBlockRemoved`, which removes the partner half, goes through the deferred restore, so breaking either half returns the walls with their layers.

## Alternatives considered
- **Side-on only.** Keeps the trunk's rule unchanged, but leaves the most common bed placement, headboard to the wall, standing a full cell back.
- **Leave beds out.** The gap decision 0035 set out to close stays open for the one piece of furniture people most want against a wall.
- **Do not shift the bed.** The headboard would clip 4/16 into the panel.

## Consequences & open questions
- Not yet played. To check: head-on from a floor click and from a panel click, with and without a saw in the off hand; side-on along a run; refusals for feet-to-panel, a cornerout and a cross-axis wall; sleeping in each and getting up; breaking the head, then the feet, of each; `/sidingroom` beside a hosted bed still counting the room sealed; save and reload with a hosted bed.
- Bed boxes fill the cell, so the whole bed sits 4/16 off the panel, and a head-on bed's foot overhangs 4/16 into the next cell.
- That overhang has the collision gap described in `docs/proposals/guest-furniture-collision.md`; its clamp to the cell would cut the foot's box at its cell edge.
- Feet-to-panel stays refused: a bed whose feet meet a wall's panel with the head in open floor has no guest to shift by.
- Recheck on a game update: `BlockBed.TryPlaceBlock` (its parameter names are pinned in `HostChangeTests`), `BlockEntityBed.Position`, `BlockEntityBed.DidUnmount` (the transpiler expects two `IsColliding` calls), and the feet-to-head step `BedHeadPos` copies from `BlockBed.OnBlockInteractStart`.
