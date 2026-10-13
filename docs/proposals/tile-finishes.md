# Tile finishes

- Status: Draft
- Created: 2026-10-12
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-247702 and #cmt-247707; the scouting session of 2026-10-12 on `main` at `4cc6928`; vanilla 1.22.7 `itemtypes/resource/tile-clay-fired.json`, `tile-stone.json`, `blocktypes/clay/tile-block.json`, `recipes/grid/tile-block.json` and the textures under `block/clay/tile/2x2` and `block/stone/tiles`; `SidingWallBlock.OnBlockInteractStart`, `OnBlockBroken`, `ResolveFinishFace` and `UnrotatedDeckBoxes`; `SidingWallEntity.SelectiveElements`, `MeshState` and `CacheKey`; `SidingWallTexSource.ResolveTexture`; `GuestWalls.Decode`; `WallShapeGen.WallElements`, `CornerOutElements`, `ClipRun` and `DeckGroups`; decisions 0013, 0019, 0043, 0051, 0068, 0069 and 0071; issue #108; nothing built, nothing played, and no vanilla code decompiled for it

## Summary
A player asked for ceramic tiles as a finish.
The proposal lays vanilla's fired tiles on a face one at a time, four to a face, each slot taking any colour, so a wall, a floor or a deck can carry a mosaic.

## Context
**The request.**
"Is it possible to add ceramic tiles as finish as well?", and again: "Pls dont forget of ceramic tiles for finish, that would be awersome".
Nothing more was said, so "one tile at a time" is the author's reading of it and not the player's words.

**What vanilla has.**
One item, `claytile-fired-{pattern}-{color}`, in ten plain colours plus a red `center` and a red `circle`, and `tile-stone-travertine` beside it: thirteen kinds.
Eight of one kind round a mortar make four `tile-2x2-…` cubes.
The item is a plate 7 by 7 by 1, and it lies on the ground through `GroundStorable`, on a floor only.
Vanilla has no tile that goes on a wall, and none among its face decor blocks.

**`2x2` is a count, not a size.**
The block's name means two tiles by two on a block face, so one tile is 8 voxels wide, half a face.
Each texture is 32 by 32 and holds all four tiles with their grout, so one quarter of a texture is one tile.
The `center` texture has its blue diamond in the middle, a quarter of it on each tile.

**What a face holds today.**
One finish key and one style: `Front` and `FrontStyle`, and the same for `SecondFront`, `Back`, the deck's two faces and the floor's two.
The key is read for the mesh, the texture, the tooltip, the drops, the peel order, the layer's sound and whether fire catches.
A finish is one group of boxes in the shape file, chosen by name (`SelectiveElements`), with one texture slot for the whole face.

## Design
**A tiled face is one finish, with a list beside it.**
The face's key becomes `tiles`, one new `Finishes` entry of `BlockMaterial: "Ceramic"` with no `Consumes` and no `Drops`.
Beside the key goes a list of slots, each empty or holding a tile kind, stored as one string per face the way a style is.
Everything that reads the key reads `tiles` and needs no change: the face is finished, it is ceramic, it does not burn (decision 0043), and no other finish goes on it.
Four places read the list: the mesh, the texture, the click and the break.

**A tile kind is a finish entry.**
One family, `tile-{pattern}-{color}`, matched on both placeholders (decision 0068), and one explicit entry for travertine.
Each consumes one item and drops one, and is marked `Tile: true`, which is what sends a click to a slot and keeps the kind from going on as a whole face.
Name, texture, cost and drop then come through the code every finish uses.

**The slots are the cell's grid.**
A face is cut at 8 on each of its two axes, measured in the cell and not along the face.
A face that covers a whole side of the cell has four whole slots.
A face that covers less draws what it covers of each slot, as a tile cut at a wall is.
A wall's deck is 12 deep, so its two slots against the wall are half tiles.
A corner's second front is 15 long, and its back is two legs, 12 and 13 long, with four slots on each, so a corner's back holds eight.
Cut this way the grout lines meet from one cell to the next, round a corner and from a wall onto its deck.
A cut tile costs a whole tile.

**Shapes.**
Each face gains one group per slot: the plain slab cut in four, in `WallShapeGen`, with its outward face mapped by position so that it samples its own quarter of the texture.
Each group has a texture slot of its own, since each may hold a different kind.
Four `center` tiles then draw the diamond without anything matching them up.
A corner's groups come from `ClipRun`, which already cuts its back shakes to each leg.
`DeckGroups` cuts the floor's groups to the deck, so the deck's tiles should come from the floor's.

