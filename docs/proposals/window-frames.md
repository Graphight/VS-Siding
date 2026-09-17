# Window frames

- Status: Draft
- Created: 2026-09-16
- Reflects: planning session on `docs/plan-later-proposals`; decompiled `VSSurvivalMod.dll` 1.21 (`BlockBehaviorDoor.GetRetention`, `BlockGlassPane.GetRetention`) and `VintagestoryAPI.dll` (`ShapeElement.RenderPass`); vanilla `blocktypes/wood/woodtyped/door.json`

## Summary
A third frame tool mode, `window`, places a `window` layout: a framed opening whose infill slot takes glass.
Glazed, it seals the room like any filled wall; unglazed, it's an open hole.
Doors need no frame at all — vanilla already does the job.

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

**The existing state already has the right shape.**
A window is a frame whose cavity is glass instead of wattle.
Decision 0003 already says no infill means no retention, so "unglazed window doesn't seal" costs nothing.

## Design

**`layout` gains `window`**, so four more block variants, with the same collision box and `GetRetention` claim as `wall`.
`ResolveLayout` maps tool mode 2 to it; `PlaceWallFrame` adds a third `SkillItem`.
Layout is already a variant axis for geometry (decision 0002), and a window's geometry differs, so this stays consistent with decision 0001.

**`shapes/block/wall/window.json`**: `wall.json`'s plates and posts plus a sill and a head rail as more `framing` elements, and a glass pane element group.
Same four texture slots, so `SidingWallTexSource` doesn't change.

**Glass is an infill entry that names its own element.**
```json
"glass": {
	Texture: "game:block/glass/plain",
	BlockMaterial: "Glass",
	Elements: { infill: "infill-glass" },
	Consumes: { type: "block", code: "game:glass-plain", quantity: 1 },
	Drops: [ { type: "block", code: "game:glass-plain", quantity: { avg: 1, var: 0 } } ]
}
```
That's decision 0007's per-face `Elements` idea applied to the infill slot: `SelectiveElements` looks up `Infills[infill].Elements.infill` the way it already does for finishes.
`Glass` isn't a cooling material, so a glazed window returns retention 1, matching vanilla panes.

**`infill-glass` elements set `renderPass` in the shape JSON.**
`ShapeElement.RenderPass` exists in the API; glass needs a transparent pass or it renders as the brick x-ray bug from decision 0007.
First thing to verify: that `ITerrainMeshPool.AddMeshData` routes a mixed-pass mesh correctly, since every wall mesh so far has been single-pass.

**No restriction on which infill goes in which layout.**
Wattle in a window frame looks like a small odd wall; glass in a plain wall is a greenhouse wall.
Both are harmless, and a rule would be code nobody asked for.

**Finishes on a window are refused** with the wrong-face error.
A slab finish would cover the glass.
Trim around the opening is a nice later idea, not this session.

## Alternatives considered
- **Door frame tool mode.** Vanilla doors already seal and already align (above); a frame would be decoration duplicating the posts either side.
- **Make gates and open doors not seal.** Vanilla decides this per door type and per world; overriding it from a wall mod surprises players who set `openDoorsNotSolid` deliberately.
- **Use vanilla glass panes in a wall gap.** They sit mid-cell, off the wall's plane, and look wrong.
- **Glass as a finish.** A finish is cosmetic and never seals (decision 0003); glazing is exactly what should decide whether the opening is sealed.
- **Glazing as a separate fifth entity field.** Glass fills the cavity; the infill slot already means "what fills the cavity".

## Consequences & open questions
- The texture opacity test (decision 0007) will reject glass, correctly, for the opaque pass. It needs to skip entries whose `Elements` point at a transparent-pass element, and nothing else.
- With `framing-only-collision` shipped, an unglazed window is walk-through. Plausibly right for a hole in a wall; check it doesn't feel like a bug.
- Does a vanilla door placed from inside the house land flush with a wall that was built from outside? Placement orientation comes from the player (`BEBehaviorDoor.getRotateYRad`); playtest both sides and record which works.
- Windows in a `cornerout` aren't planned; corners are structural.
- Leaded or coloured glass is one more infill entry each.
