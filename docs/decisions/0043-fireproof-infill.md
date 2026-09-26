# 0043 — Fireproof infill

- Status: Accepted
- Created: 2026-09-25
- Reflects: branch `fireproof-infill`; `SidingWallBlock.GetCombustibleProperties`/`ResolveLayerCombustible`/`TryBurnLayer`/`RemoveLayer`; `SidingModSystem.BurnLayerPrefix`; decision 0033; decompiled 1.22.7 `CollectibleObject.GetCombustibleProperties`, `BEBehaviorBurning`, `BlockLava`, `ModSystemFireFromLightning`, `BlockEntityCharcoalPit`, `BlockBehaviorCanIgnite`; played

## Summary
A wall over clay or stone infill used to burn exactly like a plank, and a fire that burnt out against it deleted the whole block, infill and all.
Now a wall only catches when its topmost layer is `Wood`, and a burnt-out fire takes that one layer instead of the block.
So planks over clay burn off and leave the clay standing, a straw wall burns layer by layer down to its frame, and a stone-finished wall never catches.

## Context
`GetCombustibleProperties` is one virtual method with two call shapes: `(world, itemstack, null)` for an item in a slot, and `(world, null, pos)` for a block in the world.
`Block` never overrides it, so `SidingWallBlock` fell through to `CollectibleObject`'s body, which ignores every argument and returns the `CombustibleProps` field set from `combustibleProps` in `wall.json`: always present, always the plank numbers (`burnTemperature: 600, burnDuration: 20`), on every material combination.

Burning itself is binary in vanilla.
A fire block's `BEBehaviorBurning` picks a neighbouring fuel block, counts down its burn duration, and on timeout calls `KillFire(consumeFuel: true)`, whose `OnFireDeath` runs `SetBlock(0, FuelPos)`: the fuel block is deleted outright.

### Consumer sweep
Same rule as decisions 0020 and 0033: decompiled bodies, not `VintagestoryAPI.xml`.
Every call site below passes a live `BlockPos`, so every one of them answers per position once the override exists.

| Member | What it decides | Affected? |
| --- | --- | --- |
| `BEBehaviorBurning.getBurnDuration` | whether a block next to fire is fuel (`canBurn`/`OnCanBurn`), and how long it burns | Yes: this is fire spread proper, via `TrySpreadTo`, and the server's slow tick puts a fire out once its fuel stops answering |
| `BlockLava.IsNextToCombustibleBlock` | whether lava ignites a neighbouring block | Yes |
| `ModSystemFireFromLightning` | whether a lightning strike can start a fire at a position | Yes |
| `BlockEntityCharcoalPit.IsCombustible` | whether a block bordering a charcoal pit counts as pit fuel | Yes in principle; listed for completeness |
| `BlockEntityPitKiln`/`BlockPitkiln` | whether a block in a pit kiln's fuel layer counts as fuel | Yes in principle; exotic |
| Handbook and the smelting/cooking/forge/bloomery call sites | fuel values for an **item** in a slot | No: all pass `pos: null`, which the override hands to base |

## Design
**`GetCombustibleProperties` answers from the topmost layer, through the same lookup as 0033.**
With a position, it takes `HitLayerMaterial(accessor, pos, null)`, the exact call `GetResistance` and `GetBlockMaterial` make, and `ResolveLayerCombustible` returns the block's own props for `Wood` and `null` for anything else.
With no position it falls through to base, so an item in a slot keeps the plank numbers.
No new JSON attribute: 0033 already tagged every `Infills`/`Finishes` entry with a `BlockMaterial`, and `Wood` is the only flammable bucket (planks, shakes, logs, wattle, straw); clay, stone, brick and glass are not.

**The topmost layer decides, not the infill.**
A null face resolves through `PeelLayer`'s fallback order: deck, front finish, secondfront, back finish, infill, bare frame.
So a planked or shaked wall over clay still catches, because the finish is what's on the outside.
Framings and decks carry no `BlockMaterial` and fall back to the block's own `Wood`, so a bare frame and a decked wall both catch.
Letting the infill alone decide was the request's wording, and was rejected: it would make fire the one exception to 0033's peel order, and the next part makes it unnecessary.

**A burnt-out fire takes one layer, not the block.**
`SidingModSystem.BurnLayerPrefix`, a Harmony prefix on `BEBehaviorBurning.KillFire`, asks `SidingWallBlock.TryBurnLayer` whenever the fuel is a siding wall and `consumeFuel` is true.
If `PeelLayer(null, ...)` names a layer, the server clears it through `RemoveLayer`, the switch `OnBlockBroken` already used for a player's peel (decision 0013), now shared; a burnt layer drops nothing.
The prefix then flips `consumeFuel` to false, so `OnFireDeath` only removes the fire block and does not spread into the wall's position.
A bare frame has no layer left, so `TryBurnLayer` answers false and vanilla deletes the block as before.
The client runs the same prefix when the fire's synced state kills it there, so it also keeps its block and doesn't delete it locally.

So a finish burns off; if what's beneath is still `Wood`, a neighbouring fire can catch it again and take the next layer; once the top is clay, stone, brick or glass, nothing re-ignites and the fire goes out.

`OnFireDeath` is an `Action<bool>` field on `BEBehaviorBurning`, not a virtual `Block` method, which is why this is a patch on `KillFire` rather than an override.

## Alternatives considered
- **A new `Fireproof`/`Combustible` attribute per material.** Redundant: `BlockMaterial` already partitions exactly `Wood` from everything else, and a second flag would have to be kept in sync with it.
- **Infill alone decides.** Rejected above: an exception to 0033's peel order, and layer burning already lets a clay infill survive its finish.
- **Whole-block deletion, as vanilla does.** Shipped first on this branch; in play it deleted a clay wall along with its shakes, which read as the fireproofing not working at all.
- **Leave `combustibleProps` off so no wall ever burns.** Rejected: framing is always wood, and an all-wood-and-straw wall burning is correct.
- **A config toggle for wall fire.** Rejected for now: the change only makes walls more forgiving, and a player wanting no fire spread at all has vanilla's `allowFireSpread` world setting.

## Consequences
- A wall finished on one side in planks and the other in ashlar answers by whichever layer peel order returns first, the same asymmetry 0033 recorded for resistance and sound, now for fire too.
- A charcoal pit or pit kiln against a non-wood-topped wall can no longer draw fuel from it; both call sites pass a live `pos`.
- A fire burning against a hosted furniture cell (decision 0035) targets the furniture block, not the guest wall, and is untouched by this.
- If the `KillFire` patch fails to apply, the log says so and burnt-out walls are deleted whole again; combustibility still answers per layer.
- This sweep is 1.22.7's; redo it on a game update, per decision 0020's rule.