**Placing.**
A fired tile clicked on a filled wall's face goes into the slot under the crosshair, found from `HitPosition`, which the block already reads.
The first tile sets the face's key.
A slot that is full, or a face with any other finish on it, gives the already-finished message.
A bare face behind an empty slot shows the infill.

**Breaking.**
A break takes the tile under the crosshair and drops it, and the last one clears the key.
An empty slot under the crosshair takes the first tile in the list, so infill never comes out from behind a tile (decision 0013's order).
A break with no player takes the block, and every tile drops.

**Which slot is which.**
A wall is tesselated turned by its side, and a back face is seen from the other side to its front.
One table of side, face and hit position to slot, pinned by a test for every rotation, as decision 0019 pinned "left along a run".

**Where tiles do not go.**
A diagonal's panel is a staircase of boxes and takes no tile.
A glazed cell's finish is a rim round the glass (decision 0071) and takes no tile.
A tile on a glazed cell gives `build-glazed`, as any finish on a glazed diagonal does today.
A plain diagonal takes every other finish, so a tile there needs a message of its own.

**Floors and decks are in the first cut.**
A floor's top and underside take tiles as a wall's faces do (decision 0051), and floors are where vanilla's own tiles go.

**Corners are in the first cut.**
A tiled room with bare corners would look unfinished.
The in-place corner upgrade (decision 0069) moves a face's list with its key, and a wall's back becomes one leg's half of the corner's back.

## Alternatives considered
- **One click tiles the whole face.** A family entry in `materials.json` and no code, as brick is. A pattern is then one cell coarse, which is what vanilla's tile blocks already give, and the request was read as wanting more than that.
- **Free placement, a tile at any offset.** Tiles would overlap and leave slivers, a face would hold a list of positions, and no fixed set of shape groups can draw it, so the mesh would be built by hand, which nothing in the mod does.
- **Chips 2 by 2, sixty-four to a face.** A finer mosaic. A chip shows 4 by 4 texels, so the `center` and `circle` patterns are lost, and a face is sixty-four clicks. Left until players ask; the list of slots grows to it without a migration.
- **No `tiles` key, and a face is finished when its list is not empty.** Every reader of a face's key would need the second test: the tooltip, the peel order, the sounds, fire, the deck's ledge. The key keeps that to four places.
- **Raised tiles with a recessed joint**, as brick has. More boxes for a joint the texture already paints. Flat was agreed.
- **Consuming the tile block.** Eight tiles and a mortar make four blocks, so a block is two tiles, and a slot is one.

## Consequences & open questions
- Whether the server's break carries the hit position is not confirmed. The face reaches `OnBlockBroken` through the `BreakBlock` event (`ServerBreakSelection`), and the deck test reads `SelectionBoxIndex` from the same selection, but `HitPosition` on it has not been read in the decompile. Without it a break takes the first tile.
- Seven new strings: three faces and the deck's two on a wall, two on a floor. They go into `MeshState` and `CacheKey`. Each new pattern on a face is a cached mesh that `ObjectCacheUtil` keeps, as every combination of wood and style is today; fourteen states to a slot is 38,416 for a face of four at the most, and a build uses few.
- The cell's alternate goes into the cache key only when a face key's texture varies (`AnyVaries`). The slots' kinds must be read there too, or every cell draws the same one of a tile's four variants.
- A hosted wall's record is its entity's tree (`GuestWalls`), so it should keep its tiles with no change there. Not tried.
- A deck draws its ledge only while the wall's room side is bare. With the top row of that side untiled, the slot beside the deck is open for 4 voxels of height and nothing fills it. Left as it falls unless play shows it.
- A diagonal's deck is eight strips. `DeckGroups` may cut the tiles to them as it cuts the floor's other groups; if it does, the deck takes tiles while its panel does not, and whether that reads as wrong is open.
- Decision 0075 (issue #108) changes which face a finish click lands on. A tile clicked on a finished face follows its rule: the finish goes on the face behind it while that face is bare.
- One PR, in the order shapes and their tests, then state, mesh and texture, then click, break and drops with an Atlas scenario, then corners and the upgrade, then the mod page and guide text. It goes into 1.4.0 if it is merged before the gates clear, and the release does not wait for it.
- Four clicks tile a face, and there is no fill shortcut.
- The texture's variants (`red1` to `red4`) have not been compared at their quarters; if a variant moves the grout, a cell's four tiles still share one variant, and neighbours may not meet.
