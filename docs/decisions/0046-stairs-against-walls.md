# 0046 — Stairs against walls

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `stairs-against-walls`; `SidingWallEntity.Step`/`StepOrientation`/`StepElements`; `SidingWallBlock.ResolveStepOrientation`/`AddOpenPartBoxes`/`PeelLayer`/`OnBlockInteractStart`; `SidingWallTexSource`; `SidingModSystem.HostChangePrefix`/`DeckedCellPostfix`; decisions 0002, 0035, 0042; not yet played

## Summary
A staircase built along a wall stands in the room cells beside the wall column, so every step leaves the wall cell's open 12/16 between its side and the panel.
The deck's opt-in (decision 0042) is right for a stairwell's upper floor, but the stairs themselves still cannot reach the wall.
This adds a step: a wall layer that continues the stair beside it into the wall's open part, built by clicking the wall with that stair in hand.

## Context
**Why stairs don't reach.**
A wall's panel hugs one face of its cell and leaves 12/16 open (decision 0002), and a vanilla stair is a full cell wide, so it can only stand beside the wall column.
Hosting it in the wall's cell (decision 0035) is refused: `IsHostable` turns away any block with a solid side, and a stair has several.
Even hosted, it would be shifted 4/16 off the panel and poke 4/16 into the next cell, which on a two-wide staircase is the next stair.

**What decision 0042's playtest showed.**
The deck works and the upper room still seals; a stairwell left undecked keeps its gap as intended, but the stairs up to it stand three quarters of a block off the wall.

## Design
**A step layer, like the deck.**
A new optional field on `SidingWallEntity`, `Step`, beside `Deck` rather than folded into it.
`OpenPartFilled` (`Deck != null || Step != null`) is all the overlap needs, and the hosting refusals read it instead of `Deck` alone.

**Stores the held stair's block code, not a dictionary key.**
`Step = "game:plankstairs-oak-up-north-free"`, exactly what was consumed, plus `StepOrientation = "up-north"` (vanilla's own vertical-horizontal naming, world facing).
Texture, `BlockMaterial` (sounds, resistance, burning) and the drop all come from that block, so every `BlockStairs` — vanilla's eight families and any modded ones — works with no `wall.json` entries.
This is a departure from decision 0001's dictionary pattern; see Alternatives.

**Generated boxes, not clipped vanilla shapes.**
Six `WallShapeGen` elements over the open part, unrotated frame: `step-lower`, `step-upper` (full length), and a north/south half of each.
`SidingWallEntity.StepElements` picks two of the six from a side and orientation (e.g. up, rising north on a west wall, draws `step-lower` + `step-upper-north`), turning the stair's world facing into the unrotated frame for the wall's side.
One `step` texture slot, answered by `SidingWallTexSource` from the stair block's own texture source (`capi.Tesselator.GetTextureSource(block)["up"]`).
Collision and selection (`SidingWallBlock.AddOpenPartBoxes`) add the same boxes for both the deck and the step, replacing the deck-only `AddDeckBox`; `StepOccupied` guards a second build the way `DeckOccupied` guards a deck.

**Wall layout only.**
A `cornerout` step is refused; a step also refuses the in-place corner upgrade (0026), so a stepped wall can't be upgraded into a shape that doesn't have one.

**Built from the stair itself, no picker row.**
Saw in the off hand, a `BlockStairs` in hand, a click on a non-top face of a framed `wall`: unambiguous, so the step needs no row of its own.
`SidingWallBlock.ResolveStepOrientation` copies the orientation from a `BlockStairs` in the room cell beside the clicked face (`pos + side.Opposite`) when its horizontal facing runs along the wall; otherwise it comes from the player, vanilla-style (look direction snapped along the wall; hitting the `DOWN` face or above half-height gives upside-down).
Copying on the click, not watching the neighbour, keeps the wall from rewriting itself when the room changes (the `auto-corners` trap).
It costs the held stair; a build refused on a decked wall gives `vssiding:build-decked`, and the deck branch refuses a stepped wall with `vssiding:build-stepped` in turn.

**Retention and attachment unchanged.**
A stair seals nothing upward on its own, so the step doesn't either.

**One per cell, not with a deck.**
A deck and a step both fill the open part, so a cell takes one or the other.
The top step of a flight meets the deck of the floor above at the next course, which is the flush stairwell edge decision 0042's playtest wanted.

**Breaking, dropping, burning.**
`PeelLayer` takes the step in the deck's slot, outermost on the room side, and `RemoveLayer` clears both `Step` and `StepOrientation`.
`ComputeDrops`/`StepDrop` drop the stored stair block; `LayerMaterialAt` reads that block's `BlockMaterial`, so its sounds, resistance and fire behaviour follow without a `wall.json` entry, and a wood stair catches fire the way a wood top layer already does (decision 0043).
`HostChangePrefix` drops a step the same way it drops a deck when a host change would otherwise strand it.

**Hosting refused.**
`TryHost`, `DeckedCellPostfix` and the ground-storage prefix all check `OpenPartFilled` instead of `Deck` alone, so furniture is refused into a stepped cell with the same `vssiding:build-stepped` message.

**Tooltip.**
`Describe` gains a `Step: <stair name>` line, opt-in like the deck's, naming the resolved stair block.

## Alternatives considered
- **A `Steps` family dictionary, like `Framings`/`Infills`/`Finishes`.** Would duplicate what the stair block already knows: its own texture, material and drop. Rejected in favour of storing the block code directly.
- **Folding `Deck` and `Step` into one `Fill` layer with a kind.** Would rename the saved `deck` attribute and need a migration; `OpenPartFilled` covers the overlap without one.
- **Clipping vanilla's stair shapes at mesh time.** Left for generated `WallShapeGen` boxes instead, so the six elements can be picked and rotated the way the deck's boxes already are.
- **Host the stair (0035), exempting it from the solid-side rule.** The shift pushes it 4/16 into the next cell, and the solid-side refusal exists because a shifted solid face lies to culling, retention and attachment; the whole 0035 consumer table would need re-answering for a full-cube host.
- **Thin stairs, 12/16 wide.** A new block per material and orientation, and it still leaves a 4/16 gap on the room side unless the next stair is thin too.
- **Detect the stair beside the wall and fill automatically.** Wrong the moment a player wants a gap, and a wall that changes when its neighbour changes.

## Consequences & open questions
- Z-fighting where the step's room face meets the neighbour stair's face is a real risk with generated boxes matching a vanilla shape from outside; watch for it in the playtest.
- Whether per-face top/side textures matter for stone stairs (quartz, brick) or the single `step` slot is enough.
- A `cornerout` step, where a flight turns at a corner, is left out until someone builds one.
- A stair running into the wall (perpendicular), rather than along it, has its back against the panel already; this only covers stairs running along the wall.
- Not yet played: the branch has full test coverage but has not been run in game, so the z-fighting risk above and the general feel of the layer are unverified.
