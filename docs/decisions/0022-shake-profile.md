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
- Instead each course is cut into four shakes along its run, and a shake keeps its own depth through both bands of the lap — butt at `d`, body at `0.5 + d`. That is what makes it read as a tile from butt to head rather than a banded strip with a flat body above it.
- Depths are 0, 0.1, 0.15, 0.2, 0.25 or 0.3 of a voxel, written as literal numbers in the element table. Never randomised, or the golden test would go flaky.
- Recessed, never proud: a shake's outward face never passes the block bound, so wall thickness is unchanged.
- Shake boundaries move course to course. That is the stagger, and it is the only thing keeping the four courses from reading as one vertical seam.
- The shakes abut with no gaps between them; the depth step alone carries the joint. A gap would open a line straight into the wall cavity.
- Splitting a course means the segments have to keep sampling the texture where they sit. Left alone, each of the four restarts at u 0 and the texture's first strip repeats across the run, erasing the painted grain variation the split was meant to add detail to. `RunAxis` on the generator (decision 0021) is what keeps the strip continuous.
- Every shake box stays `UvRule.Positional`. That is what carries painted courses across stacked walls.
- Element names `front-shakes` / `secondfront-shakes` are unchanged, so the blocktype's `ignoreElements` lists and `SelectiveElements` need no edits.
- This was affordable only because of decision 0021. The profile is 96 rows of element table producing 11,717 lines of JSON across the two shape files, and it was retuned once after a playtest by editing a table of depths and rerunning `just shapes`. By hand it would have been three element groups written out longhand, twice over — `cornerout` pays for every front profile on both legs.

## Alternatives considered
- **Split each course into separate shakes with modelled vertical joints.** The painted joints are irregular and per-course, so modelled ones would fight them.
- **Delete `front-shakes` and let shakes use `front-weatherboard`** (the proposal's fallback). Would have broken course alignment across stacked walls, which is the whole point of the position-mapped UVs.
- **Delete `back-boards`** (the proposal). Would have lost the 90-degree grain rotation on the room side.
- **Per-shake elements at a 2-voxel course pitch.** Already rejected by decision 0017 — dozens of elements per face, fighting a texture that draws its stagger at 4 voxels.

## Consequences & open questions
- Checked in play once, on the first cut: three segments per butt at up to 0.15 voxels. The relief was visible on close inspection but did not carry at a glance, and the flat course body above each butt still read as a band rather than as tiles.
- That playtest is what produced the profile above: four shakes per course instead of three, depth carried up through the body so a shake is a tile, and the range doubled to 0.3 voxels.
- The retuned profile has not itself been checked in play. What to look at: whether the tiles read as scales at walking distance; both `cornerout` legs; stacked walls for course continuity; and a shake wall beside a weatherboard wall, since telling them apart is the whole point.
- The depths remain a tuning knob in the element table. Too subtle or too strong is a handful of numbers and a `just shapes`, which is the property worth keeping.
- Cost: `wall.json` went 2,460→4,116 lines and `cornerout.json` 4,289→7,601, with 60 and 111 elements. Every box still declares all six faces, most of them buried, so the `buried-shape-faces` proposal is now worth more than it was.
- If the tiles read as noise, the fallback is the lap as decision 0017 left it, and that outcome needs its own decision superseding this one.
