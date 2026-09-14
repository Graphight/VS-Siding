# Wall layer state

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; decision 0001; vanilla `RoomRegistry.cs` / `Block.GetRetention`; no code yet

## Summary
A placed wall remembers four string keys on a block entity: `framing` and `insulation` (the cavity, which decides whether and how the wall seals a room), and `front` and `back` (two purely cosmetic face finishes).
Each key looks up an entry in the block's `attributes.Framings` / `Insulations` / `Finishes` dictionaries.

## Context
Decision 0001 settled that materials are dictionary keys, not variants, but left open *where a placed block stores its keys*.
Something per-position has to hold them; the block ID can't, because there's one block ID per side.

Vanilla has the same shape of problem in chiseled blocks: one block class, the materials and voxels stored on a block entity, mesh built from that state.
Roofing names its entity `AutoRoofEntity`, which points the same way.
We don't need to confirm Roofing's internals to proceed — the vanilla pattern is public and sufficient.

The second question is which layers actually *do* something.
Vintage Story players love half-timbering: exposed frame, infill between, no cladding.
That has to be a finished, room-sealing wall, not a half-built one.
Roofing already works this way — frame plus the first material layer is a complete roof, and later layers are looks — so players will expect the same.

## Design

**`vssiding.SidingWallEntity` holds four nullable strings: `framing`, `insulation`, `front`, `back`.**
`null` means that part isn't built yet.
Saved and synced through `ToTreeAttributes` / `FromTreeAttributes`, the standard block entity path, so save files and client sync come for free.

**`front` and `back` are named by geometry, not by "exterior" and "interior".**
`front` is the face on the cell boundary the wall hugs — the side the player stood on when placing it (see `in-world-build-flow`).
`back` is the inset face.
The mod can't know which side of a wall is the inside of a house, and doesn't need to: the player finishes whichever face they click.

**The cavity is function, the faces are looks.**
- `framing` + `insulation` both built → the wall seals a room. `SidingWallBlock.GetRetention` (see `wall-shape-and-collision`) returns 0 on every face until then.
- The retention sign follows the insulation's `BlockMaterial` the way vanilla does for solid blocks: stone/soil/ceramic/ore is a cooling wall (cellars), anything else isn't.
- `front` and `back` never affect retention. Brick outside a warm house, planks over a stone-packed cellar, or no finish at all — the room behaves the same.

**Both faces share one `Finishes` dictionary.**
Planks, plaster, and daub make sense on either side.
A face-specific material (wallpaper inside, weatherboard outside) can add an optional restriction field later; not needed until such a material exists.
Decision 0001 used `Exteriors` as the example name; `Finishes` is the same idea now that there are two faces.

**Keys are validated on load, not trusted.**
If a key no longer exists in its dictionary (material removed, compat mod uninstalled), that part renders as missing rather than crashing the chunk.
The key is kept, so reinstalling the mod restores the wall.

**Dictionary entry shape, minimal for the prototype:**
```json
"Framings": {
  "oak": { "DisplayName": "vssiding:framing-oak", "Texture": "game:block/wood/planks/oak1", "Drops": [ { "type": "item", "code": "game:plank-oak", "quantity": 2 } ] }
}
```
`Texture` and `Drops` only, plus `BlockMaterial` on insulation entries for the retention sign.
Roofing's entries also carry `Shape` and `Sounds` — add each when something needs it, not before.

**Starter set, all vanilla textures:** framing `oak`; insulation `wattle` (the half-timbering test), `straw` (hay), and one cooling fill (stone rubble or clay) for the cellar test; finishes `planks`, `daub`.

**Drops: breaking a wall returns the `Drops` of every built part.**
Material goes back to the player, matching Roofing's "materials are built into the frame" model.

## Alternatives considered
- **Store keys on the block ID via variants.** Rejected by decision 0001.
- **The exterior decides sealing and cooling.** Ties the look of a house to whether it can be a cellar; a player wanting a brick façade on a warm room couldn't have it. And it makes half-timbering an unfinished wall.
- **Insulation doubles as the interior face.** Same problem turned inward: plaster inside and a cellar become mutually exclusive. The cavity sits between the studs in a real wall, not on its surface.
- **`exterior`/`interior` keys.** The mod would have to guess which side of a wall is indoors. Geometry-named faces don't guess.
- **Drop a single wall item with all keys in its itemstack attributes.** Lets you pick up and move a finished wall. But walls then exist as items with icons per combination, and a second "place a finished wall" path next to the in-world build flow. Revisit if players ask to move walls.
- **Integer indices instead of string keys.** Smaller saves, but a compat mod inserting a material shifts every index and silently re-materials existing walls. Strings survive that.

## Consequences & open questions
- Every wall is a block entity. A large house is hundreds of them. Vanilla chiseled blocks suggest this scales acceptably; look at chunk load time once a big test build exists.
- Pick-block (middle click) on a wall has nothing obvious to give back without a wall item. Probably the framing's item. Low priority.
- "Insulation" undersells a layer that is now also the visible infill of a half-timbered wall. "Infill" is the builder's word for it. Naming only — decide before code, since the key is saved into worlds.
