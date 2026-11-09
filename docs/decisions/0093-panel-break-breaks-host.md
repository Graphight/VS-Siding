# 0093 — A panel break breaks the hosted block

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #163; branch `163-panel-break`; `GapShiftCollisionPatches.SwallowsPanelBreak`, `PanelInteractionPatches.GettingBrokenPostfix`, the server `BreakBlock` handler in `SidingModSystem.StartServerSide`; Atlas scenario `Lamp_Should_Break_When_ItsWallPanelIsMined`; supersedes the breaking half of the "Interaction and breaking on the panel" row in decision 0035

## Summary
Mining the panel of a wall that hosts furniture now breaks the hosted block, as if the player had clicked the furniture.

## Context
Decision 0035 swallowed every break aimed at a guest wall's panel: `GettingBrokenPostfix` returned the remaining resistance and the server `BreakBlock` handler set `PreventDefault`.
A vanilla oil lamp's selection box covers only its body, so a click on the plate and rope its `claylamp-wall` shape draws selects the panel instead, and the lamp could not be mined from there (#163).

## Design
A break on a panel is no longer discarded, so vanilla breaks the block at the position, which is the host.
The existing host-change restore puts the wall back.
This holds for every hosted block, chests and tables included: a player who mines the wall in front of a chest breaks the chest.

**Support beams keep the guard.**
`BlockSupportBeam.OnBlockBroken` passes `SelectionBoxIndex` to `BreakBeam` unchecked when the cell holds several beams, and a panel index lies past the beam's own boxes.
`SwallowsPanelBreak` is true only for a support-beam host, so both guards stay for it.

**Right-click is unchanged.**
`InteractPrefix` still swallows a right-click on the panel, since that is how blocks are placed and hosted against the panel face.

## Alternatives considered
- **Make support beams unhostable.** Removes a working case to avoid one index; the guard is smaller.
- **Redirect the break to the host's own box.** Needs a per-block choice of box and was not needed for the reported case.

## Consequences & open questions
- Mining a wall in front of a chest breaks the chest; the wall comes back.
- Played on 2026-11-08: a panel break breaks the hosted block and the wall comes back, as described.
