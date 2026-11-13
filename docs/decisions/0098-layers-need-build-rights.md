# 0098 — Every layer needs build rights in a land claim

- Status: Accepted
- Created: 2026-11-12
- Reflects: issue #171; branch `audit-claims-and-hot-paths` (PR #173) and its review; `SidingWallBlock.ClaimRefuses` and its callers in `SidingWallBlock`, `SidingFloorBlock` and `PlaceWallFrame`; `TileFill`; `ClaimScenarios`

## Summary
Any click that adds a layer to a wall, a floor or a deck, or a plank face to another block, needs build-or-break rights in a land claim.

## Context
Vanilla tests a claim in two places: `Use` before `Block.OnBlockInteractStart`, and `BuildOrBreak` inside `CanPlaceBlock`.
`OnHeldInteractStart` gets range checks only.

So three `PlaceWallFrame` writes tested no claim at all (#171): face decor, the corner upgrade of a filled wall, and a floor-mode deck.
Every other layer went on through the block-use path and so needed use rights only.
`LandClaim.TestPlayerAccess` grants a flag per player or group, so a visitor given use rights to open doors and chests could lay infill and finishes on the owner's walls.

## Design
`SidingWallBlock.ClaimRefuses` tests `BuildOrBreak` and is called just before each write, after the click is known to be a layer and before any cost or error:

- the wall's step, infill, tile and finish, and its deck's infill, tile and finish (`OnBlockInteractStart`, `LayTile`, `LayerDeck`);
- `TryUpgradeLayout` and `TryAddDeck`, inside the helper, so the held-item path and the block-use path are both covered;
- the floor's infill, tile and finish;
- `PlaceWallFrame.TrySkinFace`.

A refused click is handled, so it does not fall through to place something else.
A click that is not a layer, such as one with food in hand, reaches no test and shows no message.

`TileFill` no longer tests the claim itself: it hands the sneak-click to the block, whose tile branch tests it.

A new frame, post or floor, a hosted block and a break already needed build rights through vanilla's `CanPlaceBlock` and its break test.

`WorldMap.TryAccess` runs one body on client and server, as vanilla's `CanPlaceBlock` relies on, so the client does not predict a write the server refuses.

## Alternatives considered
- **Use rights for the corner upgrade and the deck, build rights for face decor.** The first version of this PR, matching the other layers as they then were. The review pointed out that vanilla's held-item builders (`ItemChisel`, `CollectibleBehaviorGroundStorable`) test `BuildOrBreak`, and that a floor run into a claim needed build rights while the deck it added needed only use rights.
- **One test at the top of `OnBlockInteractStart`.** Shorter, but it would show a claim message for clicks that build nothing.

## Consequences & open questions
- A new write, from either path, needs a `ClaimRefuses` call and a scenario in `ClaimScenarios`; each scenario there fails with the test switched off.
- A visitor with use rights only can no longer build on an owner's walls. That is a behaviour change for servers that relied on it.
- Open: whether a refused click shows the claim message once or twice was not observed, since both sides run `TryAccess`.
