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

**Off-hand saw required, to tell siding apart from Roofing.**
Both mods hang a frame-placing behavior on vanilla planks, so "right-click with planks" alone is ambiguous with both installed.
Roofing's timber frames want a hammer in the off hand; Roofing's shipped assets never reference the saw.
So siding frames want a vanilla `saw` in the off hand: an existing item with its own model, texture, and recipes, and a tool that reads as "framing carpentry".
No saw → our behavior passes, and whatever else is on planks (Roofing, plain placement) handles the click.

**Frame type via tool modes: `wall` for the prototype.**
Same mechanism as Roofing's standard/eave/ridge.
Window and door frames become further tool modes later (see the proposals index) — the prototype registers the tool-mode list with one entry so adding them isn't a restructure.

## Alternatives considered
- **Craft finished walls in the grid, place like any block.** Simplest to build, but three independent material slots in a grid recipe means a recipe per combination — the explosion again, in recipe form. And it loses the "watch the wall go up" feel.
- **A GUI to pick three materials.** More discoverable, much more code, and doesn't consume items naturally.
- **A dedicated "wall frame" item instead of patching planks.** Adds a crafting step and an item for nothing; Roofing's patch-the-raw-material approach is lighter.
- **A custom "construction hammer" in the off hand** (a copy of the vanilla hammer, retextured later). Works as a discriminator just as well, but it's a new item with a recipe, lang entries, handbook entry, and an art debt, to get what the saw gives for free. Worth it only if the saw turns out to be claimed by another popular building mod.
- **A tool mode on planks to choose siding vs roofing.** Both mods' tool modes would be piled onto the same item; the list gets long and it's easy to place the wrong thing. The off-hand item is a physical, visible switch.

## Consequences & open questions
- **The saw may not fully resolve the Roofing clash.** Roofing's lang file has "Wrong item/tool in offhand." — so with planks in hand and a saw in the off hand, Roofing's behavior may run first, complain, and swallow the click before ours sees it. Which behavior runs first depends on patch order. Now I'm guessing it's load order of the mods. Test with Roofing installed; if it bites, our patch can insert our behavior at the front of the planks' `behaviors` list instead of appending.
- Shift-right-click on a block is also vanilla's "place against" gesture. Check it doesn't place the held item next to the wall instead of adding the layer.
- Handbook page explaining the flow: needed before anyone else plays it, not for the prototype.
