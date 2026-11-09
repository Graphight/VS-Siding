# 0093 — A panel break breaks the hosted block

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #163; branch `163-panel-break`; the server `BreakBlock` handler in `SidingModSystem.StartServerSide` and `PanelInteractionPatches`, which lose their break guards; Atlas scenario `Lamp_Should_Break_When_ItsWallPanelIsMined`; supersedes the breaking half of the "Interaction and breaking on the panel" row in decision 0035

## Summary
Mining the panel of a wall that hosts furniture now breaks the hosted block, as if the player had clicked the furniture.

## Context
Decision 0035 swallowed every break aimed at a guest wall's panel: an `OnGettingBroken` postfix returned the remaining resistance and the server `BreakBlock` handler set `PreventDefault`.
A vanilla oil lamp's selection box covers only its body, so a click on the plate and rope its `claylamp-wall` shape draws selects the panel instead, and the lamp could not be mined from there (#163).

## Design
Both guards are gone, so vanilla breaks the block at the position, which is the host.
The existing host-change restore puts the wall back.
This holds for every hosted block, chests and tables included, and from either side of the wall: the panel is selectable from the room and from outside, so mining the outside of a house breaks a chest hosted against that wall inside.

**Support beams are no exception.**
A cell with one beam breaks like any other host.
A cell with several beams breaks the beam its `SelectionBoxIndex` names; a panel index lies past its beams, and `BEBehaviorSupportBeam.BreakBeam` returns on an index out of range, so nothing breaks, as before this decision.

**Right-click is unchanged.**
`InteractPrefix` still swallows a right-click on the panel, since that is how blocks are placed and hosted against the panel face.

## Alternatives considered
- **Keep the guards for support-beam hosts.** Built first, on a misreading that `BreakBeam` takes the index unchecked; it also stopped a single beam breaking from its panel.
- **Redirect the break to the host's own box.** Would let a cell of several beams break one from the panel; not needed for the reported case.

## Consequences & open questions
- Mining a wall that holds a chest breaks the chest, from either side; the wall comes back.
- Mining the panel of a cell holding several beams breaks nothing.
- Played on 2026-11-08: a panel break breaks the hosted block and the wall comes back, as described.
