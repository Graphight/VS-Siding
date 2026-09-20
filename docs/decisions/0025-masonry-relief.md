# 0025 — Masonry finishes get relief

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `masonry-relief`; `VSSiding.Tests/WallShapeGen.cs`; decisions 0011, 0012, 0014, 0021, 0022, 0023 and 0024; the `masonry-relief` proposal

## Summary
Brick and ashlar get a running bond of proud units over a recessed mortar plane, aligned to the painted joints.
Cobblestone and drystone get a staggered grid of varied depths with no modelled joints.
Polished rock and daub stay flat.

## Context
Decisions 0022 and 0023 gave the two wood cladding profiles modelled relief.
Masonry was left a flat 1-voxel slab with a texture on it, so a brick wall beside a shake wall read as the cheap one.

Neither cladding treatment transfers.
A board and a shingle *lap*: there is a butt edge to break and a direction to taper.
Masonry does not lap — a brick wall is a grid of units in a plane with mortar between them, and rubble is a plane of irregular lumps.
Applying a course taper to brick would make it look like lapped siding painted to look like brick.

So this is a third geometric idea: relief as a grid of depths.

## Design

**The textures were measured first, as decision 0022 measured the shingle pitch.**
All are 32px over a 16-voxel face, so 2px is one voxel.

| texture | horizontal joints | vertical joints | reading |
| --- | --- | --- | --- |
| `clay/brick/four/running/cream1` | px 6‑7, 14‑15, 22‑23, 30‑31 | px 14‑15 / 30‑31 on even bands, 6‑7 / 22‑23 on odd | course every **4 voxels**, 1 voxel of mortar at its foot; **8-voxel** units, running bond, half-unit offset |
| `stone/brick/andesite1` (ashlar) | px 13‑15, 29‑31 | px ~14‑17 on band 0, 28‑31 on band 1 | course every **8 voxels**, same 8-voxel running bond |
| `stone/cobblestone/*`, `stone/drystone/*` | none — row means flat | none — column means flat | no grid at all |

**And here is the thing that made this cheap: the painted grid already sits on the voxel grid.**
Brick's courses land on 4-voxel boundaries and its joints on whole voxels, so modelled geometry can agree with the paint exactly rather than approximately.
That is the opposite of decision 0022's situation, where the shake joints were irregular and had to be left unmodelled.

**Regular masonry: mortar recessed, units proud.**
One full-face plane set back `MortarDepth` (0.25 of a voxel), with unit lips of that same thickness standing on the block bound.
The lip ends exactly where the plane begins, so decision 0024's `IsBuried` culls every lip's inward face without being asked.
Per face at brick's pitch that is 10 lips plus 1 plane — 11 boxes, against shakes' 32.

**One emitter, parameterised.**
`RunningBond(name, slot, coursePitch, unitWidth, depthAxis, outer, inner, runLo, runHi)` produces every regular group.
Ashlar is brick with `coursePitch` 8.
A back-slot group is the same call with `outer` and `inner` swapped, so the units stand proud toward x 4 instead of x 0.
Units are laid out against the wall's own 0..16 grid and then clipped to the run, because `cornerout`'s second leg starts at 1 and still has to put its joints where the texture paints them.

**Irregular masonry: depth only.**
`RubbleGrid` cuts the face into four 4-voxel rows, each cut into four cells at literal depths of 0 to 0.3 of a voxel, boundaries moving row to row for the stagger.
No modelled joints, because there are no painted ones to agree with — decision 0022's answer to the same problem.
Cells abut with no gaps; a gap opens a line straight into the wall cavity.
Depths are literal numbers, never randomised, or the golden test goes flaky.

**Recessed, never proud.** No box passes the block bound, so wall thickness is unchanged (decision 0022's rule).

**No fourth uv rule.**
The proposal wondered whether a grid mapped on both u and v would want one.
It does not: `UvRule.Positional` already maps v to the wall's height, which is what carries the courses across stacked walls, and `RunAxis` already maps u to the box's own position along the run, which is what stops each unit restarting the texture.
Decision 0023's question — whether the rule belongs on the element or on the face — is still open, but this work did not force it.

**Red brick moves onto the composite, which answers decision 0014's open question.**
`brick` was the one brick finish still on `game:legacy/clay/brick/red1`, a texture kept from before composite textures worked.
Relief forced the issue: the legacy tile runs its bond at the opposite parity *and* draws its mortar one pixel wide on the voxel boundary rather than filling a voxel, so it is on a half-voxel grid that whole-voxel geometry cannot land on.
Checked against the generated boxes, every one of its recessed voxels fell on a painted brick.
So `brick` now draws `four/running/cream1` with a `red1` overlay, exactly as the family draws every other colour, and one `front-brick` group serves them all.
Decision 0014 asked whether red should move "for consistency with the other colours" and said to decide by eye; the answer is yes, for a reason it could not have had.

**The entry keeps its `brick` key rather than being deleted in favour of the family's `brick-red`.**
A wall stores its finish key, and `SidingWallTexSource` resolves a key that has left its dictionary to a null texture.
Deleting the entry would therefore not merely change how existing red walls look, it would blank them.

**Polished rock and daub stay flat.**
Polished rock is smooth by definition and daub is a render. Relief on either would be relief the material does not have.

## Alternatives considered
- **Reuse the shake or weatherboard profile.** Would make brick look like lapped siding.
- **Leave masonry flat.** Defensible, and the fallback if the grid reads as noise. Rejected because the brick case turned out to align exactly and cost a third of what shakes cost.
- **Per-unit depth variation instead of recessed mortar.** The proposal's own open question answered: mortar sits back from the brick face in reality, and one recessed plane is far cheaper than per-unit boxes at varying depths.
- **One shared grid for brick and rubble.** The measurements say they are different jobs. They share the depth-grid *idea* and nothing else.
- **Per-cell random depths at mesh time.** Rejected as in decision 0022 — the golden test needs identical output every run.

## Consequences & open questions
- **Not yet checked in play.** What to look at: brick beside ashlar for the two pitches; a stacked brick wall for course continuity across the block boundary; both `cornerout` legs; a masonry wall beside a shake wall, since telling the three relief treatments apart is the point; and whether the rubble grid reads as rubble or as noise.
- **Rubble is the gamble.** It is one commit, and dropping it is the fallback if it reads as noise. Brick and ashlar are not — they agree with the paint by measurement.
- Cost: `wall.json` went 68 to 134 elements and `cornerout.json` 127 to 250. Roughly half of that is rubble's 4x4 grid across four slots.
- The depths in `RubbleDepths` and the cuts in `RubbleCuts` are a tuning knob, the same way the shake depths are: a table edit and a `just shapes`.
- `MortarDepth` is one constant. If 0.25 of a voxel does not read at walking distance, that is the number to turn.
