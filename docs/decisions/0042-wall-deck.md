# 0042 — Wall deck

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `wall-deck` (PR #52); `SidingWallEntity.Deck`; `SidingWallBlock.AddDeckBox`/`GetSelectionBoxes`/`ComputeDeckRetention`/`PeelLayer`/`OnBlockInteractStart`; `SidingModSystem.DeckedCellPostfix`/`HostChangePrefix`; `SidingModePicker.Rows`; decompiled `RoomRegistry.FindRoomForPosition` (1.22.2); decisions 0002, 0008, 0013, 0020, 0026, 0035, 0040; a player report; played

## Summary
A player asked how to floor an upper storey right up to the wall, and reports that multi-storey walls look wrong.
A wall's cell is a 4/16 panel on one face and 12/16 of open air (decision 0002), so an upper floor laid in the room cells stops at the wall's cell and leaves a 12/16 slot running the length of every wall, straight down to the storey below.
This adds an opt-in deck: a 4/16 layer flush with the top of the wall's cell, filling its open part, built from the held plank and toggled from its own picker row.

## Context
**The slot.**
Take a ground storey of walls at `y=1..3` and an upper floor of planks at `y=4`.
The wall column carries on at `y=4` as another wall block, so the floor can only go in the room cells beside it.
Between the floor's edge and the panel is the wall cell's open 12/16, and nothing in it collides: `PanelCollisionBoxes` gives the full panel for a filled wall and posts and a top plate for a bare frame (0008), both hugging the panel face.
A player is 0.6 wide, so they fit through a 0.75 slot and drop a storey.
The player confirmed this is the problem they saw.

**How real houses do it.**
A floor never stops at the wall; it passes through the wall line.
In platform framing the lower wall ends in a top plate, the joists and a rim joist sit on it, the floorboards run to the outer edge, and the upper wall stands on the floor.
In a traditional timber frame a girt runs through the wall at floor height and the joists hang off it.
Either way there is a horizontal member filling the wall's thickness at floor height, and the deck is that member squeezed into the wall's cell.

**The workaround today.**
A course of full blocks at floor height in the wall column gives the floor something solid to butt against, but it breaks the siding's finish for one course.

## Design
**A deck layer on the wall.**
A new optional layer, `Deck`, on `SidingWallEntity`, holding a `Framings` key.
It draws one box 4/16 thick, flush with the top of the cell, filling the open 12/16 (unrotated x 4..16, y 12..16); a `cornerout` fills its 12×12 open square the same way.
Its top is at the same height as a plank block's, so a floor of full blocks, and the later thin floor of `thin-floor-framing`, meets it flush.
Finishes live inside the panel's own 4/16 of depth, so the outside finish is untouched and the siding runs unbroken from ground to eaves.

**Opt-in, so a stairwell keeps its gap.**
A wall is unchanged unless the player asks for a deck.
A staircase against a wall wants the open cell, and leaving the deck off is all it takes.
Auto-detecting a floor beside the wall would get the stairwell wrong, and a wall that rewrites itself when a neighbour changes is the trap the parked `auto-corners` already rejected.

**Its own picker row.**
`("vssidingDeck", ["deck"], AllowNone: true)`: one lit-or-unlit toggle beside framing.
Adding `wall+deck` and `corner+deck` to the framing row instead would double it for every future layout.
`FinishChoices` skips the row; it is not a finish style.

**Upgrade in place, like a corner.**
With the deck lit, a saw-and-plank click on a side face of a framed wall with no deck adds one, whatever it is filled or finished with (decision 0026 is the precedent, but a deck is its own layer, so there is no reason to limit it to bare frames).
That reaches houses already standing, which is where the report came from.
This branch runs ahead of finishing, so it also settles the clash with "planks on the room side finish the back face".
The top face is left alone, so it still stacks the next course, and a new frame placed with the deck lit gets its deck in the same click.
The deck costs the held plank's `Framings` entry `Consumes`, the same as a frame.

**Collision, retention, attachment.**
The deck box joins `PanelCollisionBoxes` for frame-only and filled walls alike.
It joins `GetSelectionBoxes` too: `wall.json`'s `collisionSelectionBoxesbytype` makes the selection box the panel alone, and play found that an untargetable deck let a break from below pass through it and peel whatever wall lay beyond.
A deck seals its cell's `UP` face (`GetRetention`) and holds things placed on top (`CanAttachBlockAt`), so rugs and torches work on it.
Vanilla's room walk does ask it: `RoomRegistry.FindRoomForPosition` checks every face of the current cell and the opposite face of its neighbour, so a deck seals the room above and the room below.

**Breaking and hosting.**
The deck peels first on a room-side or top hit, before a back finish (0013's one-layer-per-break order), and drops its framing material.
A decked cell refuses every placement into it: the furniture would sit where the deck is, and the guest store would need a deck field for no real build.
The hosting click (`TryHost`, 0035) is swallowed with an error; returning false there would hand the click to vanilla, which hosts anyway.
Every other path goes through `CanPlaceBlock`, where `IsReplacableBy` says yes to any hostable block with no position to see a deck, so a postfix on every `CanPlaceBlock` override refuses a decked cell, and the ground-storage prefix skips one.
Anything that still gets past both drops the deck as items in `HostChangePrefix`: a deck never silently disappears.

## Alternatives considered
- **Deck heights to match plank and slab positions (`full`/`top`/`bottom`).** The first draft. Fixing one flush-top 4/16 height matches the planned thin floor and drops a choice nobody needs.
- **Guest-hosting a floor block in the wall's cell (0035).** `IsHostable` refuses any block with a solid side, and a guest is drawn shifted into the open part, so a plank block would overlap the panel.
- **Auto-detecting a floor in the room cell.** Wrong for stairwells; see above.
- **Deck as framing-row options.** Doubles the row for every layout; see above.
- **Dropping the deck when furniture is placed into its cell.** The first build did this for every path but the click. Play found a chest placed under a deck removed it, and a deck the player built on purpose should not go without a word.
- **Documenting the sill-beam workaround and nothing else.** Free, but it breaks the outside finish, and the player asked for floor right up to the wall.

## Consequences & open questions
- The deck is the first layer that is not in the wall's plane; tree attributes, the tooltip (0030) and `GetBlockInfo` all grow a line.
- The 12/16 under a deck stays open, a small pocket along the wall seen only from the storey below.
- Nothing hangs from a deck's underside: it sits inside the wall's own cell. Hanging from a ceiling is `hanging-under-thin-floors`' problem.
- Snow, spawns and liquids on the deck's top face (0020's other rows) stay false until play says otherwise.
- A torch on a decked wall's inner face is refused along with furniture, since it would hang under the deck.
- Stairs still cannot reach the wall, deck or not; see the `stairs-against-walls` proposal.
- Played: two storeys with decks seal the upper room, the in-place upgrade, breaking from below, stacking a decked course, and both hosting refusals.

