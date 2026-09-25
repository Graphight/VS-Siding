# Fireproof infill

- Status: Draft
- Created: 2026-09-25
- Reflects: decision 0033; `SidingWallBlock.cs`'s `HitLayerMaterial`/`GetResistance`/`GetBlockMaterial`; `wall.json`'s `Infills`/`Finishes`/`combustibleProps`; decompiled 1.22.7 `CollectibleObject.GetCombustibleProperties`, `BEBehaviorBurning`, `BlockLava`, `ModSystemFireFromLightning`, `BlockEntityCharcoalPit`

## Summary
A wall over clay or stone infill still burns exactly like a plank, because `GetCombustibleProperties` is never overridden: every siding wall answers with the same `combustibleProps` block from `wall.json` (`burnTemperature: 600, burnDuration: 20`, matching vanilla planks) regardless of position.
`GetCombustibleProperties(world, itemstack, pos)` is `virtual` on `CollectibleObject` (`CollectibleObject.cs:322`) and takes the same `pos` that `GetResistance`/`GetBlockMaterial` already answer per-position (decision 0033).
The fix reuses that machinery exactly: override it to return `HitLayerMaterial`'s `null`-face answer, and hand back `null` unless that material is `Wood`.
No new JSON attribute is needed — every `Infills`/`Finishes` entry already carries `BlockMaterial` (decision 0033 tagged them all), and only the `Wood` bucket is naturally flammable.

## Context
`GetCombustibleProperties` is one virtual method with two call shapes: `(world, itemstack, null)` for an item in a slot, and `(world, null, pos)` for a block in the world.
`Block` never overrides it, so `SidingWallBlock` falls through to `CollectibleObject`'s body, which ignores every argument and returns the `CombustibleProps` field set from `combustibleProps` in `wall.json` (line 260) — always present, always the plank numbers, on every material combination.

### Consumer sweep
Same rule as decisions 0020 and 0033: decompiled bodies, not `VintagestoryAPI.xml`.
Every call site below passes a live `BlockPos`, so every one of them is already answerable per-position once the override exists.

| Member | What it decides | Affected? |
| --- | --- | --- |
| `BEBehaviorBurning.getBurnDuration` (`BEBehaviorBurning.cs:93`) | whether a block adjacent to fire is fuel (`canBurn`/`OnCanBurn`), and how long it burns | Yes — this is fire spread proper: a fire block's own `BEBehaviorBurning` targets the wall as `FuelPos` via `TrySpreadTo`, and on timeout calls `Api.World.BlockAccessor.SetBlock(0, FuelPos)` (`BEBehaviorBurning.cs:78`), deleting the whole wall block |
| `BlockLava.IsNextToCombustibleBlock` (`BlockLava.cs:110`) | whether lava ignites a neighbouring block | Yes — lava next to a stone-infilled wall no longer sets it alight |
| `ModSystemFireFromLightning` (`ModSystemFireFromLightning.cs:36`) | whether a lightning strike can start a fire at a position | Yes — a struck wall only catches if its topmost layer is `Wood` |
| `BlockEntityCharcoalPit.IsCombustible` (`BlockEntityCharcoalPit.cs:394`) | whether a block bordering a charcoal pit counts as pit fuel | Yes in principle, for a wall built into a charcoal pit's fuel stack; the same call shape, listed for completeness rather than because it's a common case |
| `BlockEntityPitKiln`/`BlockPitkiln` (`BlockEntityPitKiln.cs:100,265`; `BlockPitkiln.cs:157`) | whether a block in a pit kiln's fuel layer counts as fuel | Yes in principle, same call shape; a siding wall would have to be one of the blocks a pit kiln's fuel check walks, which is exotic but not impossible |
| `CollectibleBehaviorHandbookTextAndExtraInfo` and the smelting/cooking/forge/bloomery call sites (`ItemOre.cs`, `BlockFirepit.cs`, `BlockEntityOven.cs`, etc.) | fuel/smelting values for an **item** in a slot | No — every one of these passes `pos: null` with a real `itemstack`, a different call shape the override leaves untouched (same fallback `SidingWallBlock` already uses for `GetBlockMaterial` when `pos` is null) |
| `Block.GetPlacedBlockInfo`/handbook tooltip for the placed block | tooltip text | Not reached with a `pos` in practice, same as decision 0033's `GetHeldItemInfo` finding |

## Design
**Override `GetCombustibleProperties` in `SidingWallBlock` to answer from the topmost peeled layer, reusing `HitLayerMaterial` unchanged:**
```
public override CombustibleProperties? GetCombustibleProperties(IWorldAccessor world, ItemStack? itemstack, BlockPos? pos)
{
    if (pos == null) return base.GetCombustibleProperties(world, itemstack, pos);
    return HitLayerMaterial(world.BlockAccessor, pos, null) == EnumBlockMaterial.Wood
        ? base.GetCombustibleProperties(world, itemstack, pos)
        : null;
}
```
`HitLayerMaterial(accessor, pos, null)` is the exact call `GetResistance` and `GetBlockMaterial` already make (`SidingWallBlock.cs:747`, `758`): no clicked face, so it resolves through `PeelLayer`'s fallback order — front finish, then secondfront, then back finish, then infill, then the bare frame.
Of the five `BlockMaterial` buckets decision 0033 introduced (`Wood`/`Soil`/`Glass`/`Ceramic`/`Stone`), only `Wood` is naturally flammable in vanilla — planks, shakes, wattle and straw infill are all tagged `Wood` in `wall.json`; clay, stone, brick and glass are not.
So the check is a single equality test against `EnumBlockMaterial.Wood`, not a new per-material flag: the same `BlockMaterial` tagging that already drives sound and resistance drives this too.

