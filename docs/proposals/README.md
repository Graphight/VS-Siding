# Proposals

An idea that's been thought through but not acted on. Mutable — edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

Build order to a playable prototype — each one should be demoable in game before the next starts.

1. `wall-shape-and-collision` — a thin, oriented, walkable-against wall block, pure JSON after the palisade. Includes the room-sealing test that could sink the mod.
2. `wall-layer-state` — framing/insulation/exterior keys on a block entity, looked up in attribute dictionaries; drops.
3. `layered-wall-mesh` — one shape, three texture slots, compositing done by the texture source; mesh cache per combination.
4. `in-world-build-flow` — Roofing-style: right-click planks to frame, shift-right-click to add layers.
