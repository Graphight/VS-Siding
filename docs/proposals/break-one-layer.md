# Break one layer

- Status: Draft
- Created: 2026-09-18
- Reflects: playtest on branch `stone-infill` at 1c65919; `SidingWallBlock.OnBlockInteractStart`, `GetDrops`, `ResolveFinishFace`; vanilla 1.21 `RoomRegistry` (decompiled from `VSEssentials.dll`)

## Summary
Breaking a built-up wall takes off one layer, the most recent one on the face you hit, instead of the whole wall.
The frame goes last, when nothing else is left.

## Context
Swapping a finish or an infill today means breaking the whole wall and rebuilding it from the frame.
Playtesting stone infill ran into it: every material swap meant tearing down framing that was fine.
Players will want to re-face a wall or re-pack a cellar far more often than they want to remove it.

## Design

**Override `OnBlockBroken`; the block survives until only the frame is left.**
If the entity has an infill or any finish, remove one layer, spawn that layer's `Drops`, clear its key, and `MarkDirty`; don't call `base`, so the block stays.
A framing-only wall breaks as it does today.

**Which layer: reverse build order, starting from the face you hit.**
The face comes from `byPlayer.CurrentBlockSelection.Face`, resolved with `ResolveFinishFace` exactly as finishing does.
1. That face's finish, if it has one.
2. Otherwise another finish (`front`, `secondfront`, `back` in that order), so a hit always removes something.
3. The infill, once no finishes remain.
4. The frame.

The build flow only accepts finishes on an infilled frame, so infill waits for every finish to go; peeling never makes a wall the build flow couldn't.

**Pure function for the choice, tested without a game.**
`PeelLayer(string? face, framing, infill, front, secondFront, back) → string?` returns the layer name to clear (or `null` for "break the frame").
Assert on it per case; drops reuse `ComputeDrops` with only the peeled key set.

**Removing infill is a block change in all but name.**
Retention and the stack joins both change, so it gets the same `ExchangeBlock` (rooms recompute) and `MarkVerticalNeighboursDirty` calls that placing infill has.

**Every peel is a full break.**
Same mining time for each layer, at the block's resistance; creative peels instantly, one layer per click.

## Alternatives considered
- **Sneak-break to peel, plain break for the whole wall.** Vanilla has no such convention, so nobody would find it; and a whole-wall break is rarely what anyone wants.
- **A strip gesture with the saw (right-click in a tool mode).** Another mode to learn for what breaking already means.
- **Return all layers but keep the frame.** Swapping one face would still cost re-applying the other two layers.

## Consequences & open questions
- Is rule 2 (hitting a bare face peels the other face's finish) surprising in play? The alternative is doing nothing and showing an error.
- Per-layer mining times (straw fast, stone slow) are the natural follow-up; not needed to fix the rebuild pain.
