# Primitive earth layers

- Status: Draft
- Created: 2026-10-04
- Reflects: split from `primitive-framing` on 2026-10-04; mod page comment https://mods.vintagestory.at/vssiding#cmt-242349; `config/materials.json`; vanilla `packeddirt.json`, `rammed.json` and the grid recipes `packeddirt.json`, `rammedearth.json`; decisions 0001, 0010, 0015; not yet played

## Summary
Two earth materials a stone-age player can make with no tool: a packed earth infill and a rammed earth finish.
Both are entries in `config/materials.json` with vanilla textures, and no code.

## Context
The player who asked for primitive walls named "wattle daub, logs, rammed earth, straw".
Wattle, daub, logs and straw are already layers; rammed earth is the one missing.

Vanilla makes both from soil in the crafting grid, with no tool.
Six low-fertility soil blocks give six `game:packeddirt`, and six packed dirt give six `game:rammed-light-plain`.
One more grid step turns plain rammed earth into `thinlight`, `thicklight`, `thinheavy` or `thickheavy`.

These sat inside `primitive-framing`, which also carries the stick frame and the stone build signal.
They need neither: a plank frame takes them today, so they can ship before or after it.

## Design
**A packed earth infill.**
A `packeddirt` entry in `Infills`, shown as "Packed earth", consuming and dropping one `game:packeddirt` block with its vanilla texture.
It names `BlockMaterial: "Soil"`, as `clay` does, so it cools a cellar the way clay does (decision 0015) and takes the soil layer sounds.

**A rammed earth finish.**
A `rammed` entry in `Finishes`, shown as "Rammed earth", consuming `game:rammed-light-*` and dropping `game:rammed-light-plain`.
It is a flat face with no `Elements`, as `plate-{metal}` is.

**Display names.**
Each entry carries a `DisplayName` in `lang/en.json`, which `MaterialDisplayNameTests` checks against the real game assets.
The handbook's list of infills gains packed earth.

## Alternatives considered
- **Keep them in `primitive-framing`.** They share a theme and nothing else; splitting them out leaves the promised frame a one-session job.
- **Consume loose soil instead of the packed block.** Soil is a block with fertility variants and grass states, so a match on it would also take a right-click with any dirt in hand. The packed block is one code and its recipe sets the cost.
- **A `drypackeddirt` infill too.** A second earth infill that differs only by a sand ingredient and a texture; left until someone asks.

## Consequences & open questions
- Whether rammed earth's four patterns become finish styles or stay one look. A style needs a picker row (decision 0040), which is more than this session.
- Dropping `rammed-light-plain` for a patterned block loses the pattern, which costs nothing since the pattern step is one block in, one block out.
- Quantity: one block per layer, as cobblestone and polished rock take.
