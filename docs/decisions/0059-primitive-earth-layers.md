# 0059 — Primitive earth layers

- Status: Accepted
- Created: 2026-10-04
- Reflects: split from `primitive-framing` (decision 0058) on 2026-10-04; mod page comment https://mods.vintagestory.at/vssiding#cmt-242349; branch `feat/primitive-earth-layers`; `config/materials.json`, `EarthScenarios`, `SidingWallBlockRetentionTests`; vanilla `packeddirt.json`, `rammed.json` and the grid recipes `packeddirt.json`, `rammedearth.json`; decisions 0001, 0010, 0015; unit and end-to-end tests pass; not yet played

## Summary
Two earth materials a stone-age player can make with no tool: a packed earth infill and a rammed earth finish in vanilla's five patterns.
Both are entries in `config/materials.json` with vanilla textures, and no code.

## Context
The player who asked for primitive walls named "wattle daub, logs, rammed earth, straw".
Wattle, daub, logs and straw are already layers; rammed earth is the one missing.

Vanilla makes both from soil in the crafting grid, with no tool.
Six low-fertility soil blocks give six `game:packeddirt`, and six packed dirt give six `game:rammed-light-plain`.
One more grid step turns plain rammed earth into `thinlight`, then `thicklight`, `thinheavy`, `thickheavy` and back to plain, one block in and one block out.

These sat inside `primitive-framing`, which also carried the stick frame and the stone build signal.
They need neither: a plank frame takes them, so they shipped on their own.

## Design
**A packed earth infill.**
A `packeddirt` entry in `Infills`, shown as "Packed Earth Infill", consuming and dropping one `game:packeddirt` block with its vanilla texture.
It names `BlockMaterial: "Soil"`, as `clay` does, so it cools a cellar the way clay does (decision 0015) and takes the soil layer sounds.
The code is exact, so `packeddirt-trampledearth` does not match it.

**A rammed earth finish, one per pattern.**
A `rammed-{pattern}` entry in `FinishFamilies`, matching `game:rammed-light-*` on its `pattern` variant (decision 0010).
It expands to five finishes, each consuming and dropping one block of its own pattern and drawing that pattern's texture.
The held block picks the look, as rock type does for `cobblestone-{rock}`, so a peeled finish comes back as the block that went on.
It is a flat face with no `Elements`, as `plate-{metal}` is.

**Display names.**
Each entry carries a `DisplayName` in `lang/en.json`, which `MaterialDisplayNameTests` checks against the real game assets once `rammed.json` is among the test's candidates.
The handbook, the README and the ModDB page name both.

**One end-to-end scenario.**
The unit sweeps check each entry's lang key and texture, and nothing checks that a `Consumes.code` names a real collectible.
`EarthScenarios` layers one packed dirt and one patterned rammed earth block onto a frame in the running game.

## Alternatives considered
- **Keep them in `primitive-framing`.** They share a theme and nothing else; splitting them out left the promised frame a one-session job.
- **One `rammed` finish that consumes any pattern and drops plain.** The proposal's first shape. It gives one look for five blocks, and the family costs four more lang lines.
- **Patterns as finish styles on the picker.** A style needs a picker row (decision 0040). The family gives the same five looks with no row, and the pattern step is already one block in, one block out at the crafting grid.
- **Consume loose soil instead of the packed block.** Soil is a block with fertility variants and grass states, so a match on it would also take a right-click with any dirt in hand. The packed block is one code and its recipe sets the cost.
- **A `drypackeddirt` infill too.** A second earth infill that differs only by a sand ingredient and a texture; left until someone asks.

## Consequences & open questions
- Not yet played: how the banded textures read on a quarter-block slab, and laid flat on a floor, is only visible in game.
- Quantity: one block per layer, as cobblestone and polished rock take.
- Rammed earth has one `soil` variant today, `light`. A second would need its own family entry, since a template fills one placeholder.
