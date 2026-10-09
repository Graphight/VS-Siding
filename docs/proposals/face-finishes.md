# Face finishes

- Status: Draft
- Created: 2026-10-06
- Reflects: mod page comments https://mods.vintagestory.at/vssiding#cmt-243659, #cmt-243661 with its first screenshot, #cmt-245197, the reply offering a face mode (#cmt-245413) and the player's answer (#cmt-245601); decompiled 1.22.7 `BlockBehaviorDecor`, `WorldChunk.SetDecor`/`BreakDecor`/`AdjustSelectionBoxForDecor`, `ChunkTesselator.BuildDecorPolygons`, `SurfaceLayerTesselator`, `DecorFlags`, `ServerChunk`'s decor serialisation, `SystemMouseInWorldInteractions.ContinueBreakSurvival`; vanilla `cloth/wallpaper.json`, `overlay/plaster.json`, `cloth/rug.json`, `cloth/mediumcarpet.json`; `SidingModePicker.Rows`, `PlaceWallFrame`, `config/materials.json`'s `planks-{wood}`; decisions 0001, 0010, 0027, 0031, 0035, 0040, 0051, 0058; issue #81; not built, not played

## Summary
A player frames a house in full log blocks and wants a Siding finish on one face of a log: floorboards over a beam, siding on the room side of a post.
They asked for it as a "fill block", a thin wall or floor with the rest of its cell filled solid.
The proposal goes the other way round: the log stays vanilla's, and a finish goes onto one of its faces as a vanilla decor block, picked as a fourth option in the saw picker's first row.

## Context
**The request.**
"A 'fill block' sort of like Vs Roofings 'CTRL' to fill the block, and still have a flooring type on the top side, so I can have full block sides and everything else, but a proper floor inside the wall wthout seeing the log."
And again: "to be able to place logs and other solid blocks, going down like my original screenshot, which would allow finishing the top of the 'floor'/'Deck' with whatever you want, but provide the bottom as a full block to build off".
The first screenshot shows a house on log stilts with log beams under the floor's edge.

**The player's answer.**
Offered "a saw mode that replaces the current face on an existing block with a particular finish", which is this proposal, the player answered "definitely no full blocks. I was just saying I COULD solve it with a plank block", and went on to describe what Roofing's fill does under a roof frame.
That description is recorded in `walls-under-roofing`.
The player agrees that the mod should add no full block, and said neither yes nor no to the face mode.
The four screenshots on the two requests show log stilts and beams under a floor and a one-wide stairwell from above and below, and none shows a roof, so the request is still read here as floors and posts.

**The problem under it.**
A cell holds one block.
Where a log beam sits, a thin floor cannot, so the floor shows the beam's top; where a wall sits, the log post cannot.
The first report of it: "I like to frame my houses with logs, but I can't frame, and use your wall mod in the same block".
Decision 0063 answers the wall half from the other side, a thin wall that looks like a log.
This proposal answers the floor half, and the wall half for players who keep the real log.

**Vanilla already has a skin on one face of a block.**
A chunk keeps a dictionary of decors, one block id per position and face, with a sub-position and a rotation packed into the key (`WorldChunk.Decors`, `DecorBits`).
It is saved with the chunk and sent to clients by vanilla (`ServerChunk`, `Packet_ServerSetDecors`).
`WorldChunk.SetDecor` stores any block on any face and checks nothing.
The rule that a decor needs a solid face is in `BlockBehaviorDecor.TryPlaceBlock`, which calls the host's `CanAttachBlockAt` and fails on snow and ice.

**How one is drawn.**
`ChunkTesselator.BuildDecorPolygons` tesselates the decor block in the cell its face looks at, and only where the host's own face is drawn.
A `surfacelayer` decor is one flat quad 2/1024 off the face, textured from `all`, with its UVs turned by the key's rotation (`SurfaceLayerTesselator`).
A `json` decor is the block's shape under a rotation matrix per face, which is how `rug` and `mediumcarpet` have thickness.
`wallpaper` is the nearest vanilla neighbour: nine variants, `surfacelayer`, all six sides, thickness 0.
`overlay-plaster` is the same with `notFullFace`.

**What vanilla does with one afterwards.**
In survival, holding break on a face removes that face's decor first, 225 ms in, and drops what the decor block's `drops` name (`ContinueBreakSurvival`, `Block.OnBrokenAsDecor`).
Replacing the host breaks every decor on it.
`DecorThickness` widens the host's selection box.
None of that needs code from this mod.

**What a decor cannot carry.**
A decor has no block entity, so its look is its block code.
Every finish a face can take is therefore a real block, which is what decision 0001 avoided for walls.
It is one axis here, where a wall's variants would have crossed framing, infill and finish.
The cost is that `FinishFamilies` (decision 0010) expands at `AssetsFinalize`, after blocks are registered, so a face finish's variants have to be listed in JSON and cannot follow the families.

