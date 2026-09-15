# In-world build flow

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; `vsroofing_1.7.2` shipped `patches/items.json` and handbook guide text; no code yet

## Summary
Walls are built in place, layer by layer, the way Roofing builds roofs: right-click with a framing material to place a frame, shift-right-click it with infill to complete the wall, then optionally shift-right-click either face with a finish.
Each step consumes the held item and fills one key on the block entity.
The face you click is the face you finish — no menu, no modifier keys.

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
The wall always hugs the target cell's face nearest the player, so it appears directly in front of them.
That's orientation from player facing — the same thing `HorizontalOrientable` does in the shape proposal, just computed in our behavior.

**Adding layers: `SidingWallBlock.OnBlockInteractStart`, shift held.**
- Held item matches an infill entry, infill empty → fill the cavity. The wall is now complete and seals rooms (see decision 0003).
- Held item matches a finish entry, infill built → finish the face the player clicked. `BlockSelection.Face` says which face of the selection box was hit: the hugged direction → `front`, the opposite → `back`, an end or top face → nothing happens.

Order is frame → infill → finishes, and the two finishes in any order.
Roofing fixes its layers as 0, 1, 2 in sequence; that works for a roof, which has one visible side.
A wall has two, and the player has already told us which one they mean by where they're standing.

Worked through: a player outside the house places a frame and fills it; the wall hugs their side, so the finish they click next lands on `front` — the outside.
They walk indoors, click the wall, and the finish lands on `back` — the inside.
Building from inside works the same way in reverse, with no setting to change.

**Matching items to materials lives in the dictionary entry**, next to its existing `Drops`:
```json
"straw": { "Texture": "game:block/hay/normal-side", "Consumes": { "type": "item", "code": "game:drygrass", "quantity": 4 }, "Drops": [ ... ] }
```
Often `Consumes` and `Drops` will be the same stack; keep them separate only if that turns out not to be true, otherwise collapse to one field.

**Removing: breaking the block drops all built layers** (per decision 0003).
No per-layer removal in the prototype.

**Off-hand saw required, to tell siding apart from Roofing.**
Both mods hang a frame-placing behavior on vanilla planks, so "right-click with planks" alone is ambiguous with both installed.
Roofing's timber frames want a hammer in the off hand; Roofing's shipped assets never reference the saw.
So siding frames want a vanilla `saw` in the off hand: an existing item with its own model, texture, and recipes, and a tool that reads as "framing carpentry".
No saw → our behavior passes, and whatever else is on planks (Roofing, plain placement) handles the click.

**Frame type via tool modes: `wall` and `corner` for the prototype.**
Same mechanism as Roofing's standard/eave/ridge.
`corner` places the `cornerout` layout (see `wall-shape-and-collision`), without which a house built from outside has a walk-through gap at every corner and doesn't seal.
Window and door frames become further tool modes later (see the proposals index).

## Alternatives considered
- **Craft finished walls in the grid, place like any block.** Simplest to build, but four independent material slots in a grid recipe means a recipe per combination — the explosion again, in recipe form. And it loses the "watch the wall go up" feel.
- **A GUI to pick materials.** More discoverable, much more code, and doesn't consume items naturally.
- **Pick the finished face with a menu, or sprint/sneak modifiers.** Sneak already means "add a layer" (Roofing's convention), and Roofing uses ctrl-right-click for its base block, so modifiers collide with muscle memory from the mod we're imitating. A menu is an extra step for a choice the player already made by standing on one side of the wall.
- **Fixed layer order (front, then back) as Roofing does.** Forces the outside to be finished before the inside regardless of what the player wants, and still has to guess which side is outside.
- **A dedicated "wall frame" item instead of patching planks.** Adds a crafting step and an item for nothing; Roofing's patch-the-raw-material approach is lighter.
- **A custom "construction hammer" in the off hand** (a copy of the vanilla hammer, retextured later). Works as a discriminator just as well, but it's a new item with a recipe, lang entries, handbook entry, and an art debt, to get what the saw gives for free. Worth it only if the saw turns out to be claimed by another popular building mod.
- **A tool mode on planks to choose siding vs roofing.** Both mods' tool modes would be piled onto the same item; the list gets long and it's easy to place the wrong thing. The off-hand item is a physical, visible switch.

## Consequences & open questions
- **The saw may not fully resolve the Roofing clash.** Roofing's lang file has "Wrong item/tool in offhand." — so with planks in hand and a saw in the off hand, Roofing's behavior may run first, complain, and swallow the click before ours sees it. Which behavior runs first is its position in the planks' `behaviors` array. Test with Roofing installed; if it bites, our patch uses `"op": "add", "path": "/behaviors/0"` to put ours first (vanilla `plank.json` already has a `behaviors` array, so the path exists). Roofing's `addmerge` appends, so ours stays in front whichever mod patches first. A patch can only reorder here — it can't change Roofing's C# offhand check — and it doesn't need a `dependsOn` condition, since putting ours first is harmless without Roofing. If reordering isn't enough, a patch conditioned on `vsroofing` being installed is the next step, but only once we know what it would need to change.
- **Reaching the back face.** On a house's outer wall the back face is indoors, so finishing it means walking in. Natural for a house; awkward for a freestanding wall only if its far side is blocked. Watch for it in playtest.
- **Auto-connecting corners.** Picking the corner piece from neighbouring walls would save a tool-mode switch per corner, but it makes collision, retention, and mesh all depend on neighbours and need refreshing when a neighbour changes. Manual first; auto-connect if players find it tedious.
- **Clicking an end or top face does nothing.** Silent failure is confusing; a short error message ("click the wall's face") probably belongs here.
- Shift-right-click on a block is also vanilla's "place against" gesture. Check it doesn't place the held item next to the wall instead of adding the layer.
- Handbook page explaining the flow: needed before anyone else plays it, not for the prototype.
