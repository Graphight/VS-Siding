# 0098 — Land claims on the held-item path

- Status: Accepted
- Created: 2026-11-12
- Reflects: issue #171; branch `audit-claims-and-hot-paths`; `PlaceWallFrame.TrySkinFace`, `SidingWallBlock.TryUpgradeLayout` and `TryAddDeck`, `TileFill`

## Summary
Face decor on another block needs build-or-break rights; the corner upgrade and the deck need use rights.

## Context
`OnHeldInteractStart` gets range checks only, so a plank click wrote into another player's claim.

## Design
- `TrySkinFace` tests `BuildOrBreak` on the host block, as vanilla decor placement does.
- `TryUpgradeLayout` and `TryAddDeck` test `Use`, the same test every other layer a click adds to a wall gets on the block-use path, and answer true (click handled) on a refusal.

Both helpers are called from the held-item path and from `OnBlockInteractStart`, so the test sits in the helper and covers both with no new parameter.

`WorldMap.TryAccess` runs one body on client and server.
On the server's block-use path vanilla has already tested `Use`, so the test inside `TryUpgradeLayout` and `TryAddDeck` passes silently there.

## Alternatives considered
- **Test in each caller.** Two call sites per helper, and a new caller would miss it.

## Consequences & open questions
- A new write from the held-item path needs its own test; the three scenarios named `...InAnotherPlayersClaim` in `FaceFinishScenarios`, `JunctionScenarios` and `DeckScenarios` are the pattern, each of which fails with its guard removed.
- Open: whether a refused click shows the claim message once or twice was not observed, since both sides run `TryAccess`.