## Design
**One decor block, planks first.**
`vssiding:facefinish-{wood}`: a `Decor` behaviour on all six sides, `surfacelayer`, thickness 0, textured with the plank texture the `planks-{wood}` finish names, and dropping the planks it cost.
Its `wood` states load from the `block/wood` world property, as vanilla's plank blocks do, so a mod that adds its wood there gets a face finish and one that does not goes without.
Planks are the whole of both requests.
Stone, daub, shakes and plates wait until asked for.

**Flat, with no thickness.**
A skin on a beam's top face is drawn in the cell above it.
At thickness 0 it lies 2/1024 over the plane a thin floor's boards end on (decision 0051), which reads as one floor.
A `json` decor with a board's 1/16 would stand proud of that floor as a lip.
So `boards` and `hboards` carry over, as the key's rotation, and `weatherboard` does not.

**Placed by the plank already in hand.**
`PlaceWallFrame` is on every plank (decision 0058) and reads the first row's choice of wall, corner or floor from `SidingModePicker.Layout`.
A fourth option, `face`, joins `("vssidingFraming", { "wall", "corner", "floor" })` in `SidingModePicker.Rows`, with an icon drawn like the others (decision 0031).
In that mode a plank click on a block's face, with the build signal in the off hand, calls `SetDecor` with the wood's face finish and takes two planks, the cost of a plank finish on a wall.
The check is vanilla's own: the host's `CanAttachBlockAt` for that face, and not snow or ice.
A Siding wall or floor is not a host, since its faces take finishes as layers.
No other item needs a behaviour while planks are the only face finish.

**Board direction.**
The boards row picks it, `boards` or `hboards`, as it does on a wall (decision 0027), and the rotation goes into the decor key.
On a top or bottom face the same two options are the two directions a floor's boards run (decision 0051).

## Alternatives considered
- **The fill block as asked.** A layer filling the open 12/16 of a wall's or floor's cell with a block's material. Drawing it is one box. Everything else in the mod reads that space as open: furniture is hosted in it (decision 0035), collision, light and retention treat it as air, and `sidesolid` is false on the whole block, so a filled cell would still support nothing above it. The mod would have to handle full blocks in the cells where its code assumes open space.
- **A store of this mod's own, keyed by position and face**, drawn through the `TesselateBlock` transpiler that draws a guest wall (decision 0035). It would keep the families, the relief and every style. It would also need its own sync, save, break, drop and selection, which is what vanilla's decor layer already is.
- **A `json` decor with real board thickness.** Relief suits a log's side face; on a beam's top it is the lip above. Revisit for side faces if players ask for weatherboard on a post.
- **Every finish as a variant from the start.** Eight finish entries and ten families, four of the families crossed with every rock and two with every wood. Two comments asked for boards.
- **A different tool in the off hand.** It would keep plank clicks unambiguous, and the picker's first row already does: its options are exclusive answers to "what does this click put down".
- **Chiselling.** Vanilla's chisel has an add-material mode (`ItemChisel`'s `addmat`), so a player can put a plank layer on a log today. `chiselling-walls` is about turning a wall into a microblock, which is a different job.

## Consequences & open questions
- The player has not said the face mode is what they meant. Ask with a screenshot of a skinned beam before building past the first block.
- Whether a plank texture on a flat quad lines up with a Siding floor's boards beside it. Issue #81 lined vertical boards up with vanilla plank blocks, which suggests it will; not looked at.
- Chiselled hosts. `BlockEntityMicroBlock` takes decor through `IAcceptsDecor`, with its own storage, so a chiselled log needs that path or is left out at first.
- A decor is drawn only where its host's face is drawn, so a face against an opaque neighbour shows nothing. What a face shows against the open part of a Siding wall's cell is to be played.
- The creative inventory would list a face finish per wood unless hidden, and a player holding one could place it by vanilla's own decor click, with no planks spent.
- Breaking in creative removes block and decor together and drops nothing, as for any decor.
- A stick has `PlaceWallFrame` too. Whether a stick click in face mode does nothing or puts up a bark skin to match decision 0063's bark styles is open.
- The tooltip. A decor adds no line to its host's block info, so nothing says which finish is on a face.
- A ground floor under a diagonal wall with no digging (decision 0067 built the deck for an upper storey only).
The block below stays as it was and half of its top face is skinned, the triangle on the room side.
A `surfacelayer` decor is one flat quad over the whole face, so the triangle needs a `json` decor of stepped strips or a texture with a transparent half; neither is checked.
Whether one rotated variant covers the four orientations is not checked.
The cut edge lies under the panel's base, which covers 2 voxels either side of the diagonal.
