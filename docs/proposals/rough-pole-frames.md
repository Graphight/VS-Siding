# Rough pole frames

- Status: Draft
- Created: 2026-10-05
- Reflects: the playtest of decision 0058 on branch `feat/primitive-framing`; `SidingWallEntity.SelectiveElements`/`CacheKey`, `SidingWallTexSource.Alternate`, `SidingWallBlock.UnrotatedFramingBoxes`, `VSSiding.Tests/WallShapeGen`, `config/materials.json`; vanilla textures `block/wood/bark/oak` and `item/resource/rope`; decisions 0007, 0008, 0019, 0021, 0058; not yet played

## Summary
A stick frame is a plank frame in a darker texture: the same squared posts and plates, ruler straight.
The proposal lets a framing entry name its own shape elements, as a finish already can, and draws the stick frame as rough poles with uneven posts, branch stubs and lashed joints.

## Context
Decision 0058 added the `sticks` framing.
In play its first texture could not be told from oak planks, and oak bark fixed the colour.
What was wanted beyond that was "a more rugged look with spurs and bits", and it was left out of 0058 as too much for that change.

**A texture cannot do it.**
A post is one voxel wide on the face a player looks at, and vanilla's wood textures are 32 pixels across a block, so that face is two texels wide.
Two texels carry a colour and nothing else, which is why the bark and the planks differ only in brightness at wall scale.
Anything that reads as rough has to be in the silhouette.

**Every framing shares one set of elements.**
`SelectiveElements` asks the shape for `framing-top`, `framing-bottom`, `framing-left` and `framing-right` on a wall and `framing` on a corner, whatever the framing key is.
A finish entry names its own elements per face through `Elements` (decision 0007); a framing entry has no such key.

**The room there is.**
The frame fills x 1 to 3 of the wall's four voxels, and the finish slabs sit on x 0 to 1 and 3 to 4, so anything standing proud of the frame's thickness pokes through a finish.
The infill sits at x 1.5 to 2.5, half a voxel back from the frame on each side.
So a stub can run sideways from a post across the infill inside that half voxel, and a post can vary in width along the wall, and both stay under any finish laid later.

**What moves with the frame.**
A member drops where two cells join (decision 0008), a glazed cell swaps the frame for a bezel (decision 0019), and collision comes from `UnrotatedFramingBoxes`, which copies the plain members' boxes.

## Design
**`Elements` on a framing entry.**
An optional string, `Elements: "poles"` on `sticks`, read by `SelectiveElements` as the prefix in place of `framing`, so the wall asks for `poles-left` and the corner for `poles`.
Entries without it keep `framing`, so no plank frame changes.
`CacheKey` already carries the framing key, so a rough frame meshes under its own key with no change there.

**Pole groups from `WallShapeGen`.**
Each `poles-` group holds the member it replaces, broken into two or three lengths that differ by a quarter voxel in width so the edge is not one straight line.
A post gains one or two branch stubs lying in the half voxel over the infill, and each plate overruns its post by the post's width, the way a lashed pole does.
The groups keep the plain groups' names after the prefix, so every join in decision 0008 drops the same member it drops today.

**Two variants, picked by position.**
One pattern stamped on every cell reads as wallpaper along a run.
The generator emits `poles` and `poles2`, and the entity picks between them with `SidingWallTexSource.Alternate(pos)`, the hash that already varies plank textures per cell and already keys the mesh cache.

**Lashing at the joints.**
A thin band in vanilla's `item/resource/rope` texture wraps each post where a plate meets it, under a new `#lashing` slot the texture source answers.
It is the one new slot in the shape, and it is dropped with the plate it belongs to.

**Collision stays the plain frame's.**
Stubs and overruns are a quarter to one voxel, inside the cell, and a player cannot feel them.
`UnrotatedFramingBoxes` is not touched.

**Walls and corners only, for now.**
A glazed cell keeps the shared bezel, and a floor's joists and a deck's rim keep the plain groups.
Joists are seen from below in a cellar, where rough poles would show, so that is the first follow-up if this is liked.

## Alternatives considered
- **A hand-drawn texture with knots.** Two texels of post width show none of it, and the mod ships no texture of its own for walls.
- **Round poles from angled boxes.** A vanilla shape is boxes; an eight-sided post is four boxes per length, each rotated, and their faces z-fight with the infill and the finish slabs.
- **Roughness that stands proud of the wall.** It would poke through any finish, or need a rule that strips it once a face is finished.
- **A random pattern per cell, built at run time.** Each cell would mesh under its own key, and the cache exists so that a long wall is a handful of meshes.
- **Rough elements on every framing.** Planks are sawn timber; a square frame is right for them.
- **A separate `poles` framing beside `sticks`.** Two entries consuming the same stick would match the held item twice, and `MatchConsumes` returns the first.

## Consequences & open questions
- Bark is furrowed top to bottom, so a plate shows the furrows across its length; vanilla ships `bark/oak-h` for a log lying down, and a plate could take it through a second framing slot.
- Whether stubs a quarter voxel deep are visible at all at wall scale, or only the uneven edge and the lashing are; one cell in play answers it before the rest is built.
- Whether two variants are enough to break the pattern along a run of ten.
- A corner's three posts are always drawn, so its stubs must not meet where two legs join.
- `WallShapeGenTests` pins the committed shapes to the generator, so `just shapes` has to run in the same change.
- `tent-walls` adds a bone framing that would want its own look by the same key.
