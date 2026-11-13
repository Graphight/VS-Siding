# 0096 — A diagonal's outer deck

- Status: Accepted
- Created: 2026-11-11
- Reflects: branch `diagonal-outer-deck`, issue #168 and a ModDB comment asking for it; `SidingWallBlock.OuterDeckBoxes`/`HitDeckHalf`/`HalfFacing`/`TryAddDeck`/`LayerDeck`/`PeelAt`/`BurnLayer`/`LayerParts`, `SidingWallEntity`'s `OuterDeck*` fields, `DeckLayers` and `CellAlternate`, `WallShapeGen`'s `outerdeck-*` groups, `SidingFloorBlock.Continues`, `DiagonalScenarios`; decisions 0042, 0043, 0053, 0066, 0067; the author's choice of the hit side's deck for a panel hit and the review of PR #172

## Summary
A diagonal's deck can cover its outer triangle as well as its inner one, each half with its own layers.
Decision 0067 gave a diagonal a deck over the room's half only, and a thin floor cannot stand in a diagonal's cell.

## Context
A lower storey cuts a corner with a diagonal while the storey above is square, or two rooms sit side by side and a diagonal cuts one room's corner.
In both cases the outer triangle of the diagonal's cell has no floor.
Decision 0067 left it out because nothing asked for it.

## Design
**The outer triangle is the inner's eight strips turned 180 degrees about the cell centre.**
`OuterDeckBoxes` holds them, so the teeth and corner cuts mirror 0067's and the same drawing rules apply.
They follow the inner strips in the collision and selection lists, and `HitDeckHalf` reads which half a selection index falls in.

**Each half has its own state.**
`SidingWallEntity` keeps `OuterDeck`, `OuterDeckInfill`, `OuterDeckFront`, `OuterDeckBack` and the two styles, saved as `outerdeck*` keys.
Either half can be decked alone.

**A click decks the half it lands on.**
With Deck lit, planks on a face the diagonal claims add the outer deck, and on an unclaimed face the inner (`HalfFacing`).
A floor run laid up to the diagonal decks the half facing the run.
A freshly raised diagonal with Deck lit gets the inner half only.

**A half costs half a deck.**
`LayerParts("diagonal", deck*)` is 2 for every deck layer, rounded up by `Share`, so one half costs and refunds half of what a full deck does.
A diagonal's inner deck saved before this refunds half what it cost.

**Retention stays with the inner half.**
The UP face reads `ComputeDeckRetention` on the inner fields.
The outer half retains nothing, since the outer room never enters the cell and the claimed faces stop it.
`CanAttachOf` stays false on DOWN, so neither half carries a hanger.

**Peeling.**
A hit on a strip peels that half's layers in a floor's order.
A hit on a claimed face of the panel takes the outer deck before the wall's layers, as a hit on the room side already took the inner deck first.
A hit with no face, which fire and break resistance use, takes the inner deck, then the outer, then the wall's layers.
Fire catches only on wood (decision 0043), so an inner deck top that does not burn leaves the outer deck's top layer next in line.
With Floor picked, planks on a half's top board it once that half is filled, whatever the other half holds.
The tooltip shows the half the selection is on.
The outer deck's framing counts toward the cell's alternate, so a stick or bamboo outer deck alternates with the floor beside it.

**Floors join the half they face.**
`SidingFloorBlock.Continues` reads the outer deck across a claimed face and the inner across an unclaimed one, and checks that half's glazing.

## Alternatives considered
- **A thin floor in the diagonal's cell.** A floor has no orientation, so a cut floor needs new variants, and the cell holds a wall.
- **One deck over the whole cell.** A square deck stands outside the panel as a triangular ear, which is the reason 0067 cut the inner half.
- **One shared set of layers for both halves.** The two halves face different rooms, so each takes its own finishes.
- **Hangers under a full deck.** Not built.

## Consequences & open questions
- 0067's consequence that the outer triangle has no floor no longer holds.
- A triangle floor standing alone in an empty cell is still unbuilt.
- A decked wall or corner upgraded to a diagonal paid a full deck, and its deck becomes the inner half, which refunds half.
- `diagonal.json` went from 4.4 MB to 7.0 MB with the outer groups; the strips are still not merged (0067).
- The tooltip's choice of half, the drawing and walking the outer strips are not covered by unit or e2e tests.
