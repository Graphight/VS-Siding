# Proposals

An idea that's been thought through but not acted on. Mutable — edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

Build order to a playable prototype — each one should be demoable in game before the next starts.

## Later (not yet written up)

- `window-and-door-frames` — framed openings as extra frame-type tool modes, like Roofing's eaves and ridges. A closed solid door and a glazed window seal the room; an open door, an unglazed window, or a non-solid door (e.g. a gate) don't.
- `face-specific-finishes` — materials that only make sense on one side (wallpaper, weatherboard). Both faces already take finishes.
- `multiple-walls-per-cell` — both faces or a corner in one cell.
- `furniture-against-thin-walls` — placement is gated by cell occupancy (`Block.Replaceable`), not collision geometry, so nothing can go in the empty 3/4 of a wall's cell even though there's visibly room. Vanilla's `Decor` system doesn't cover it (thin/flat only, not a full `BlockEntity` like a cooking pot); the real fix is probably chisel-style voxel merging into one block entity, not true two-block coexistence.
- `framing-only-collision` — once a wall has framing but no infill, a player should be able to walk through the open studs (Roofing does this for unfinished roof sections). Out of scope until then: it needs per-entity dynamic collision boxes (decision 0002's boxes are static per `layout`/`side` variant today) and stud geometry nobody's designed yet. Build-flow is what first makes a framing-only wall state reachable in play, but it's its own architectural decision, not an interaction-flow detail.
- `cornerout-finish-faces` — a `cornerout` wall claims two faces for retention (`GetRetention`'s `CorneroutSecondFace`), but `ResolveFinishFace` only knows a `wall` layout's hugged side and its opposite, so the corner's second leg can never take a finish (always "click the wall's face"). Low urgency: `cornerout` has no layered mesh yet either (decision 0004), so a finish on it wouldn't render regardless. Needs a real design for what "front"/"back" mean on an L-shaped corner before it's worth coding, alongside the mesh work.
