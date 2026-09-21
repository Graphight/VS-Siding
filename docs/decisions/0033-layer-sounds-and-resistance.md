# 0033 — Layer sounds and resistance

- Status: Accepted
- Created: 2026-09-21
- Reflects: branch `layer-sounds-and-resistance`; commits `01588c7`, `c9a3d07`, `ac117be`, `b47c741`

## Summary
Hit sound, break sound, break time and tool suitability now come from the layer under the cursor, instead of every wall behaving like a plank at `resistance: 1.2`.

## Context
`wall.json` hard-coded one sound set and one `resistance: 1.2` for every wall in the mod.
So an ashlar-faced wall over granite rubble sounded like a plank, chipped away like a plank, and a fist took it down as fast as a hammer would.

Decision 0013 already peels one layer per break, so the code already knew which material was under the cursor.
This wires that answer into vanilla's three per-position hooks: `GetSounds`, `GetResistance`, `GetBlockMaterial`.

## Design
`LayerKey` and `LayerMaterial` resolve the hit layer's `EnumBlockMaterial`, extracted from the switch that was already inlined in `OnBlockBroken`.

`LayerSounds` and `LayerResistance` are dictionaries in `attributes`, keyed by `EnumBlockMaterial` name — four or five buckets (Wood, Soil, Glass, Ceramic, Stone), not five hundred hand-tuned materials.

`GetSounds`, `GetResistance` and `GetBlockMaterial` all read the hit layer's material via a shared `HitLayerMaterial` lookup.
`OnLoaded` parses both dictionaries once into `Dictionary<EnumBlockMaterial, BlockSounds>`/`Dictionary<EnumBlockMaterial, float>`, because `AsObject<BlockSounds>()` per hit tick is a full Newtonsoft parse.

Resistance multipliers against a base of 1.2: Wood 1.0, Soil 0.7, Glass 0.5, Ceramic 1.5, Stone 2.5.

**One correction to the proposal.** It claimed "every material entry already carries `BlockMaterial`".
That was only true of `Infills`.
`Finishes`/`FinishFamilies` had to be tagged as part of this work: daub/daub-{color} Soil, brick* Ceramic, planks*/shakes Wood, cobblestone/polished/ashlar/drystone Stone.
`Framings` are all planks and carry none — they fall back to the block's own `blockmaterial: "Wood"`.

### Consumer sweep

`GetBlockMaterial` is a vanilla method, and vanilla overloads it across unrelated systems, same as decision 0020's `SideSolid` sweep.
The sweep was done by decompiling `VintagestoryAPI.dll`, `VSSurvivalMod.dll`, `VSEssentials.dll` and `VSCreativeMod.dll` with `ilspycmd` (1.22.2), not by reading `VintagestoryAPI.xml`.
Every finding below is verified against decompiled source.

| Member | What it decides | Affected? |
| --- | --- | --- |
| `CollectibleObject.GetMiningSpeed` / block-breaking tick loop | which tool suits the block, break-speed multiplier | Yes — the intended effect |
| `Block.GetBlastResistance` | explosion damage resistance | Yes — calls the virtual method at Block.cs:2500/2502 |
| `Block.ExplosionDropChance` | chance of dropping loot when exploded | Yes — calls the virtual method at Block.cs:2514 |
| `Block.EffectiveBleedPriority` | bleed priority between adjacent blocks | No — reads the `BlockMaterial` field |
| `Block.GetRetention` (Sound branch) | sound retention | Moot — `SidingWallBlock` fully overrides `GetRetention` and never calls base |
| `Block.GetHeldItemInfo` tooltip | "Material:" line when held | No in practice — called with `pos` null, falls to base |
| `CollectibleBehaviorArtPigment` | whether a face is paintable | Not reached — gates on `SideSolid[face]` first, false on every siding wall (decision 0002) |
| `BlockBehaviorDecor` (VSEssentials) | whether decor can attach | Yes in principle, but it only disqualifies Snow/Ice, which a wall never resolves to |
| `BlockDoor`/`BlockAxle`/`BlockBeehive`/`BlockGroundStorage`/`BlockMicroBlock`/WorldEdit | type-specific behaviours | Not applicable — called on their own block types, never a `SidingWallBlock` |
| `EntityAgent` snow-footstep particles | snow particles under an entity | Reaches the override only if an entity stands inside a wall; returns the topmost layer, never Snow. No effect |

**The sweep overturned the plan's own preliminary guess.**
The plan assumed `GetBlastResistance`/`ExplosionDropChance` read the `BlockMaterial` field and so were untouched.
The decompiled bodies call the virtual method instead.
The XML doc prose for `GetBlastResistance` even *says* "BlockMaterial", and the real body calls `GetBlockMaterial(...)` — a concrete case of `CLAUDE.md`'s rule that the XML is prose and only the DLL is truth.
The consequence is a bonus, not a bug: a stone-faced wall now resists blasts like stone.

`GetBlockMaterial(accessor, pos, stack)` falls back to `base` when `pos` is null, and is allocation-free and thread-safe, since the API doc warns it may run off the main thread.

## Alternatives considered
- **Per-material resistance/sound in the dictionaries.** 500 entries times three fields, hand-tuned, to express four buckets. The bucket *is* `BlockMaterial`.
- **Resistance from the whole wall rather than the hit layer.** Would make peeling a plank finish off a stone wall as slow as the stone.
- **`requiredMiningTier` per layer.** Suitability yes, a hard gate no — a player who boarded over rubble must be able to undo it without a pickaxe.
- **Leaving it alone.** Defensible, it's cosmetic, but the peel logic was already written, so the change was small.

## Consequences & open questions
- `combustibleProps` is read off the block, not the position, so a granite wall still burns. Out of scope, noted rather than faked.
- `walk` is left alone; nobody walks on a quarter panel.
- Place sound stays wood, which is correct since the frame *is* wood.
- `GetResistance` and `GetBlockMaterial` take only a `BlockPos`, with no clicked face, so they fall back to `PeelLayer`'s order: topmost finish, then infill, then frame. That's the right answer, but it means resistance can differ from what the face under your cursor suggests on a wall finished on one side only.
- This sweep is 1.22.2's; redo it on a game update, like decision 0020's.
