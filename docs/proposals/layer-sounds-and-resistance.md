# Layer sounds and resistance

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning, after decision 0027

## Summary
Make the sound and the break time of a wall come from the layer you are actually hitting, instead of every wall being planks at `resistance: 1.2`.

## Context
`wall.json` hard-codes one sound set and one resistance for every wall in the mod:

```
resistance: 1.2,
sounds: { place: "game:block/planks", hit: "game:block/planks", break: "game:block/planks", walk: "game:walk/wood" }
```

So an ashlar-faced wall over granite rubble chips away like a plank and *sounds* like a plank, and a fist takes it down as fast as a hammer would.
Meanwhile decision 0013 already peels one layer at a time, so the game knows exactly which material the player is hitting — it just doesn't tell the ear.

Every material entry already carries `BlockMaterial` (`Wood`, `Soil`, `Glass`, `Stone`), which is what vanilla keys its own sounds and tool-suitability from.
The hooks are per-position and all present in 1.22.2: `GetSounds(IBlockAccessor, BlockSelection, ItemStack)`, `GetResistance(IBlockAccessor, BlockPos)`, `GetBlockMaterial(IBlockAccessor, BlockPos, ItemStack)`.

## Design
Reuse `PeelLayer` — the function that already decides which layer a break removes — to pick the layer under the cursor, then:

- **`GetSounds`** returns the vanilla sound set for that layer's `BlockMaterial`: stone for rubble and cobble, soil for clay and daub, glass for glazing, planks for framing and board finishes.
- **`GetResistance`** scales the base by that layer's material, so a stone face is slow and straw is quick. Keep it one multiplier per `BlockMaterial`, in the attributes, not a per-material number to tune 500 times.
- **`GetBlockMaterial`** returns the same layer's material, which is what makes a pickaxe the right tool for a stone face and an axe right for boards, for free.

Everything keys off the existing `BlockMaterial` field, so no new JSON per material.

## Alternatives considered
- **Per-material `resistance` and sound sets in the dictionaries.** 500 entries times three fields, hand-tuned, to express four buckets. The bucket *is* `BlockMaterial`.
- **Resistance from the whole wall rather than the hit layer.** Simpler, and wrong in the direction that matters: it would make peeling a plank finish off a stone wall as slow as the stone.
- **`requiredMiningTier` per layer.** Tempting for stone, but it means a player who boarded over rubble can't undo their own wall without a pickaxe they may not have brought. Suitability yes, a hard gate no.
- **Leaving it.** Defensible — it's cosmetic. But it's the cheapest remaining "this mod feels finished" change, and the peel logic is already written.

## Consequences & open questions
- `combustibleProps` is read off the block, not the position, so a granite wall still burns. That needs a different mechanism (or a `bytype` split) and is out of scope here; note it rather than fake it.
- `walk` sound is a property of the block's top face, and nobody walks on a quarter-panel, so leave it.
- Place sound fires before the entity's layer exists on the client, so placing a frame will always sound like wood. Correct anyway: the frame *is* wood.
- One test per hook, asserting the whole returned value for a stone-faced wall and a bare frame.
