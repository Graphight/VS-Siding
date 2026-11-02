# 0084 — A floor decks a wall

- Status: Accepted
- Created: 2026-11-01
- Reflects: branch `inside-corner-post`; `SidingWallBlock.TryAddDeck`/`CanTakeDeck`, `PlaceWallFrame`'s floor path, `SidingFloorBlock.RunEndFor`, `SidingFloorTests`, `VSSiding.E2E.Tests/FloorScenarios.cs`; decisions 0042, 0050, 0053; not yet played

## Summary
A Floor-mode plank click whose target is a wall's cell, or a floor run that reaches one, gives that wall a deck from the held framing.
A click on the wall itself still lays a floor beside it.

## Context
A thin floor and a wall cannot share a cell; the wall's deck is the floor there (decision 0042).
Laying floors up to an internal wall without a deck left a strip, which may be the report "Internal walls create a subdivision in the house that won't fill."
Decision 0042 kept the deck a separate click so that a floor is not auto-detected into a stairwell.
This decision adds an explicit click, not auto-detection.

## Design
**Shared deck add.**
`TryAddDeck` is the deck-add path a plank click on a wall's side face already used, now shared with the floor paths.
It keeps the same checks (a step, an occupied deck, the cost) and the same errors, and `CanTakeDeck` runs those checks without sending an error.

**Floor mode.**
A Floor-mode plank click whose target cell is a wall's, such as a click on the side of the block beside the wall, decks that wall.
A floor run whose end reaches a wall's cell decks it too.
`RunEndFor` counts a wall that `CanTakeDeck` as a run end; when the wall cannot take a deck, the floor's underside fallback applies as before.
A click on the wall itself still lays a floor in the cell beside it (decision 0050).

## Alternatives considered
- **Detect a floor beside a wall and deck it.** Not built: 0042's stairwell concern, a wall left bare beside a stair.
- **Replace the wall with a floor.** Not built: it destroys a wall's layers.
- **A separate item or mode for the deck.** Rejected: the held framing and Floor mode already say it.

## Consequences & open questions
- This extends 0050 and does not supersede it.
- `SidingFloorTests`' "wall at gap gives null" row changes with the run end.
- Whether the strip that "won't fill" is in the floor or in the wall is still the player's to say.
- Played: not yet played in game; the playtest is the last step before merge.