**This inherits 0033's peel-order caveat exactly.** A wood-planked or shake-finished wall over stone or clay infill still resolves to `Wood` and still burns, because the finish is what `PeelLayer` returns first when one exists — the infill's fireproofing only shows through on a bare frame (no finish) or a non-flammable finish.
That is the same "resistance can differ from what the face under your cursor suggests" limitation 0033 already recorded, now extended to flammability; it is not a new problem this proposal introduces.

**A fireproof position stops the wall from being selected as fuel at all — it does not make a burning wall partially resistant.** `GetCombustibleProperties` returning `null` makes `canBurn`/`OnCanBurn` (`BEBehaviorBurning.cs:64`, `93`) answer `false` for that position, so `TrySpreadTo` never spreads fire onto it and lava/lightning never ignite it.
There is no partial-burn state to design: vanilla's own fire mechanic is binary — a fuel block either isn't touched, or its `FuelPos` times out and `Api.World.BlockAccessor.SetBlock(0, FuelPos)` deletes the entire block, layers and all (`BEBehaviorBurning.cs:78`).
This proposal does not intercept that deletion or add layer-by-layer burning (peeling one layer per fire tick, mirroring decision 0013's break-time peel) — `OnFireDeath` is an `Action<bool>` field on `BEBehaviorBurning`, not a virtual `Block` method, so intercepting it would mean a Harmony patch on a per-instance delegate rather than an override, a materially bigger change than reusing `HitLayerMaterial`.
A burning flammable wall still vanishes whole, same as before this proposal; it just no longer ignites at all once its topmost layer is stone, clay, brick or glass.

**Framing stays wood and is never checked directly.** `LayerMaterial`'s comment already states framings carry no `BlockMaterial` and fall back to the block's own `Wood` (`SidingWallBlock.cs:873-874`); this proposal doesn't change that.
A bare frame (no infill, no finish) still answers `Wood` through the same fallback, and still burns — consistent with a stud wall being flammable lumber.

## Alternatives considered
- **A new `Fireproof: true`/`Combustible: false` attribute per material.** Redundant: `BlockMaterial` already partitions exactly the two states this needs (`Wood` vs. everything else), and decision 0033 already back-filled it onto every `Infills`/`Finishes` entry. A second flag would have to be kept in sync with the first for no behavioural gain.
- **Check the infill alone, ignoring the finish.** The current leaning is against it, since `GetResistance`/`GetBlockMaterial` already answer in peel order (0033) and fire would be the one exception.
Still open below: it is what the request asked for.
- **Peel one layer off a burning wall instead of deleting the whole block.** Would make fire damage feel like decision 0013's break-peel, but requires intercepting `BEBehaviorBurning.OnFireDeath`, an instance delegate on the *fire* block's behaviour, not an override on `SidingWallBlock`. Out of scope for a per-position combustibility answer; could be its own proposal if whole-block deletion turns out to feel wrong in play.
- **Leave `combustibleProps` off entirely so no siding wall ever burns.** Rejected: framing is always wood, and an all-wood-and-straw wall burning is correct; the bug is only that stone/clay/glass never protected anything.

## Consequences & open questions
- No new attribute, no `wall.json` changes: the fix is entirely in `SidingWallBlock.cs`, reusing `HitLayerMaterial`, `BlockMaterial` and the existing `combustibleProps` block.
- A wall finished on one side in planks and the other in ashlar answers by whichever peel order returns first (front, then secondfront, then back) for a faceless query — the same asymmetry 0033 already flagged for resistance and sound, now also true for fire.
- A charcoal pit or pit kiln built against a stone/clay-infilled wall would no longer be able to draw fuel from it; both call sites pass a live `pos` and are covered by the same override, so this is a consequence, not a gap.
- This sweep is 1.22.7's; redo it on a game update, per decision 0020's rule.
- Open: whether a non-flammable infill should protect a wood-finished wall too.
The request was fireproofing by infill; this design lets the finish win, as 0033 does for resistance, so a planked clay wall still burns.

## Stages
1. **Override:** add `GetCombustibleProperties` to `SidingWallBlock`, reusing `HitLayerMaterial(accessor, pos, null)` against `EnumBlockMaterial.Wood`, matching `GetResistance`'s null-pos fallback.
2. **Test:** a golden-style unit test per `BlockMaterial` bucket (`Wood` returns the block's `combustibleProps`, every other bucket returns `null`), plus a peel-order case (planks finish over stone infill still returns non-null).
3. **Playtest:** stand a fire against a bare-frame wall (burns), a straw/wattle-infilled wall (burns), a stone- or clay-infilled bare wall (does not ignite), and a plank-finished wall over stone infill (still burns) to confirm the peel-order caveat plays out as designed.
4. **Graduate** as a decision extending 0033.
