# Proposals

An idea that's been thought through but not acted on. Mutable — edit freely, argue in the doc, change your mind.

- One file per idea: `slug.md`, no number. Numbering happens on graduation.
- Same headings as a decision (see `../README.md`), so the two diff against each other.
- Acting on one **graduates** it: the decision takes the next `NNNN` in `../decisions/`, and the proposal file is deleted.

## Open

Build order to a playable prototype — each one should be demoable in game before the next starts.

## Later (not yet written up)

- `window-and-door-frames` — framed openings as extra frame-type tool modes, like Roofing's eaves and ridges. A closed solid door and a glazed window seal the room; an open door, an unglazed window, or a non-solid door (e.g. a gate) don't.
- `multiple-walls-per-cell` — both faces or a corner in one cell.
- `furniture-against-thin-walls` — placement is gated by cell occupancy (`Block.Replaceable`), not collision geometry, so nothing can go in the empty 3/4 of a wall's cell even though there's visibly room. Vanilla's `Decor` system doesn't cover it (thin/flat only, not a full `BlockEntity` like a cooking pot); the real fix is probably chisel-style voxel merging into one block entity, not true two-block coexistence.
- `framing-only-collision` — once a wall has framing but no infill, a player should be able to walk through the open studs (Roofing does this for unfinished roof sections). Out of scope until then: it needs per-entity dynamic collision boxes (decision 0002's boxes are static per `layout`/`side` variant today) and stud geometry nobody's designed yet. Build-flow is what first makes a framing-only wall state reachable in play, but it's its own architectural decision, not an interaction-flow detail.
- `cornerout-finish-faces` — `cornerout` now has a layered mesh (both legs share the same `Framing`/`Infill`/`Front`/`Back` state, so a corner's second leg mirrors whatever finish the primary face got). But `ResolveFinishFace` still only recognises a `wall` layout's hugged side and its opposite, so a corner's second leg (`GetRetention`'s `CorneroutSecondFace`) can never be *independently* finished - you can only ever choose the finish via the primary `side` face, and both legs get it. Needs a real design for whether independent per-leg finishes are wanted at all before it's worth adding entity state for it.
- `expand-material-catalogue` — only `oak` framing exists (decision 0003's starter set); any other plank wood type (`plank-aged`, `plank-birch`, ...) silently doesn't match and frame placement no-ops. Same starter-set gap applies to `Infills`/`Finishes`. Two directions worth weighing when this gets picked up: hand-author a `Framings`/`Infills`/`Finishes` entry per wood type/material (more entries, but each gets its own texture and stays simple to reason about), or find a programmatic way to derive entries from installed items/blocks (e.g. every `plank-*` wood variant, every `Block.Attributes.woodType` sibling) so third-party mods' own wood types are picked up automatically without a JSON entry per mod. The second is more powerful but needs a real design — what does a "material" even mean generically enough to auto-derive a texture and drop stack from an arbitrary item.
