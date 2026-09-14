# Proposals

An idea that's been thought through but not acted on. Mutable — edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

Build order to a playable prototype — each one should be demoable in game before the next starts.

1. `wall-shape-and-collision` — a thin, oriented, walkable-against wall block after the vanilla palisade, plus a `GetRetention` override so walls seal rooms.
2. `wall-layer-state` — framing + infill (seals rooms) and front + back finishes (looks only) as keys on a block entity; drops.
3. `layered-wall-mesh` — one shape, three texture slots, compositing done by the texture source; mesh cache per combination.
4. `in-world-build-flow` — Roofing-style: right-click planks with a saw in the off hand to frame, shift-right-click to add layers.

## Later (not yet written up)

- `window-and-door-frames` — framed openings as extra frame-type tool modes, like Roofing's eaves and ridges. A closed solid door and a glazed window seal the room; an open door, an unglazed window, or a non-solid door (e.g. a gate) don't.
- `face-specific-finishes` — materials that only make sense on one side (wallpaper, weatherboard). Both faces already take finishes.
- `multiple-walls-per-cell` — both faces or a corner in one cell.
