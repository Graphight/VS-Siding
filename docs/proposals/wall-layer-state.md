# Wall layer state

- Status: Draft
- Created: 2026-09-13
- Reflects: planning session on `prototype-proposals`; decision 0001; no code yet

## Summary
A placed wall remembers which framing, insulation, and exterior it has as three string keys on a block entity.
Each key looks up an entry in the block's `attributes.Framings` / `Insulations` / `Exteriors` dictionaries.

## Context
Decision 0001 settled that materials are dictionary keys, not variants, but left open *where a placed block stores its keys*.
Something per-position has to hold them; the block ID can't, because there's one block ID per side.

Vanilla has the same shape of problem in chiseled blocks: one block class, the materials and voxels stored on a block entity, mesh built from that state.
Roofing names its entity `AutoRoofEntity`, which points the same way.
We don't need to confirm Roofing's internals to proceed — the vanilla pattern is public and sufficient.

## Design

**`vssiding.SidingWallEntity` holds three nullable strings: `framing`, `insulation`, `exterior`.**
`null` means that layer isn't built yet.
Saved and synced through `ToTreeAttributes` / `FromTreeAttributes`, the standard block entity path, so save files and client sync come for free.

**Keys are validated on load, not trusted.**
If a key no longer exists in the dictionary (material removed, compat mod uninstalled), the layer renders as missing rather than crashing the chunk.
The key is kept, so reinstalling the mod restores the wall.

**Dictionary entry shape, minimal for the prototype:**
```json
"Framings": {
  "oak": { "DisplayName": "vssiding:framing-oak", "Texture": "game:block/wood/planks/oak1", "Drops": [ { "type": "item", "code": "game:plank-oak", "quantity": 2 } ] }
}
```
`Texture` and `Drops` only.
Roofing's entries also carry `Shape`, `Sounds`, `BlockMaterial` — add each when something needs it, not before.

**Starter set, all vanilla textures:** framing `oak`; insulation `straw` (hay), `wattle`; exterior `planks`, `daub`.
Enough to see two visibly different combinations.

**Drops: breaking a wall returns the `Drops` of every built layer.**
Material goes back to the player, matching Roofing's "materials are built into the frame" model.
The alternative (drop a wall item carrying all three keys) is covered below.

## Alternatives considered
- **Store keys on the block ID via variants.** Rejected by decision 0001.
- **Drop a single wall item with the three keys in its itemstack attributes.** Lets you pick up and move a finished wall, and makes creative-inventory walls easy. But it means walls exist as items, need their own icons per combination, and duplicate the "place a finished wall" path alongside the in-world build flow. Revisit if players ask to move walls.
- **Integer indices instead of string keys.** Smaller saves, but a compat mod inserting a material shifts every index and silently re-materials existing walls. Strings survive that.

## Consequences & open questions
- Every wall is a block entity. Block entities are heavier than plain blocks; a large house is hundreds of them. Vanilla chiseled blocks prove this scales acceptably, but it's worth a look at chunk load time once a big test build exists.
- Pick-block (middle click) on a wall has nothing obvious to give back without a wall item. Probably the framing's item. Low priority.
- Should insulation be optional (frame + exterior, nothing between)? Model allows it already (`null`); gameplay question for `in-world-build-flow`.
