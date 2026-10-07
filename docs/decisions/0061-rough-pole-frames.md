# 0061 — Rough pole frames

- Status: Accepted
- Created: 2026-10-05
- Reflects: the playtests of decision 0058 on branch `feat/primitive-framing` and of decision 0060 on branch `feat/tent-walls`; branch `feat/rough-pole-frames`; `SidingWallEntity.FrameElements`/`CellAlternate`/`SelectiveElements`, `SidingWallTexSource.ResolveTexture`, `VSSiding.Tests/WallShapeGen`, `config/materials.json`, `blocktypes/wall.json` (`ignoreElements`), `WallShapeGenTests`, `FinishElementGroupsTests`, `SidingWallEntityTests`, `SidingWallTexSourceTests`; vanilla textures `block/wood/bark/oak`, `block/creature/bone` and `item/resource/rope`; decisions 0007, 0008, 0019, 0021, 0058, 0060; unit and end-to-end tests pass; played on 2026-10-06

## Summary
A stick frame is a plank frame in a darker texture, and a bone frame is one in a paler texture: the same squared posts and plates, ruler straight.
A framing entry can now name its own shape groups, and the stick and bone frames are drawn as rough poles with uneven posts, stubs and lashed joints.

## Context
Decision 0058 added the `sticks` framing.
In play its first texture could not be told from oak planks, and oak bark fixed the colour.
What was wanted beyond that was "a more rugged look with spurs and bits", and it was left out of 0058 as too much for that change.

Decision 0060 added the `bone` framing on the same shared elements.
In play it was "a little too clean edged": a bone frame is as square as a sawn one.

**A texture cannot do it.**
A post is one voxel wide on the face a player looks at, and vanilla's wood textures are 32 pixels across a block, so that face is two texels wide.
Two texels carry a colour and nothing else, which is why the bark and the planks differ only in brightness at wall scale.
Anything that reads as rough has to be in the silhouette.

**Every framing shared one set of elements.**
`SelectiveElements` asked the shape for `framing-top`, `framing-bottom`, `framing-left` and `framing-right` on a wall and `framing` on a corner, whatever the framing key was.
A finish entry names its own elements per face through `Elements` (decision 0007); a framing entry had no such key.

**The room there is.**
The frame fills x 1 to 3 of the wall's four voxels, and the finish slabs sit on x 0 to 1 and 3 to 4, so anything standing proud of the frame's thickness pokes through a finish.
The infill sits at x 1.5 to 2.5, half a voxel back from the frame on each side.
So a stub can run sideways from a post across the infill inside that half voxel, and a post can vary in width along the wall, and both stay under any finish laid later.

**What moves with the frame.**
A member drops where two cells join (decision 0008), a glazed cell swaps the frame for a bezel (decision 0019), and collision comes from `UnrotatedFramingBoxes`, which copies the plain members' boxes.

## Design
**`Elements` on a framing entry.**
An optional string on a framing entry, `Elements: "poles"` on `sticks` and on `bone`, a plain prefix.
`SidingWallEntity.FrameElements` reads it, defaulting to `framing`, and `SelectiveElements` takes it as a trailing `frame` parameter that defaults to `framing`.
Floors, decks and joists reach `SelectiveElements` through `SidingFloorEntity` and `DeckElements` without passing it, so they keep the plain groups with no edit there.
A wall asks for `poles-left`, `poles-right`, `poles-top` and `poles-bottom`, and a corner for `poles`, `poles-top` and `poles-bottom`.
Sticks and bones share the one set of groups: the unevenness, the overrun and the lashing are what a pole frame is, whatever the pole.
Guest walls tesselate through the same `OnTesselation`, so they follow with no change.
Every new group name is in the `ignoreElements` list of each `*-wall-*` and `*-cornerout-*` variant in `blocktypes/wall.json`, or an unbuilt cell would draw poles over the plain frame.
`EveryFramingElementGroupExistsAndIsIgnoredByTheDefaultShape` reads the prefixes from the real entries and checks both variants against both shapes and all eight lists, since a name a shape lacks draws nothing and reports nothing.

**Pole groups from `WallShapeGen`.**
The generator emits the groups into `wall.json` and `cornerout.json`, and `just shapes` rewrites them; `GeneratedShapeMatchesTheCommittedOne` pins them.
Posts sit a quarter voxel inside the frame's thickness, x 1.25 to 2.75 on a wall.
Each is cut into three lengths, and the middle one is a quarter voxel wider along the run, inward only.
The lengths inside the plate bands stay one voxel wide, and the left and right posts cut at different heights.
Nothing narrows along the run or the height, so no line of sight opens through the wall, and nothing leaves x 1 to 3, so nothing shows through a finish.

**The overrun is built as cheeks.**
The proposal had each plate overrun its post.
A plate keeps the plain box and gains quarter-voxel cheeks in the two skins of the frame's thickness (x 1 to 1.25 and 2.75 to 3) across the post.
Face on, the plate reads as running over the post.
One proud box that swallowed the post would put two same-facing faces on the plane at a run's end, and they would z-fight.
The plate's own north and south faces sit behind the posts and are left out of the group.

**Stubs.**
Each post gains two stubs, in the half voxel over the infill (x 1 to 1.5 or 2.5 to 3).
Each is a voxel high and runs one to two and a half voxels along the run from the post.
They lie between y 3 and y 13.5, clear of the lashing bands.
The two variants put them at different heights and on different sides of the infill.

