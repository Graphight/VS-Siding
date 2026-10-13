# 0075 — Finishing the far face

- Status: Accepted
- Created: 2026-10-12
- Reflects: branch `far-face-finish`; issue #108 and the mod page comment https://mods.vintagestory.at/vssiding#cmt-245654; `SidingWallBlock.OnBlockInteractStart`, `OnBlockInteractStep`, `LayerDeck`, `Describe` and `DescribeFaces`; `SidingFloorBlock.OnBlockInteractStart`, `OnBlockInteractStep` and `FinishFace`; `SidingWallEntity.GetBlockInfo`; `build-already-finished` in `lang/en.json`; `VSSiding.E2E.Tests/FarFaceScenarios.cs`, `WallBuilder.Press`, `TooltipScenarios.cs`; `VSSiding.Tests/SidingWallTooltipTests.cs`; decisions 0027, 0035, 0050, 0053, 0065, 0069; vanilla 1.21 `SystemMouseInWorldInteractions`, `ServerSystemBlockSimulation` and `Block` (decompiled)

## Summary
A finish clicked on a finished wall face goes on the face behind it while that face is bare.
A floor and a deck follow the same rule for their top and underside.
A held right-click layers once, and the tooltip leaves out an unfinished face that lies against a solid block.

## Context
A wall built against dirt has a face no click can reach, and so does the outside of an upper-storey wall.
Before this, a finish went on the clicked face only.
A second click on that face showed `build-already-finished`, or, for planks and held blocks, passed on and placed something.
The request is issue #108, asked on the mod page.

## Design
**The rule.**
In the wall's finish path, a clicked face that is finished and that the click does not restyle sends the finish to the face behind it, when that face is bare.
The check that the player can afford the item, the charge and the style are the same as for the clicked face.
Where both faces are finished, the click does what it did before: a framing material or a block passes on and places, and anything else shows the message.
Sneak still places, since vanilla skips the block's interaction.

**The opposite face.**
`ResolveFinishFace(layout, side, blockSel.Face.Opposite)` names the slot.
A corner's two back faces point in different world directions, so the world-opposite of the clicked face names one slot.
On a `cornerout` hugging west, the east back reaches `front` and the south back reaches `secondfront`, and either front reaches the shared `back`.
A diagonal's claimed faces reach `back` and its backs reach `front`.
A glazed diagonal still takes no finish.

**Floors and decks.**
`SidingFloorBlock.OnBlockInteractStart` and `SidingWallBlock.LayerDeck` send the finish through `SidingFloorBlock.FinishFace(blockSel.Face.Opposite)`.
The style is picked again for the far face, since a finish lists the styles a floor takes per face (`SidingFloorEntity.HasFloorStyle`).
A framing material never takes the far face on a floor or a deck, whatever the picker says.
Planks on a boarded top still extend the run, or raise a wall on the floor (decision 0050).
A plank ceiling put on from above over water could not be reached to take off again.

**Restyle wins.**
The same material with another style picked restyles the near face, free, as in decision 0027.

**Corner is picked.**
A framing material on a `wall` layout with Corner picked falls through to the corner upgrade, as in decision 0069.
Planks and held blocks otherwise take the far face on a wall like any other finish.

**A held press layers once.**
Vanilla's client sends a held right-click to the block again every 0.25 s, and only while `Controls.HandUse` is `None`.
`Block.OnBlockInteractStep` returns false by default, which ends the interaction and lets the repeat in.
Without a guard, the press that boards the near face would board the far face a quarter of a second later.
`SidingWallBlock.OnBlockInteractStep` and `SidingFloorBlock.OnBlockInteractStep` return true, which keeps `HandUse` at `BlockInteract`.
`Block.OnBlockInteractCancel` returns true by default, so releasing the button or moving the crosshair off the cell ends the interaction.
The server mirrors this in `ServerSystemBlockSimulation`.
This was read in the decompiled 1.21 client and server and not observed in game.
Atlas boots a server only, and `WallBuilder.Press` models the client's loop by hand.
Decision 0069 records the same repeat as "A fresh press only".

**The tooltip.**
`SidingWallBlock.Describe` and `DescribeFaces` take an optional `buried` predicate.
`SidingWallEntity.GetBlockInfo` passes one that reads `SideOpaque` on the touching side of the cell a front or a second front faces.
An unfinished buried front is left out, and a finished one is still listed.
A diagonal's line goes only when both of its claimed sides are buried.
A back is never treated as buried, because it looks into its own cell.
A floor's and a deck's faces are not checked: an underside hangs 12/16 above the cell below, and a top lies against a solid block only where one stands on the floor.
A guest wall's tooltip follows the same rule, since `GuestWalls.Decode` sets the entity's `Api` and `Pos`.

**Message.**
`build-already-finished` now reads "Both sides are already finished".

## Alternatives considered
- **The far side wins over a restyle.** A player correcting a style on the face in front of them would have the far face finished instead, with a charge.
- **Planks keep their placing on walls.** Planks are the commonest finish, so the rule would skip the most common case and leave the far face reachable only with other materials.
- **Leave the tooltip alone.** A finish is appearance only, so a face against dirt would read unfinished until the player paid to finish a face nobody can see.
- **A guest wall's panel.** It takes no click (decision 0035), so its far face cannot be finished this way.
- **Folding the three finish paths into one.** The wall, the floor and the deck still each carry their own copy of the branch. A shared helper is a refactor this change does not need.

## Consequences & open questions
- A held press layers one layer per press where it could layer two. An item that is both an infill and a finish no longer fills and then finishes in one hold.
- The held-press guard is untested in game. The playtest to run is to hold right-click for two seconds with planks on a filled wall's bare face and check that only that face boards.
- Planks or a held block clicked on a finished wall face no longer place while the far face is bare. Sneaking places them.
- Two styles of one material, one per side, take three clicks from one side: restyle the near face, finish the far face, restyle the near face back.
- Atlas scenarios in `FarFaceScenarios.cs` cover a non-placing finish and planks on a finished back, a corner's two backs, a held press on a bare face, and the floor and deck cases. `TooltipScenarios.cs` and `SidingWallTooltipTests.cs` cover the buried front.
- A wall hosting furniture cannot take the far face.
