# 0022 — A real shake profile

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `shake-profile` at commit ca115a1; `VSSiding.Tests/WallShapeGen.cs`; decision 0017; the `shake-profile` proposal
- Revises decision 0017's geometry choice: 0017's lap stands as written and is not edited here; this decision changes what `front-shakes` and `secondfront-shakes` model.

## Summary
Shake courses get modelled relief on their butt edges — three boxes per course at slightly different outward depths — instead of reusing weatherboard's flush lap unchanged.
Decision 0017's 4-voxel course pitch and position-mapped UVs are kept.

## Context
The `shake-profile` proposal claimed `front-shakes`, `secondfront-shakes` and `back-boards` were byte-for-byte copies of `front-weatherboard`, `secondfront-weatherboard` and `back` — worth -1,868 lines of pure deletion.
They are not copies.
They share box lists but differ in `faces`:

- `front-shakes` carries position-mapped UVs. The box at y 0-1 samples v 15→16 where weatherboard samples v 0→1, and so on up the face. That is decision 0017's central mechanism: it lands the painted courses on the stepped lips and carries them across stacked walls.
- `secondfront-shakes` differs from `secondfront-weatherboard` in 7 of its 8 elements, same reason.
- `back-boards` carries `"rotation": 90` on its outward face, standing plank grain upright on the room side. Decision 0017 names this.

The audit behind the proposal compared `from`/`to` and ignored `faces`. There were no lines to reclaim and nothing was safe to delete.

But the complaint underneath was true: shakes had weatherboard's silhouette. Same eight boxes, same 4-voxel steps. The shingles were painted on, not modelled. That is what this decision changes.

## Design

- The texture sets the pitch. `shingles/{wood}-top.png` is 32px with course shadow lines at rows 7, 15, 23, 30 — one course every 4 voxels on a 16-voxel face. That is the pitch decision 0017 already chose and it is unchanged.
- The texture's vertical joints are irregular, roughly every 6-7 texels (~3 voxels), at different positions in each course. So modelled vertical joints cannot be made to land on the painted ones. A wall carrying two disagreeing sets of joints reads worse than one. This is why the courses are not split sideways into separate shakes with gaps.
- Instead each course's butt edge is segmented along its run into three boxes whose outward faces sit at slightly different depths — 0, 0.05, 0.1 or 0.15 voxels recessed — so the butt line breaks up and reads as individual split shingles. Depths are literal numbers in the element table, never randomised, or the golden test would go flaky.
- Recessed, never proud: the butt's outward face never passes the block bound, so wall thickness is unchanged. The recessed course body stays at 0.5 voxels, so the relief is at most 30% of the lap step — deliberately subtle.
- Every shake box stays `UvRule.Positional`. That is what carries painted courses across stacked walls.
- Element names `front-shakes` / `secondfront-shakes` are unchanged, so the blocktype's `ignoreElements` lists and `SelectiveElements` need no edits.
- This was affordable only because of decision 0021. 59 changed lines in the element table produced 2,280 lines of JSON across the two shape files. By hand it would have been three element groups written out longhand — `cornerout` pays for every front profile twice.

## Alternatives considered
- **Split each course into separate shakes with modelled vertical joints.** The painted joints are irregular and per-course, so modelled ones would fight them.
- **Delete `front-shakes` and let shakes use `front-weatherboard`** (the proposal's fallback). Would have broken course alignment across stacked walls, which is the whole point of the position-mapped UVs.
- **Delete `back-boards`** (the proposal). Would have lost the 90-degree grain rotation on the room side.
- **Per-shake elements at a 2-voxel course pitch.** Already rejected by decision 0017 — dozens of elements per face, fighting a texture that draws its stagger at 4 voxels.

## Consequences & open questions
- Not yet checked in play. Every previous decision in this repo records a playtest; this one does not have one yet. What needs looking at: whether 0.15 voxels of relief reads at all at normal viewing distance, or reads as noise against the painted courses; an upright wall; both `cornerout` legs; stacked walls for course continuity; and a shake wall next to a weatherboard wall, since telling them apart was the entire point.
- The depths are a tuning knob in the element table, not a fixed result. If the relief is too subtle or too strong, it is four numbers to change and a `just shapes`.
- Element count per face rose from 8 to 16 for each of the three shake groups. `wall.json` went 2,460→3,012 lines, `cornerout.json` 4,289→5,393.
- If the playtest says the relief does not read, the fallback is the lap as decision 0017 left it, and that outcome needs its own decision superseding this one.
