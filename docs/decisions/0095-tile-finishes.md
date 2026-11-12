# 0095 — Tile finishes

- Status: Accepted
- Created: 2026-11-11
- Reflects: branch `tile-finishes`; the proposal of 2026-10-12 (mod page comments #cmt-247702 and #cmt-247707); `SidingWallBlock.TileSlot`/`TilesToLay`/`TileToBreak`/`FloorTileSlot`; `SidingWallEntity.TileSlots`/`TurnTiles`; `SidingWallTexSource.ResolveTexture`/`AtlasPosition`/`RollsRight`; `TileFill`; `WallShapeGen`; `config/materials.json`; vanilla 1.22 `claytile-fired-*` and `tile-stone-travertine`; decisions 0013, 0043, 0068, 0069, 0071, 0074, 0075, 0077, 0082, 0085 and 0090

## Summary
Vanilla's fired tiles lay on a face one at a time, four slots to a face, each slot taking any tile, so a wall, corner, post, floor or deck carries a mosaic.

## Context
A player asked for ceramic tiles as a finish.
Vanilla has thirteen kinds: `claytile-fired-{pattern}-{color}` and `tile-stone-travertine`.
The block `tile-2x2-…` means two tiles by two on a face, so one tile is 8 voxels wide, and a 32-pixel texture holds four tiles with their grout.
A face already holds one finish key and one style string, read by the mesh, texture, tooltip, drops, peel order, sound and fire.

## Design
**A tiled face is the finish `tiles`.**
It is one `Finishes` entry with `BlockMaterial: "Ceramic"` and no `Consumes` or `Drops`, so every reader of a face's key (finished, peel order, sound, fire in 0043, wattle light in 0074, far face in 0075) works unchanged.

**The slots live in the style string.**
A face's existing style (`FrontStyle`, `SecondFrontStyle`, `BackStyle`, `SecondBackStyle`, the deck's two and the floor's two) holds four comma-separated tile keys, empty for an empty slot, e.g. `tile-plain-blue,,tile-center-red,`.
No new field is added, so saving, `MeshState`, `CacheKey`, `GuestWalls`, the corner upgrade (0069, 0090) and `SharesLegacyBack` carry the slots with the key.
`SidingWallEntity.TileSlots` splits the string.

**A tile kind is a finish entry marked `Tile: true`.**
One family for `game:claytile-fired-*-*` with `variant: ["pattern","color"]` (0068), and one entry for `tile-stone-travertine`.
Each consumes one item and drops one.
Texture comes from vanilla's `block/clay/tile/2x2/{pattern}/{color}*` and `block/stone/tiles/travertine*`, and the name from vanilla's item lang key, so the tooltip reads "3 Earthy brown ceramic tile (plain)".
`MatchConsumes` never returns a `Tile` entry as a whole-face finish.

**Slots sit on the cell's grid.**
A face is cut at 8 on its two axes, measured in the cell.
A face that covers part of a slot draws the part it covers, so grout lines meet across cells, round corners, onto decks and onto caps.
A cut tile costs a whole tile, so tiles ignore `LayerParts` and `Share` for cost and refund.
Slot index is `row * 2 + col`, row 0 the lower half and col 0 the lower coordinate along the face in the shape's unrotated frame.
A floor and a deck are tesselated unrotated, so their slots are world-grid quarters.
One `TileSlot` table maps side, face and hit position to a slot, pinned by a test for every rotation.

**Shapes.**
Each slot is its own group, `{face}-tile{n}`, with its own texture slot `{face}tile{n}`, cut from the plain finish slab with `ClippedFrom` so each piece samples its own quarter of the texture.
The groups are left out of the rim pieces.
Coverage is a wall's front and back, all four faces of a corner (0090), a post's four faces (0082, two slots visible on each), a floor's top and underside, and the deck of a wall, corner, post and diagonal (cut to its strips).
A post's face is 4 voxels wide, half a tile, so its tiles squeeze a whole tile across it (`TilePieces`' squeezed face): grout on both sides, at two texels a voxel across.
A diagonal's panel takes no tile.
On a corner, `frontedge` takes the second front's tile in its slot, or the front's while the second front is bare.
A capped deck (0085) draws the deck top's tiles over the wall's top, and an empty deck slot leaves the wall's own top showing.
A corner upgrade (0069) swaps each tiled face's columns with `TurnTiles`, so the tiles stay where they were.

