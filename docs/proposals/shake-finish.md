# Shake finish

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; decision 0007's weatherboard elements; vanilla 1.21 `textures/block/wood/shingles/`

## Summary
Logs become a front finish of overlapping rows of hand-split shakes, one per wood type.
Same stepped-element geometry as decision 0007's weatherboard, with the vanilla per-wood shingle texture.

## Context
Decision 0003's finish catalogue has two rows needing their own geometry: planks as weatherboard (shipped in decision 0007) and logs as shakes.
Shakes are the last one.

Vanilla already ships `block/wood/shingles/{wood}-side.png` for every wood, used by its own roofing blocks.
So the texture is free and per-wood, and the geometry is a variation on elements that already exist.

Depends on `material-families` for the per-wood entries.

## Design

**One `FinishFamilies` template, key `shakes-{wood}`:**
held `game:log-placed-{wood}-ud` (a block), quantity 1, texture `game:block/wood/shingles/{wood}-side`, `Elements: { front: "front-shakes", back: "back-boards" }`.
The back reuses decision 0007's flat vertical boards: nobody shingles the inside of a room.

**`front-shakes` element group, in both `wall.json` and `cornerout.json`.**
The same stepped lip-plus-body trick as `front-weatherboard`, inside the same 1-voxel finish depth, so it never escapes the cell or z-fights (decision 0007's reasoning holds).
What makes it read as shakes rather than weatherboard:
- Shorter courses: 8 rows of 2 voxels instead of 4 rows of 4, still lining up at block boundaries.
- Each course split into staggered shake widths, offset by half a shake on alternate rows, so the vertical joints don't line up.

That's a lot more elements than weatherboard (a few per row, times eight rows).
If the element count visibly costs frame time on a big build, drop to 4 courses before reaching for anything cleverer.

If `cornerout-second-front` has shipped, the cornerout also needs `secondfront-shakes`.

## Alternatives considered
- **Firewood or planks as the held material.** Firewood isn't per-wood, and planks already mean weatherboard. A log reads as "raw timber you split".
- **Texture-only shakes on the flat slab.** The shingle texture alone looks like a flat tiled print from any angle; the step is what sells it, as with weatherboard.
- **A C#-generated mesh with randomised shake widths.** Nicer variety, but the whole mesh pipeline is JSON shapes plus selective elements (decision 0007 rejected the same thing).

## Consequences & open questions
- Check `log-placed-{wood}-ud` exists for every wood the family matches; some woods only have resin variants.
- Check the shingle texture's alpha against the opacity test before authoring geometry; if it fails, the test says so up front.
- Aged/veryaged shingle textures exist (`aged-side.png`); whether there's a matching held log is a question for the session.
