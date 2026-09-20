# 0024 — Pruning buried shape faces

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `buried-shape-faces`; `VSSiding.Tests/WallShapeGen.cs`; the `buried-shape-faces` proposal; decisions 0002 and 0021

## Summary
The shape generator drops a face when a box **of the same element name** lies flat against it and covers it entirely.
That removes 62 of `wall.json`'s 404 declared quads and 130 of `cornerout.json`'s 754, all of them inside the cladding profiles.
The conditional burials the proposal named first were measured, priced at about six quads a cell, and deliberately left alone.

## Context

Every box in both shape files declared all six faces, including faces permanently sealed inside the box beside them.
Those become real quads in the chunk mesh, drawn every frame, invisible — and a siding wall is a block-entity mesh, so the cost is per wall cell in view, not per block type.

The proposal insisted on measuring before changing anything, and the measurement changed the answer.
Quads per built cell, counted by running `SelectiveElements` over each build combination and summing the `faces` entries of the elements it names:

| Build state | `wall` before → after | `cornerout` before → after |
| --- | --- | --- |
| bare frame | 24 → 24 | 42 → 42 |
| frame + wattle | 30 → 30 | 54 → 54 |
| frame + wattle, mid-stack | 30 → 30 | 54 → 54 |
| + plain slab finish, both faces | 42 → 42 | 78 → 77 |
| + weatherboard, both faces | 132 → 117 | 258 → 227 |
| + shakes, both faces | 252 → 205 | 498 → 400 |
| glazed, unmerged | 26 → 26 | 46 → 46 |
| glazed, merged all round | 2 → 2 | 22 → 22 |

**The cladding profiles are the whole cost.** A shakes-clad wall is 252 quads against a bare frame's 24, and `front-shakes` alone is 192 of them.
Anything that does not touch the profiles is rounding error, and the profiles are exactly where the same-name prune lands: `front-shakes` 192 → 149, `front-weatherboard` 96 → 81, `back-logs` 30 → 26 on `wall` and 60 → 48 on `cornerout`.

No frame-time reading was taken.
That needs the game running and a built scene; the quad counts were decisive on their own, and a reading stays the honest end-to-end check if the saving ever needs defending in real frames rather than in quads.

## Design

**Same name is the whole safety argument.** A name is a `selectiveElements` group, so boxes sharing one are drawn together or not at all — `front-shakes` is 32 boxes under one name, either all in the mesh or none of it.
That makes the prune unconditional: no reasoning about build state, join state, finish or glazing enters into it, and the rule cannot be wrong for a combination nobody thought to check.
The proposal expected per-combination pruning to be the interesting part. It turned out to be unnecessary for everything worth having.

**Cover has to be total.** Two shakes abut at a shared plane but sit at different depths, and the shallower one leaves a strip of its neighbour's face showing — that strip *is* the joint the profile is made of (decision 0022).
So the test is containment on both cross axes, not merely a shared plane. Partial cover keeps the face.
Concretely, in the bottom course: the box at z 0–5 keeps all six faces because both its neighbours are recessed behind it, while the box at z 5–9 loses north and south to neighbours that stand proud of it at both ends.

**It lives in the generator, so nothing is hand-maintained.** `EmitElement` filters through `IsBuried` before emitting; the element table is untouched, and `just shapes` rewrites both files.

**The golden test cannot check this, so the guards are written out by hand.** Decision 0021 spelled out why: that test compares the generator against a file `just shapes` regenerates from the same generator, so any new rule blesses its own output.
Three assertions carry the real weight — the literal face lists of the four boxes above, the per-combination quad totals in the table, and that no element is pruned down to an empty `faces` object, which would render as a hole rather than an error.

## Alternatives considered

- **The cross-group prune: `infill` against the posts, `framing-top`/`framing-bottom` against them, the finish slabs against the framing.** Measured and rejected. It is worth about six quads on a built cell — three per cent of a clad wall, the case that dominates — and it costs the shape table an imported fact it cannot check: that a non-glazed cell always draws both posts (`NeighbourJoins`) and that infill cannot exist without framing (`OnBlockInteractStart`). Both are true today. Neither is enforced anywhere near the generator, and a stale one renders as a hole in a wall, silently. Decisions 0002 and 0020 are the record of what a rendering assumption leaking across systems costs. Revive it if a profile-free wall ever becomes the common case, and carry a test asserting the invariant from `SelectiveElements`' side if so.
- **Faces coincident across block boundaries** — `infill-top`'s up face against the cell above's `infill-bottom`, for one. Two separate block-entity meshes, so one mesh cannot prune for the other, and the neighbour's presence is a runtime fact. Out of scope.
- **Per-combination meshes.** The cache already keys on materials and joins, so it could hold them, but this would multiply its entries to buy the six quads above. Not worth the entries.
- **Vanilla face culling.** Decision 0002 turned `sidesolid` off precisely so a thin wall does not cull its neighbours. This is culling *within* one mesh, which vanilla does not do.
- **Pruning by hand.** Re-introduces exactly the hand-maintenance problem decision 0021 removed.

## Consequences & open questions

- A new cladding profile gets the prune for free, because the rule reads the element table rather than the shape file.
- Both shape files shrank by roughly a third of their lines; the diff is machine-written either way.
- The saving is about a fifth of a clad wall's quads and nothing at all on a bare frame or a glazed pane, so it shows up in exactly the builds that have the most siding on screen.
- The depth numbers in the shake table are a tuning knob, and turning one now changes which faces are pruned as well as how the wall looks. The literal face-list test is what makes that visible rather than silent.
