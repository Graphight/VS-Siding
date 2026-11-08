# 0089 — Fill from a face, frame on from the rest

- Status: Accepted
- Created: 2026-11-08
- Reflects: issue #155; branch `155-fill-from-face`; `SidingWallBlock.FramesOnFrom`/`FillHintKey`/`OnBlockInteractStart`/`LayerDeck`, `SidingFloorBlock.OnBlockInteractStart`, `SidingWallEntity.GetBlockInfo`, `SidingFloorEntity.GetBlockInfo`, `lang/en.json`; `SidingWallBlockBuildFlowTests`, `VSSiding.E2E.Tests/StickScenarios`; decisions 0026, 0053, 0058, 0088; unit and end-to-end tests pass

## Summary
Sticks and bamboo stakes are a framing and an infill at once, so a plain click on a bare frame filled it from any face.
Stacking or extending a run of bare frames then took a sneak-click on every cell.
Now the clicked face decides: a face that takes a finish fills, any other face frames on, as it does for planks.

## Context
Playtesting bamboo framing (#152) found that placing many frames with a material that also fills was a chore, and the playtester did not want another key combination (issue #155).
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

**Decks on a bare wall.** With the deck lit, sticks on a bare wall's end frame the next cell, decked, where planks deck the clicked cell in place; a stick deck in place still needs a filled wall, as 0058 says.

**Tooltip.** With the build signal on and a frame-and-fill item in the main hand, a cell missing its infill says which faces fill and which frame on (`FillHintKey`).

**Unchanged.** Sneak-clicking works as before, and the corner upgrade (0026) stays out of reach with sticks on a bare frame, since its finish face now fills.

## Alternatives considered
- **A new key combination.** The playtester asked for none, and the clicked face already tells the two apart.
- **Fill a floor from its underside only, so a plain top click carries the run on.** A floor laid out over water has an underside nobody can reach, so it could never be filled.
- **Keep every face of a floor filling.** A run laid from its edges would still take a sneak on every cell.
- **Sticks deck a bare wall in place, as planks do.** 0058 keeps a stick deck for a filled wall, and an end click framing on is the rule everywhere else.

## Consequences & open questions
- Supersedes only the plain-click fill from any face in 0058 and 0088.
- A bare wall now fills from its faces only: an end click frames on, and a sneak-click frames on too, so a wall with both faces out of reach takes no stick infill.
