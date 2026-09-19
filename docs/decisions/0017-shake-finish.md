# Shake finish

- Status: Accepted
- Created: 2026-09-16
- Reflects: `shake-finish` proposal; decision 0007's weatherboard elements; vanilla 1.21 `textures/block/wood/shingles/` and `blocktypes/wood/woodtyped/log.json`; graduated on branch `shake-finish`; playtest on branch `shake-finish` at fc0ad49

## Summary
Placed logs become a front finish of overlapping shake courses, one per wood, with hewn logs on the back, log-cabin style.
The geometry is decision 0007's stepped weatherboard; the shakes and their stagger come from vanilla's face-on shingle texture.

## Context
Decision 0003's finish catalogue has two rows needing their own geometry: planks as weatherboard (decision 0007) and logs as shakes.
Shakes are the last one.

The proposal pointed at `shingles/{wood}-side.png`.
That is the edge of a shingled roof seen side-on, not shakes seen face-on.
The face-on texture is `shingles/{wood}-top.png`: 32px, four staggered courses, a dark shadow line every 8px (at px ~7, 15, 23, 30).
Mapped onto a 16-voxel face, that is one course every 4 voxels, the same pitch as weatherboard, with its shadows where weatherboard's lips sit.

## Design

**One `FinishFamilies` template, key `shakes-{wood}`:**
matches `game:log-placed-*-ud` (a block, variant `wood`), consumes and drops 1 log, texture `game:block/wood/shingles/{wood}-top`, `BackTexture: "game:block/wood/debarked/{wood}"`, `Elements: { front: "front-shakes", back: "back-logs" }`.
`log.json` has a placed log for all twelve woods plus `aged`, and `aged-top.png` exists, so aged shakes come free.
There is no veryaged log, so there are no veryaged shakes.

**`front-shakes` (and `secondfront-shakes` on `cornerout`) are weatherboard's elements with position-mapped UVs.**
Weatherboard starts every board's texture at v=0, which suits plank grain.
Shakes sample the texture where the element sits (v = 16 − y), so the painted courses line up with the stepped lips and carry on across stacked walls.
The stagger between rows is already in the texture, so no per-shake elements are needed: 8 elements per face, same as weatherboard.

**The room side is a hewn log wall, `back-logs`.**
Nobody shingles the inside of a room, and a log finish that showed planks inside would repeat what planks already do.
So the back is four horizontal logs per block, on the same 4-voxel pitch as the shake courses: a half-depth base slab with a proud log in front of it every 4 voxels, leaving a groove where the logs meet.
Vanilla's `debarked/{wood}` texture already runs its grain horizontally, so the logs need no UV rotation; their room face samples by position like the shakes.

**A finish entry may carry `BackTexture`.**
A finish has one `Texture` for every face, so the back can't show different wood from the front without it.
`SidingWallTexSource` uses `BackTexture` for the `#back` slot when present and `Texture` otherwise; every existing entry is unchanged.
Reusing `back-boards` was ruled out: its room face is rotated 90° to stand plank grain upright, which also turns the shingle texture on its side.

All fourteen shingle textures are fully opaque.
The opacity test now includes log candidates and `BackTexture`, so a family entry pointing at a missing or translucent texture fails the build.
A second asset test checks every finish element group exists in both shapes and is in every `ignoreElements` list, since a missed one silently draws on the default mesh.

## Alternatives considered
- **8 courses of 2 voxels with per-shake staggered elements (the proposal).** Dozens of elements per face, and the painted texture already draws the stagger at a 4-voxel pitch that 2-voxel courses would fight.
- **The `-side` texture.** It shows the roof's edge, not shakes.
- **Plank boards on the back (`back-boards`).** Needs `BackTexture` anyway, and gives a log the same room side as planks.
- **Bark on the back (`bark/{wood}-h`).** Also horizontal and per-wood, but unpeeled bark is an odd thing to live with.
- **Firewood or planks as the held material.** Firewood isn't per-wood, and planks already mean weatherboard.
- **A C#-generated mesh with randomised shake widths.** The whole mesh pipeline is JSON shapes plus selective elements (decision 0007 rejected the same thing).

## Consequences & open questions
- Checked in play: the front of a straight wall shows upright shakes with the painted courses on the lips.
- Not yet checked in play: the log-cabin back, a cornerout's second leg, and courses continuing across a stacked wall.
- The first playtest's "sideways shakes" were most likely the back: shakes then used `back-boards`, whose 90° room face shows the shingle texture turned on its side without stretching.
  Face `rotation` on the front was tried and is no fix: it turns the texture inside each thin strip, so the strip samples 16 texels across 1 voxel and stretches.
