# Window frames

- Status: Draft
- Created: 2026-09-16
- Rewritten: 2026-09-20, against `main` at 4726ed6 — the original predates decisions 0008, 0009, 0010, 0013, 0015, 0016, 0017 and 0018, and predates the merging requirement below
- Reflects: decompiled `VSSurvivalMod.dll` 1.21 (`BlockBehaviorDoor.GetRetention`, `BlockGlassPane.GetRetention`); vanilla `blocktypes/wood/woodtyped/door.json`, `blocktypes/glass/full-plain.json` and `full-colored.json`; `VintagestoryAPI.xml` on `MeshData.RenderPassesAndExtraBits` / `WithRenderpasses` / `AddRenderPass`; decision 0018 as landed on `main`

## Summary
Glass becomes an infill material, so a siding wall can be glazed.
Glazing seals the room like any filled wall but still lets light through, which needs the "sealed means opaque" rule from decision 0015 split in two.
Adjacent glazed cells merge in both directions, dropping every member between them, so a wall of glass has no posts or rails inside it.
Doors need no frame at all: vanilla already does the job.

## Context
The backlog entry bundled windows and doors as extra frame tool modes, with the rule that a closed solid door or a glazed window seals a room, and an open door, an unglazed window, or a gate doesn't.

**Doors turned out not to be ours.**
Decompiling `BlockBehaviorDoor.GetRetention`: vanilla doors report retention themselves, gated on the door's own `airtight` attribute (`door.json` sets it false only for `door-crude` and `door-ruined*`).
Whether an *open* door still seals is a world setting, `openDoorsNotSolid`, off by default.
A closed vanilla door panel sits in the outer 2/16 of its cell (`door.json`'s closed box is `z 0.875..1`), inside the 4/16 our wall occupies, so a door in a gap between two siding walls lines up with the wall's front plane when placed from the same side.
So a doorway is: leave two cells empty, put a wall above, place a vanilla door.
Overriding vanilla's sealing rules for doors and gates would fight a world setting players already have; not this mod's business.

**Windows are ours.**
Vanilla glass panes (`BlockGlassPane`) do seal (retention 1 across their plane), but they sit in the centre of the cell, not against a face, so they don't line up with a siding wall.

**Windows merge, and that's the point.**
Two windows side by side are one wider window; a grid of them is one sheet of glass, with no post and no rail anywhere inside the run.
Player agency beats carpentry realism here — if someone wants a glass curtain wall, they get one.
This is the same idea as decision 0008's stacked frames sharing plates, but without 0008's alternating cross-beam: a window run drops *every* internal member, in both directions.
Walls keep 0008 exactly as it is.

**What changed since the first draft.**
The original said the existing state already had the right shape and that glazing would cost almost nothing beyond a shape file.
Decisions since then make half of that untrue and half of it cheaper:

- Decision 0015 made a sealed wall absorb light (`ComputeLightAbsorption` returns 99 whenever `ComputeRetention` is non-zero) and Harmony-patched the room registry's skylight sample to zero out any `SidingWallBlock` cell that absorbs.
- Decision 0016 made a sealed wall emit side AO on every face, for the same reason.
- Decision 0018 added two client-side rendering patches — `SealedCellLightPostfix` rewrites a sealed cell's light, `SealedCellFaceLightPrefix` makes faces onto one take smooth lighting's flat path — and both gate on `SidingWallBlock.IsSealed`, which is that same absorption test.
- All three hang off one predicate — *sealed* — and a glazed window is the first thing in the mod that must be sealed *and* transparent. That coupling has to come apart before glass works at all.
- Decision 0010 (material families) means coloured and leaded glass are not "one more entry each"; they're one family entry for the lot.

Decisions 0008 (framing-only collision) and 0013 (break one layer) also arrived; they cost the `window` layout some bookkeeping and give it peeling for free, respectively.

## Design

### Part 1 — glass as an infill

This is the whole sealing-and-light question, and it needs no new block variant and no new shape file.
A glass infill in a plain `wall` fills the cavity between the posts: a picture window.

**The infill entry.**
```json
"glass-{color}": {
	Match: { type: "block", code: "game:glass-*", variant: "color" },
	DisplayName: "vssiding:infill-glass-{color}",
	Texture: "game:block/glass/{color}",
	BlockMaterial: "Glass",
	Transparent: true,
	Consumes: { type: "block", code: "game:glass-{color}", quantity: 1 },
	Drops: [ { type: "block", code: "game:glass-{color}", quantity: { avg: 1, var: 0 } } ]
}
```
Vanilla's `glass` block is one code with a `color` group — `plain` plus nine colours plus quartz — so one family entry covers the lot.
`Glass` is not in `ComputeRetention`'s cooling list, so a glazed wall returns retention 1, matching vanilla panes.

**`Transparent` splits "sealed" from "opaque".**
Today `ComputeLightAbsorption` is defined as `ComputeRetention(...) != 0 ? 99 : 0`, which is exactly the assumption glass breaks.
It gains one clause: a sealed cell whose infill entry sets `Transparent` absorbs 0.
Four things follow for free, because they all already read `GetLightAbsorption`:
- `RoomSunlight` (the 0015 patch) returns the real light level for a glazed cell, so a room with windows counts its window columns as sky — which is what vanilla does for glass panes, and what a player expects from a cellar with a window in it.
- `DoEmitSideAo` stops emitting for glazed cells, so a glazed window doesn't shade its neighbours' corners like a solid block.
- `IsSealed` goes false, so decision 0018's postfix leaves the cell's light alone and its prefix keeps the smooth path for faces onto it. A window's cell *should* hold the daylight coming through it, and the faces around it *should* be shaded smoothly — both patches exist to hide light a player isn't supposed to see, and through a window they are.
- Retention is untouched: the room still seals.

The first thing to check in play is that a glazed room is still a room, and that a glazed *cellar* correctly stops being much of a cellar.

**Render pass: set it on the mesh, not on the element.**
The first draft gave glass its own shape element carrying `"renderPass": 3`, and had the infill entry name it via `Elements: { infill: "infill-glass" }`, the way decision 0007's finishes do.
That does not survive Part 2: a window's infill needs its own geometry, and a per-material element name means authoring and ignoring a parallel `-glass` element for every one of them.
Instead, `OnTesselation` tesselates in two passes when the infill is `Transparent`: the infill elements alone into one `MeshData`, everything else into another, then fills the glass mesh's `RenderPassesAndExtraBits` with `EnumChunkRenderPass.Transparent` (`WithRenderpasses` allocates it, one `short` per quad) and `AddMeshData`s it onto the opaque mesh.
Same shape elements serve wattle and glass; only the pass differs.
The combined mesh is cached exactly as today, keyed as it already is on the material keys.

**Verify before building anything else in Part 1:** that `mesher.AddMeshData` routes a mixed-pass mesh to the right pools.
`RenderPassesAndExtraBits` is documented as what the terrain tesselator reads to pick each quad's mesh pool, but every wall mesh so far has been single-pass, so that is an assumption.
Check too whether `TesselateShape` leaves the array unset on the opaque mesh — `MeshData.AddMeshData` skips datasets that aren't set, so the opaque half may need filling with `Opaque` explicitly before the merge.
If the pass can't be mixed in one mesh, the fallback is two `mesher.AddMeshData` calls, which costs nothing structural.

**The texture opacity test has to learn about glass.**
`MaterialTextureOpacityTests` asserts every material texture is fully opaque, and `block/glass/plain` correctly is not.
`CollectTextureCodes` skips the opacity requirement for an entry that sets `Transparent`, and nothing else.

**Glass in a `cornerout` costs nothing extra** and is left allowed; it's a corner window.

### Part 2 — merging, and no `window` layout

The first draft of this proposal added a `window` layout: four more block variants, a shape file with a sill and a head rail, and a third tool mode.
Playtesting killed it.

**The player reached for glass as an infill and got a window.**
They never noticed the dedicated tool mode existed, because they didn't need it: a wall's own posts and plates already frame each pane, so glazed wall cells read as mullioned glazing straight away.
A mode you have to discover in order to get what you already have is pure tax.

**And the behaviours weren't the layout's to begin with.**
Merging and refusing finishes are properties of *glass*, not of a shape.
A glazed wall should merge with the glazed wall beside it and should not take a plank slab over it, whichever layout it sits in.
Attaching them to `layout == "window"` was a mis-assignment; they belong on `SidingWallBlock.IsTransparent`.

So there is no `window` layout. Glazing carries the behaviour:

- **`NeighbourJoins` merges in all four directions when the infill is transparent**, dropping every member between one glazed cell and the next, with no alternating cross-beam. Opaque fill keeps decision 0008 exactly. `ContinuesGlazing` requires the neighbour to be glazed too, so a pane against a wattle-filled cell keeps its post — that is a join between two walls, not one sheet.
- **A wall's two posts become `framing-left`/`framing-right`** so they can drop individually. "Left" is the `z = 0` end of the unrotated shape, which is `CorneroutSecondFace[side]` — the face counter-clockwise from `side`, where `cornerout`'s second leg sits. A test pins that against the rotation rather than leaving it to a comment.
- **A `cornerout` keeps its three posts** and merges only up and down. Corners are structural, and a corner has nothing to merge along.
- **Glazing takes no finish**, refused in `OnBlockInteractStart` rather than in `ResolveFinishFace`, so breaking is unchanged: `PeelLayer` still finds no finish on a glazed cell and peels the glass out (decision 0013).
- **Collision needs nothing.** Merging requires transparent infill, and any infill already switches collision to the full slab, so the merge flags can never reach `FramingBoxes`. The horizontal collision work that the staged plan called for turned out to be unreachable and was deleted.

**Walls are not watertight by default, and never were.**
Vanilla derives `GetLiquidBarrierHeightOnSide` from `SideSolid`, which decision 0002 turned off on every face so a thin wall doesn't cull its neighbours.
Every siding wall has therefore reported a barrier of 0 since the first release — wattle and stone as much as glass.
`SidingWallBlock` now overrides it and asks exactly what `GetRetention` asks: a wall that seals air seals water, glazing included.
Changing infill is block-entity state rather than a block change, so it also has to call `TriggerNeighbourBlockUpdate`, or the water beside it never re-evaluates and the wall only starts damming when something unrelated happens nearby.

## Alternatives considered
- **Door frame tool mode.** Vanilla doors already seal and already align (above); a frame would be decoration duplicating the posts either side.
- **Make gates and open doors not seal.** Vanilla decides this per door type and per world; overriding it from a wall mod surprises players who set `openDoorsNotSolid` deliberately.
- **Use vanilla glass panes in a wall gap.** They sit mid-cell, off the wall's plane, and look wrong.
- **Glass as a finish.** A finish is cosmetic and never seals (decision 0003); glazing is exactly what should decide whether the opening is sealed.
- **Glazing as a separate fifth entity field.** Glass fills the cavity; the infill slot already means "what fills the cavity".
- **Keep 0008's alternating cross-beam in a window stack.** A rail every second cell would band a tall glass wall and defeat the point; walls keep the alternation, windows don't.
- **A nine-patch infill (centre, four edge strips, four corners), each piece drawn on its own merge condition.** Needed only if the pane is cell-sized and the fillers have to avoid overlapping each other — two layers of transparent glass in a corner renders visibly darker. A full-cell pane behind the frame makes all nine collapse to one.
- **Glass gets its own shape element with `"renderPass": 3` on it,** as the first draft had. Per-element render pass is real (`shapes/block/farmland-fertilizer.json` uses it), but it forces a parallel `-glass` element for every infill element, in every shape, plus its `ignoreElements` entries. Setting the pass on the mesh costs about four lines and scales.
- **Derive `Transparent` from `BlockMaterial == "Glass"` instead of a flag.** It reads as cleverness at the JSON's expense, and the next transparent material wouldn't be glass. One explicit flag, read in one place.
- **Ship the `window` layout first, or only.** The shape file is the expensive half and the least certain; the light/retention split is the interesting half and stands alone.

## Consequences & open questions
- **Verify mixed render passes through `ITerrainMeshPool.AddMeshData` before anything else.** Everything in Part 1 assumes it.
- Decision 0018 fixed the floor-edge glow by darkening sealed cells and flattening the faces onto them. A glazed cell is deliberately outside both patches, so a sealed wall *beside* a window again borders a bright cell. Whether that reads as a window lighting the floor (wanted) or as 0018's bug coming back (not) is a playtest question, at stage 4.
- Decision 0018 notes a sealed cell whose open side is a doorway can show a bright sliver of the doorway's daylight. A merged run of unglazed windows is a large doorway, so the walls around one may show exactly that; if so, both want the same fix (take the darker of the open side and the room).
- With framing-only collision (decision 0008), an unglazed window is walk-through apart from its sill and head rail. Plausibly fine for a hole in a wall; check it doesn't feel like a bug.
- A merged run of unglazed windows is a hole with no posts in it at all — a doorway by another name. That's the agency the merge is for, but it's worth a look in play.
- Does a vanilla door placed from inside the house land flush with a wall that was built from outside? Placement orientation comes from the player (`BEBehaviorDoor.getRotateYRad`); playtest both sides and record which works.
- Windows in a `cornerout` aren't planned; corners are structural, and a corner can't merge with anything. Glass infill in one is allowed anyway (Part 1), which is a different thing.
- The mesh cache key grows by two booleans; a wall of glass is still only a handful of distinct meshes, but check the cache doesn't bloat with every side × merge combination in a large build.
