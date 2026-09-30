# Deck as floor

- Status: Draft
- Created: 2026-09-29
- Reflects: decision 0042's deck; decision 0050's floor shape and entity; a session discussion after the first floor playtest; not yet played

## Summary
A deck is framing timber and nothing else (decision 0042).
This gives it the floor's layers: joists from the held plank, then its own infill, a top finish and an underside, so a floor run up to a wall reads as one floor from above and below.

## Context
Decision 0050 built a floor out of the wall's layers laid flat, and called the deck its rim joist.
Beside a floor with floorboards and a plastered ceiling, the deck is bare framing timber on both faces, so the floor stops short of the wall by the deck's 12/16.
Every floor's joists run north-south three to a cell, at the same x in every cell, so a deck drawn from the same table lines its joists up with the floor beside it.

## Design
**The floor's shape, trimmed to the deck.**
`WallShapeGen` emits the floor's elements clipped to the deck's area (x 4..16 unrotated, a 12×12 square for a cornerout) as `deck-*` groups.
The wall's mesh turns with its `side`, and the deck's joists must not, so the generator writes one set per rotation and the entity picks the set for its side.

**Deck layers on the wall entity.**
`SidingWallEntity` gains `DeckInfill`, `DeckFront` and `DeckBack`, keyed into the same dictionaries; `Deck` stays the framing.
The texture source maps the `deck-*` slots to them.

**Which layer a click means.**
Decision 0042 already made the deck its own selection box, so `BlockSelection.SelectionBoxIndex` tells a deck hit from a panel hit.
A deck hit layers the deck the way a click layers a floor (`SidingFloorBlock.OnBlockInteractStart`): infill on any face, a finish on its top or its underside.
A panel hit is unchanged.

**Breaking.**
A deck hit peels the deck as a floor peels: the hit face's finish, then any finish, then the infill, then the deck frame.
A panel hit peels the wall as today.

**Sealing, and old decks.**
A floor seals once framed and filled, so a new deck does the same.
Every deck built before this seals as bare framing, and turning that off would quietly unseal upper storeys and cellars in existing worlds.
A deck loaded without the new tree attributes is marked as sealed, and keeps sealing until it is broken or filled.

## Alternatives considered
- **Leave decks as framing only.** Free, and the floor stops 12/16 short of the wall in every finished room.
- **Host a floor block in the wall's cell.** A cell holds one block; decision 0042 already turned down guest-hosting a floor.
- **Unseal old decks.** Consistent, but it changes existing worlds without a word.

## Consequences & open questions
- The wall entity's tree attributes, the tooltip (0030) and `GetBlockInfo` each grow three more lines.
- The legacy flag needs a name that survives a save and load, and a test that an old deck still seals.
- Lighting under a filled deck is `thin-floor-lighting`'s problem, like any sealed floor.