**The joint.**
Vanilla's clay tile textures paint each joint as a shadow texel and a grout texel, wholly left of the tile's edge across (columns 14-15 and 30-31) and straddling it down (rows 15|16 and 31|0).
Cut on the 16-texel grid, each tile then showed its whole joint on the right and none on the left.
`SidingWallTexSource.AtlasPosition` inserts each clay tile texture rolled one texel right (`RollsRight`, `RollRight`), under its own atlas name, so every tile shows a texel of joint on each side and the `center` diamond sits where four tiles meet.
Travertine's joint already straddles the edge both ways, so it is not rolled.
All 48 clay textures, every variant, share the layout; a test reads them from the game install.

**Placing.**
A tile clicked on a face goes into the slot under the crosshair, and the first one sets the key to `tiles`.
A full slot gives the already-finished message, with no swap and no jump to the far face.
A face finished with anything else follows 0075: the tile goes on the bare face behind it, in the slot under the projected hit.
A glazed face (0071) and a diagonal's panel each refuse with a message of their own.
Tiles as decor on plain vanilla blocks (0077) are out of scope.

**Sneak-click fills.**
A sneak-click fills every empty slot on that face in slot order, as many as the player holds, with no message when they run out.
Vanilla's `GroundStorable` takes a sneak-click on an up face before the block sees it, so `TileFill` goes ahead of it and hands that click to a Siding wall or floor while the build signal is held.
`TileFill` matches tile items by hard-coded code patterns, not by `Tile: true`, because finish families are expanded on the server and not on the client.
The server's held-item path tests range alone, so `TileFill` makes the land claim test (`Use`) that a click gets on the block-use path, and a refused fill lays nothing and leaves no pile.

**Breaking.**
A break takes the tile under the crosshair and drops it.
An empty slot there takes the first tile in the list, so infill never comes out from behind a tile (0013).
The last tile clears the key and the list.
A break with no player drops every tile.
The server's break selection carries `HitPosition`, built from the packet in `ServerSystemBlockSimulation.TryModifyBlockInWorld`.

**Tooltip.**
A tiled face lists its kinds with counts, and leaves empty slots out.

**Wattle.**
One tile counts as a finished face and stops the light, because `IsFinished` reads the key.

## Alternatives considered
- **One click tiles the whole face**, as brick does, from a family entry alone. A pattern is then one cell coarse, which vanilla's tile blocks already give.
- **Free placement at any offset.** Tiles would overlap and leave slivers, and no fixed set of shape groups can draw it.
- **Sixty-four chips to a face.** A chip shows 4 by 4 texels, which loses the `center` and `circle` patterns. The slot list can grow to it without a migration.
- **No `tiles` key, a face finished when its list is not empty.** Every reader of the key would need the second test.
- **Raised tiles with a recessed joint.** The texture already paints the joint.
- **Consuming the tile block.** A block is two tiles, and a slot is one.
- **Mirroring the left column's uv** in place of the roll. The joints then alternate: two grout texels down a cell's middle and two shadow texels where cells meet, with no grey line there at all on dark tiles.
- **Shipping shifted copies of vanilla's tile textures.** Simpler code, but it redistributes edited vanilla art.
- **A post face showing the half of a tile the grid gives it, or a tile's middle slice.** The grid half was built first and looked odd in play; the squeezed whole tile was chosen over the slice.

## Consequences & open questions
- A tile item added by another mod is not matched by `TileFill`, and takes `GroundStorable`'s sneak-click as vanilla does.
- Each tile pattern on a face is a cached mesh. A cell's alternate enters the cache key when a slot's texture varies, so neighbouring cells do not all draw the same variant.
- A deck draws its ledge only while the room side is bare, and a tiled room side counts as finished.
- A clay tile texture from another mod's own path is not rolled.
