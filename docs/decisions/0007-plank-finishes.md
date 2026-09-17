# Plank finishes

- Status: Accepted
- Created: 2026-09-16
- Reflects: in-game playtest of the build flow merged in #7 (83d94a9)

## Summary

Add planks as a face finish: overlapping horizontal weatherboards on the front (exterior), flat vertical boards on the back (interior).
Fix the brick finish rendering see-through while we're in the finish code.

## Context

Playtesting #7 turned up two things.

**Brick finish is x-ray.**
Brick shows a blue overlay and you can see solid blocks behind it.
Daub, which goes through the same code path, looks fine.
The difference is the texture: `game:block/clay/brick/four/running/red1.png` has 840 of its 1024 pixels at alpha 163 (about 64% opaque), while `daub/browngolden/normal1.png` is fully opaque.
We render the finish as opaque geometry, so the partial alpha shows through.
Vanilla's `claybricks` block uses the same texture family without the problem, so vanilla handles it somewhere we don't; that mechanism isn't identified yet.

**Planks in hand build frames, not finishes.**
With a saw in the offhand, right-clicking a framed and infilled wall with planks places a new frame in the neighbouring cell.
That isn't a gesture conflict.
`SidingWallBlock.OnBlockInteractStart` looks planks up in `Finishes`, finds nothing, and falls through, so `PlaceWallFrame` takes the click.
Once a plank finish exists, the wall claims the click first.

## Design

### Brick

Investigate how vanilla `claybricks` renders `four/running/*` opaque (render pass, shader alpha handling, or a base texture underneath) and match it.
If that turns out to be awkward, point the finish at a fully opaque brick texture instead.
Either way, add a check that fails if a `Finishes`/`Infills`/`Framings` texture has non-opaque pixels, so the next material doesn't repeat this.

### Same gesture, context decides

Keep the saw-in-offhand right-click for everything (decision 0006).
Planks on a wall with framing and infill, clicking an unfinished face, apply the finish.
Planks anywhere else (air, ground, an already-finished face, a frame with no infill) still place a frame.
No shift-click: shift+right-click is vanilla ground storage, which places plank and brick piles.

### Finish geometry chosen by face

Today every finish is the same 1-voxel slab named `front` or `back` in the shape, with only the texture swapped.
Planks need different geometry per face, and one held item (`game:plank-oak`) has to mean weatherboard on the front and vertical boards on the back.
`MatchConsumes` returns the first dictionary entry that matches, so two separate entries both consuming planks can't work.

So a finish entry gains an optional per-face element name:

```json
"planks": {
	DisplayName: "vssiding:finish-planks",
	Texture: "game:block/wood/planks/oak1",
	Elements: { front: "front-weatherboard", back: "back-boards" },
	Consumes: { type: "item", code: "game:plank-oak", quantity: 2 },
	Drops: [ { type: "item", code: "game:plank-oak", quantity: { avg: 2, var: 0 } } ]
}
```

`SidingWallEntity.SelectiveElements` adds `Elements.front ?? "front"` instead of the literal `"front"` (same for back).
Existing daub and brick entries are unchanged.
The shape files gain the new named element groups alongside the existing `front`/`back` slabs.
The texture slot stays `#front`/`#back`, so `SidingWallTexSource` doesn't change.

### Weatherboard overlap

This is the same kind of hand-authored JSON as the corner framing, just more elements.
Each board is two elements inside the existing 1-voxel finish depth, stepped rather than rotated:

- a thin board body, e.g. x 0.5..1, 4 voxels tall
- a lip along its bottom edge, x 0..1, 1 voxel tall, standing proud of the board below

Four boards per block gives 8 elements per face, and the step reads as overlap from any angle that isn't dead-on.
Stepping avoids element rotation, which would poke tilted corners through the block edge or into neighbouring cells and z-fight where boards meet.
Board edges line up at block boundaries (4-voxel pitch into 16), so stacked walls continue the pattern.

Vertical interior boards need no overlap: one slab with the texture UVs rotated 90° (per-face `rotation`), or a few slabs with a small gap to show the joins.

## Alternatives considered

- **Shift-click to build frames.** Collides with vanilla ground storage for planks and bricks, and isn't needed once planks are a finish.
- **Separate `weatherboard` and `boards` finish entries.** Both consume planks, so one would always shadow the other.
- **Rotated boards for real overlap.** Truer to life, but tilted elements escape the cell bounds and z-fight; stepped elements look nearly the same at play distance.
- **Generating board geometry in C#.** Flexible, but the whole mesh pipeline is JSON shapes plus selective elements today; hand-authored elements stay consistent with that.

## Consequences & open questions

- Corners need their own weatherboard elements on both legs in `cornerout.json`, and the lip at the outside corner needs deciding: butt joint with a corner board, or let one leg's lips run past the other.
- Which way the grain runs in `planks/oak1` needs checking in game before deciding which face gets the UV rotation.
- Weatherboard lips stick out a fraction of a voxel less than a full slab would, which matters only if collision should follow the finish; decision 0002's boxes don't today.
- Absorbs the `face-specific-finishes` item from the proposals backlog: per-face `Elements` is the mechanism for one-side-only materials too.
