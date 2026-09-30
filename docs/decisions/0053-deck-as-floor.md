# 0053 — Deck as floor

- Status: Accepted
- Created: 2026-09-30
- Reflects: branch `deck-as-floor`; `WallShapeGen` deck groups; `SidingWallEntity` deck layers and `LegacyDeck`; `SidingWallBlock.IsDeckHit`/`LayerDeck`/`DeckPeelLayer`/`PeelAt`/`ComputeDeckRetention`; `SidingFloorBlock.Joins`/`DeckReaches`; `SidingModSystem.HostChangePrefix`; decisions 0013, 0019, 0042, 0050, 0051; not yet played

## Summary
A deck (decision 0042) was bare framing timber, so a floor run up to a wall stopped 12/16 short of it: floorboards and a ceiling on the floor, raw plank on the deck.
This gives the deck the floor's layers: joists from the held plank, then its own infill, a top finish and an underside finish, drawn from the floor's own shape clipped to the deck's area.

## Context
Decision 0050 built a floor out of the wall's layers laid flat, and called the deck its rim joist.
Decision 0051 gave the floor styles, a lath ceiling and glass.
Beside that floor the deck was still one solid slab of framing on both faces.
Every floor's joists run north-south three to a cell, at the same x in every cell, so a deck drawn from the same table lines its joists up with the floor beside it.

This changes one part of decision 0042.
A deck no longer seals its cell's UP face by existing: a new bare deck seals nothing, and only framing plus infill does, as on a floor.
The rest of 0042 stands: the deck is still its own selection box, toggled from its own picker row.

## Design
**The floor's shape, trimmed to the deck.**
`WallShapeGen` clips the floor's elements (joists, rims, infill, finishes, lath and the glazing bezel) to the deck's area in world space, using the same rotation as `SidingWallBlock.BuildDeckBoxes`, and writes them into `wall.json` and `cornerout.json` as `deck-{side}-{name}` groups on new slots `deck`, `deckinfill`, `deckfront` and `deckback`.
The old solid `deck` element is gone.
One set is written per side because the wall mesh turns with its side and the deck's joists must not: `SidingWallEntity` tesselates the deck's groups as a second pass, unrotated, picked by `SidingFloorEntity.SelectiveElements` and prefixed with the side.
`SidingWallTexSource` maps the new slots.
A clipped face keeps sampling the texture where it sits on the floor, so its grain continues the floor beside it rather than restarting at the clip.
This is pinned by a test against the game's `ModelCubeUtilExt.AddFace` uv mapping, not against the generator's own arithmetic.

**Deck layers on the wall entity.**
`SidingWallEntity` gains `DeckInfill`, `DeckFront`, `DeckBack`, `DeckFrontStyle` and `DeckBackStyle`, keyed into the same dictionaries as a floor's; `Deck` stays the framing.
They are tree attributes, part of `MeshState` and `CacheKey`.
The styles are per face, as on a floor (decision 0051), so a deck's board direction can match the floor beside it; the proposal had left them out.

**Joins.**
A floor's rims drop where a framed floor continues (decision 0050), and a deck does the same.
A deck's north and south rims drop against a framed floor or a deck whose area reaches the shared edge, meaning a wall that does not claim that face (`ClaimsFace`).
A glazed deck's bezel drops on all four sides the same way.
`SidingFloorBlock.Joins` counts such a deck with `DeckReaches`, so a floor next to a deck drops its rim against it, and each side marks the other's horizontal neighbours dirty when its infill changes.

**Glass.**
A glass deck draws the floor's bezelled pane, clipped, in the transparent pass by the wall's `SetRenderPass` split.
It merges with glazed floors and glazed decks beside it into one pane, as glazed floors do among themselves (decisions 0019, 0051).

**Which layer a click means.**
Decision 0042 already made the deck its own selection box, so `SelectionBoxIndex` tells a deck hit from a panel hit (`IsDeckHit`).
A deck hit layers the deck the way a click layers a floor (`LayerDeck`, after `SidingFloorBlock.OnBlockInteractStart`): infill on any face, then a finish plus style on the top or the underside, with a free restyle.
A panel hit keeps decision 0042's order, with the deck resolved to its outermost layer.
Planks with `floor` picked still lay a floor beside the wall.

**Breaking.**
Wherever the wall's peel answers "deck", a floor-style order within the deck decides the layer (`DeckPeelLayer`, reached from `PeelAt`).
On a deck-box hit that is the hit face's finish, then any finish, then the infill, then the joists.
Otherwise it is the outermost deck layer.
This covers `OnBlockBroken`, the layer material used for sounds, resistance and fire, `RemoveLayer`, `ComputeDrops`, and the open-part drops in `HostChangePrefix`.
Decision 0013's one layer per break holds.
The tooltip and `GetBlockInfo` add the deck's infill, top and underside lines under `Deck:`.

**Sealing, and old decks.**
The deck's UP retention and `CanAttachBlockAt` come from `ComputeDeckRetention`, which is `ComputeRetention(deck, deckInfill)`, so a clay deck cools.
Every deck built before this sealed as bare framing, and turning that off would quietly unseal upper storeys and cellars in existing worlds.
Old decks draw the new bare joists, since the solid slab is gone, but keep sealing through `LegacyDeck`.
The flag is saved as `legacydeck` and set on load when the entity has a deck and no `legacydeck` key.
It is detected by the key's absence, not by a missing new attribute, because a null string does not survive a save: an unfilled new deck saves no `deckinfill` key, and would read as old.
Once saved, the flag is always present, so a new deck is never mistaken for an old one.
A legacy deck seals at 1 until it is filled or broken.

## Alternatives considered
- **Leave decks as framing only.** Free, and the floor stops 12/16 short of the wall in every finished room.
- **Host a floor block in the wall's cell.** A cell holds one block; decision 0042 already turned down guest-hosting a floor.
- **Unseal old decks.** Consistent, but it changes existing worlds without a word.
- **Old decks keep drawing the solid slab.** Keeps them identical, and leaves two looks for one deck; rejected in favour of the bare joists, with the legacy flag keeping only the sealing.
- **Refuse glass on a deck.** Simpler, but a glass floor would end at the wall in a glazed room; the bezel clips like the rest, so it is supported.
- **Rotate the deck's groups with the wall.** The wall mesh turns with its side, which would turn the joists; per-side groups tesselated unrotated keep them north-south.

## Consequences & open questions
- The wall entity's tree attributes, the tooltip (0030) and `GetBlockInfo` each grow more lines, and the shapes grow by a set of deck groups per side.
- Deck light absorption is unchanged: the wall's absorption does not read the deck, so a filled deck does not darken the cell further.
- The handbook's Deck and Floors text grows to say a deck takes infill and finishes.
- Not yet played.
  To play: a floor run into a decked wall on all four sides and a cornerout; board direction matching across the join; a glass floor merging into a glass deck; layering and peeling from above and below; rooms sealing above and below; a clay deck cooling; an old world's deck still sealing.
