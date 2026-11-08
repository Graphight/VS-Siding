# 0089 — Fill from a face, frame on from the rest

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #155; branch `155-fill-from-face`; `SidingWallBlock.FramesOnFrom`/`FillHintKey`/`OnBlockInteractStart`/`LayerDeck`, `SidingFloorBlock.OnBlockInteractStart`, `SidingWallEntity.GetBlockInfo`, `SidingFloorEntity.GetBlockInfo`, `lang/en.json`; `SidingWallBlockBuildFlowTests`, `VSSiding.E2E.Tests/StickScenarios`; decisions 0026, 0053, 0058, 0088; unit and end-to-end tests pass

## Summary
Sticks and bamboo stakes are a framing and an infill at once, so a plain click on a bare frame filled it from any face.
Stacking or extending a run of bare frames then took a sneak-click on every cell.
Now the clicked face decides: a face that takes a finish fills, any other face frames on, as it does for planks.

## Context
Issue #155 asked for a way to build a stick or bamboo run without sneaking on each cell.
Decision 0058 made a plain click fill a bare frame and a sneak-click frame on; 0088 gave stakes the same rule.

## Design
**Held items.** The rule applies only to a held item that matches both `Framings` and `Infills` (`FramesOnFrom`).
A pure infill (clay, straw, stone) still fills from any face, and planks change nothing.

**Walls.** A bare frame fills only from a face `ResolveFinishFace` answers.
A `wall` frames on from its top, bottom and two ends.
A `cornerout` and a `diagonal` claim all four sides, so they fill from any side and frame on from top and bottom only.
A post takes no infill and is unchanged.

**Floors and decks.** A bare floor or deck fills from its top and underside (`SidingFloorBlock.FinishFace`); an edge click frames on.
Carrying a run on from the top still takes a sneak, which matters over water.
Sticks on a bare wall's end frame the next cell, decked; a stick deck in place still needs a filled wall.

**Tooltip.** With the build signal on and a frame-and-fill item in the main hand, a cell missing its infill says which faces fill and which frame on (`FillHintKey`).

**Unchanged.** Sneak-clicking works as before, and the corner upgrade (0026) stays out of reach with sticks on a bare frame, since its finish face now fills.

## Alternatives considered
- **A new key combination.** Rejected: the face already says which is meant.
- **Fill a floor from its underside only.** Rejected: the top is the natural face to fill.
- **Keep the sneak on floors.** Rejected: it is the same chore on every cell of a run.
- **Deck in place like planks.** Rejected: 0058 keeps a stick deck for a filled wall.

## Consequences & open questions
- Supersedes only the plain-click fill from any face in 0058 and 0088.
- A stick player who wants to fill a wall from its end must now sneak.
