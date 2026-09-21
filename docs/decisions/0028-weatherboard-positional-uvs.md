# 0028 — Weatherboard samples the texture where the board sits

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `finish-style-choice`; `VSSiding.Tests/WallShapeGen.cs`'s `UvRule`; decisions 0007, 0021 and 0023; the first in-play look at the taper decision 0023 asked for

## Summary
The weatherboard groups switch from `UvRule.Course` to `UvRule.Positional`, so each lap samples its own slice of the texture instead of every course repeating the texture's top four pixel rows.
`UvRule.Course` had no other user and is deleted.

This supersedes decision 0023's choice of UV rule only.
0023's taper — four one-voxel steps per course, thick at the butt — is untouched and stays Accepted; only the rule that paints it changes.

## Context
Decision 0023 closed with "not yet checked in play", listing what to look at.
Checked in play, the wall reads as a repeating pattern: every block shows the same four texture rows four times over, so a wall of any size is one motif tiled sixteen times per block and again in every block above it.

The arithmetic is in the shape file rather than anyone's opinion.
`Course` sets `v = courseTop - y` where `courseTop` is the top of the board's own 4-voxel course, so v never leaves 0..4 — the texture's top quarter, full width, repeated.
Oak plank art has its grain variation spread down all sixteen rows, and three quarters of it was never drawn.

This is inherited intent, not a constraint.
Decision 0007 chose flat v so every board restarted its grain at v 0; 0023 kept that per-board restart when it split the course into steps, and rejected `Positional` on the grounds that "four visibly different boards per block is a bigger change than the taper asked for".
That was the right call for a change about geometry. It is the wrong call once the texture is the thing being judged.

## Design

**Every weatherboard group is `Positional`:** `front-weatherboard`, `secondfront-weatherboard` and `back-weatherboard`, in both `wall.json` and `cornerout.json`.
`v = (16 - y1, 16 - y0)` gives the sixteen laps of a block sixteen distinct one-voxel slices, top to bottom, mapping the texture exactly once over the block.
Because v is measured against the block's own 0..16 and the art tiles vertically, a stacked wall has no seam at the boundary — the same property the shake groups (decision 0022) have always relied on.

**Only v changes.**
`positional` and `course` were both gated on `vAxis == 'y'`, and u comes from `Span` either way, so the end faces and the lap edges are byte-identical to before.

**`UvRule.Course` is deleted, with `CoursePitch` and the `courseTop` branch.**
It had exactly one user — the 96 weatherboard rows — and a rule with no users is a rule nobody can check.
`UvRule` is back to two members, which unwinds 0023's closing note about a fourth member forcing the rule down onto the face.

**The golden test moved with it.**
`EachWeatherboardStepSamplesTheTextureOnceDownItsCourse` pinned 0023's four-slice repeat and failed the moment the rule changed, which is what it was for.
It is replaced by `EveryWeatherboardLapSamplesItsOwnSliceOfTheTexture`, asserting the sixteen slices on both the front and back groups.

## Alternatives considered
- **Leave it.** It is 0023's stated intent, and it is what a player is now looking at; "the doc says so" is not a reason for a wall to look like wallpaper.
- **Keep `Course` but widen the pitch to 16.** `courseTop` would then equal the block top and the rule would *be* `Positional`, with extra arithmetic in front of it.
- **Give each board a different texture.** The finish is one material with one texture; this is a UV question, not an art question.
- **Supersede 0023 wholesale.** Its taper is still what ships and still has the better argument in it. Superseding one clause and saying which is more honest than restating a decision to change a line of it.

## Consequences & open questions
- **Not yet checked in play either.** The same discipline 0023 asked for applies here: look at a stacked wall for the block boundary, a `cornerout`'s two legs, and a weatherboard wall beside a shake wall.
- The lap edges — the `up` faces that catch the light — are unchanged, so the relief reads exactly as before. Only the grain behind it moves.
- Back and front now differ in which texture rows land on which lap, because the back group laps the other way. That is correct: they are opposite faces of a wall, not mirror images of one board.
- If the sixteen slices read as *too* busy, the knob is the art, not the rule — a plank texture with less variation down its height will settle it without touching the shape.