**Lashing is cheeks on its own slot.**
A band of cheeks in the same skins, a quarter voxel wider along the run than the post, sits just under the top plate (y 13.5 to 15) and just over the bottom plate (y 1 to 2.5).
They are in the plate's group on a `lashing` slot, so they drop with the plate where two cells join.
`WallTextures` and `CornerOutTextures` declare the slot, and `SidingWallTexSource.ResolveTexture` answers it with `game:item/resource/rope`.
The texture is one constant in the texture source, not a key on the framing entry; it moves to `materials.json` when a second lashing material exists.

**Corners.**
A corner's corner post stays the plain 2 by 2 box.
It has two depth axes, and recessing it opens a line of sight between it and the infill, so it takes no cheeks and no lashing.
Only the two end posts are rough, each recessed on its own leg's depth axis, and the plates gain cheeks and lashing at the end posts only.
Its stubs stand on the end posts, at the far ends of the legs, so none meets another where the legs join.

**Two variants, picked by position.**
One pattern stamped on every cell reads as wallpaper along a run.
The generator emits `poles` and `poles2` for both layouts, the second with different cut heights, stub heights and stub sides.
The plates, cheeks and lashing of `poles2` are the first variant's renamed.
`FrameElements` appends `2` when the cell's alternate is odd.

**One hash, three readers.**
`SidingWallTexSource.Alternate(pos)` already picked each `*` texture's variant and keyed the mesh cache; the pole variant is its third reader.
`SidingWallEntity.CellAlternate` hands all three the whole hash while any layer's texture varies, its parity alone for a pole frame with no varying texture, and 0 otherwise, so the cache separates the two variants without multiplying by 120.
The infills count as layers there, which they did not before.
`packeddirt` is the one infill with a `*` texture, and it drew four textures under a plank frame and one under a stick frame; with the parity it would have drawn two, changing with the pole variant.
It now draws all four under every wall frame.

**Invariant tests.**
Three tests in `WallShapeGenTests`, each run for both layouts and both variants, pin the recipe so the numbers can be turned in play:
1. Every pole box lies inside the frame's thickness, and on a corner starts clear of the slab on the other leg's outer face.
2. Every pole group covers the mid-plane of the plain member it replaces.
3. Among groups drawn together, no two emitted faces with the same facing share a plane and overlap.

**Quad cost.**
`EachBuiltCellHandsTheTesselatorThisManyQuads` counts the quads one built cell hands the tesselator.

| Cell | Plain frame | Pole frame | Second pole frame |
| --- | --- | --- | --- |
| wall, bare | 24 | 144 | 144 |
| cornerout, bare | 42 | 158 | 158 |

A pole frame is six times a plain wall frame and nearly four times a plain corner frame, against a finished wall of 42 (daub) to 205 (shakes) quads.
The mesh cache keys the variant, so a long wall is a handful of meshes.

**What keeps the plain groups.**
A glazed wall keeps the shared bezel, since decision 0019's `glazing-*` members span the full cell edge.
A glazed corner has no bezel groups and takes the pole plates.
A floor's joists, a deck's rim and collision keep the plain frame; `UnrotatedFramingBoxes` is not touched, since stubs and cheeks are a quarter to two voxels inside the cell and a player cannot feel them.
Joists are seen from below in a cellar, where rough poles would show, so they are the first follow-up if this is liked.

## Alternatives considered
- **A hand-drawn texture with knots.** Two texels of post width show none of it, and the mod ships no texture of its own for walls.
- **Round poles from angled boxes.** A vanilla shape is boxes; an eight-sided post is four boxes per length, each rotated, and their faces z-fight with the infill and the finish slabs.
- **Roughness that stands proud of the wall.** It would poke through any finish, or need a rule that strips it once a face is finished.
- **A random pattern per cell, built at run time.** Each cell would mesh under its own key, and the cache exists so that a long wall is a handful of meshes.
- **Rough elements on every framing.** Planks are sawn timber; a square frame is right for them.
- **A second set of groups for bone, with knobbed ends in place of stubs.** A second table in the generator and two more groups per member, for a difference a quarter voxel deep; the shared groups looked fine on bone in play.
- **A separate `poles` framing beside `sticks`.** Two entries consuming the same stick would match the held item twice, and `MatchConsumes` returns the first.
- **One proud box for each plate.** Two same-facing faces on one plane at a run's end, which z-fight; the cheeks replace it.
- **A recessed corner post.** It opens a line of sight between the post and the infill on a corner's two depth axes.
- **The rope texture as a key on the framing entry.** One lashing material exists, so one constant serves.

## Consequences & open questions
Played on 2026-10-06, after all of it was built: "it all looks fine", and the textures were liked.
The playtest stop the plan set after the first wall cell was passed over, so the recipe was seen whole and not turned.
- The stubs, the uneven edge and the lashing read at wall scale, and the first-cut numbers stand.
- The cheeks read as a plate running over its post.
- A stub on a bone frame looks fine, so bone keeps the shared groups.
- Two variants are enough along a run.
- Nothing shows through a finish, and a stacked pair looks right where the plates and lashing drop at the join.
- The rope texture reads as lashing on bark and on bone.
- A glazed stick cell, which keeps the plain bezel, was not looked at beside a glazed corner, which takes the pole plates.
- Bark is furrowed top to bottom, so a plate shows the furrows across its length; vanilla ships `bark/oak-h` for a log lying down, and a plate could take it through a second framing slot.
- A floor keeps its own check in `SidingFloorEntity`, which still leaves the infill out, so packed dirt in a floor varies only under a plank joist frame or a plank finish.
- A pole frame costs six times the quads of a plain wall frame and nearly four times a plain corner frame; whether that matters on a long wall is unmeasured.
- A stick deck, stick joists and bone ones keep the plain groups, as the proposal said.
- `diagonal-walls` needs its own `poles-` groups if both ship, since a framing entry names groups by prefix.
