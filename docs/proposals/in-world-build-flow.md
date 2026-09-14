# In-world build flow

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; `vsroofing_1.7.2` shipped `patches/items.json` and handbook guide text; no code yet

## Summary
Walls are built in place, layer by layer, the way Roofing builds roofs: right-click with a framing material to place a frame, then shift-right-click the frame with insulation, then with an exterior.
Each step consumes the held item and fills one key on the block entity.

## Context
Without a build flow, walls only exist via creative inventory and commands, and there's nothing to play.

Roofing's shipped assets show its flow clearly.
`patches/items.json` adds a `vsroofing.PlaceFrame` collectible behavior to vanilla planks, sticks, bamboo, rods, and firewood.
Its handbook says: place the frame with right-click (frame type chosen via tool modes), add roofing materials with shift-right-click, some materials needing a hammer in the off hand.
Players already know this pattern from Roofing, so copying the *interaction design* costs them nothing to learn.

## Design

**Placing a frame: a `vssiding.PlaceWallFrame` collectible behavior, patched onto framing items.**
A JSON patch adds it to `game:itemtypes/resource/plank.json` (and sticks later).
The behavior maps the held item to a framing key (e.g. `plank-oak` → `oak`), places the wall block with that key set, and consumes the item cost.
Placement picks the side from the half of the cell the player clicked, replacing `HorizontalOrientable` from the shape proposal.

**Adding layers: `SidingWallBlock.OnBlockInteractStart`, shift held.**
Held item matches an insulation entry and insulation is empty → fill it.
Otherwise matches an exterior entry and exterior is empty → fill it.
Order is enforced: no exterior before a frame; insulation is optional, but can't be added after the exterior.
That order is a guess at what feels right, and cheap to relax.

**Matching items to materials lives in the dictionary entry**, next to its existing `Drops`:
```json
"straw": { "Texture": "game:block/hay/normal-side", "Consumes": { "type": "item", "code": "game:drygrass", "quantity": 4 }, "Drops": [ ... ] }
```
Often `Consumes` and `Drops` will be the same stack; keep them separate only if that turns out not to be true, otherwise collapse to one field.

**Removing: breaking the block drops all built layers** (per `wall-layer-state`).
No per-layer removal in the prototype.

**No off-hand tool requirement in the prototype.** Roofing's hammer-in-off-hand is good flavour; it's also an extra failure mode to debug while the basics don't work yet.

## Alternatives considered
- **Craft finished walls in the grid, place like any block.** Simplest to build, but three independent material slots in a grid recipe means a recipe per combination — the explosion again, in recipe form. And it loses the "watch the wall go up" feel.
- **A GUI to pick three materials.** More discoverable, much more code, and doesn't consume items naturally.
- **A dedicated "wall frame" item instead of patching planks.** Adds a crafting step and an item for nothing; Roofing's patch-the-raw-material approach is lighter.

## Consequences & open questions
- Patching behaviors onto vanilla planks means right-click with planks in hand now places frames. Roofing already does this to the same item — if both mods are installed, which behavior wins? Probably needs a tool mode ("siding frame" vs "roof frame" vs plain placement) or a sneak modifier. Test with Roofing installed before release.
- Shift-right-click on a block is also vanilla's "place against" gesture. Check it doesn't place the held item next to the wall instead of adding the layer.
- Handbook page explaining the flow: needed before anyone else plays it, not for the prototype.
