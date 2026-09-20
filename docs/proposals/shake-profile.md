# Shakes get their own profile

- Status: Draft
- Created: 2026-09-20
- Reflects: repo-wide over-engineering audit on branch `window-frames` at fc5caac; `shapes/block/wall/wall.json` and `cornerout.json`; decisions 0007 and 0017

## Summary
`front-shakes` is a byte-for-byte copy of `front-weatherboard`, and `back-boards` is a copy of `back`.
Shakes therefore render as weatherboard with a shingle texture on it.
Give shakes the overlapping tapered profile they were named for, and delete the two copies that only exist to hold a name.

## Context
Decision 0007 let a finish name its own shape elements (`Elements: { front: ..., back: ... }`), so cladding profiles could differ per material.
Decision 0017 added shakes and gave them `front-shakes` and `back-logs`.

`back-logs` is real: five boxes, a genuinely different profile.
`front-shakes` is not. An audit comparing element geometry found three exact duplicates:

| Element | Duplicate of | Boxes | Shape file |
| --- | --- | --- | --- |
| `front-shakes` | `front-weatherboard` | 8 | `wall.json`, `cornerout.json` |
| `secondfront-shakes` | `secondfront-weatherboard` | 8 | `cornerout.json` |
| `back-boards` | `back` | 1, 2 | `wall.json`, `cornerout.json` |

The texture comes from the `#front` slot, resolved per material by `SidingWallTexSource`, not from the element.
So `front-shakes` contributes nothing that `front-weatherboard` would not, and the planks finish naming `back-boards` is naming the default `back` by another name.

Nothing renders differently today if all three are deleted.
That is the point: **the JSON claims a distinction the geometry does not make.**
A player looking at a shake wall is seeing weatherboard.

Measured cost of the copies: **1,868 lines** across the two shape files (623 in `wall.json`, 1,245 in `cornerout.json`).

## Design

**Delete `back-boards` outright.**
The planks and plank-family finishes drop `Elements.back`, falling through to `back` as every other finish already does.
Remove it from both shape files and from every `ignoreElements` list.
Pure subtraction, no visual change.

**Give `front-shakes` and `secondfront-shakes` a real profile.**
Shakes are split shingles: short, overlapping, tapered, laid in courses with a visible butt edge — not the long horizontal boards weatherboard draws.
The existing weatherboard profile is eight boxes alternating `x 0..1` and `x 0.5..1` up the face, a simple lap.
A shake profile wants more courses, a shallower taper, and a broken vertical joint so the courses don't read as continuous planks.
Exact box list is a modelling job, to be done against the game's own shingle texture and looked at in play before it is called done.

**Keep the element-name mechanism.**
Decision 0007 is vindicated by this, not undermined: the mechanism is right and one material simply never used it. `back-logs` is what it looks like when it is used properly.

**A test that would have caught this.**
`FinishElementGroupsTests` checks that every named element *exists* and is *ignored*. It cannot see that two names hold identical geometry.
Add an assertion that no two distinct element names in a shape have the same box list, with an explicit allow-list if a deliberate alias ever turns up.
That is a handful of lines and it fails loudly the next time a profile is copied and not changed.

## Alternatives considered
- **Delete `front-shakes` and let shakes use `front-weatherboard`.** Honest and -1,868 lines, but it gives up a finish the mod advertises as distinct. Worth doing only if the modelling turns out not to be worth it — in which case the shakes finish should lose its `Elements` entry and be described as a weatherboard texture variant.
- **Leave it; it is only JSON.** It is 1,868 lines of JSON that has to be kept in sync by hand, and it hides a feature that does not work.
- **Generate the shape files.** See below; separable and bigger.

## Consequences & open questions
- **A shape generator is the obvious follow-on and deliberately not in scope here.** These files are 2,462 and 4,291 lines of machine-shaped JSON that nobody hand-edits — every change in the `window-frames` session was made by script. A small generator (profiles and repeated courses as data, faces and uv derived) would make a shake profile a few lines instead of a few hundred, and would make the duplicate impossible to write. It needs its own proposal, including whether the generated files stay committed.
- The `-1,868` lines only land in full if shakes end up sharing weatherboard's profile. Giving shakes a real profile spends some of it back, and that is the right trade.
- `cornerout.json` carries `secondfront-*` copies of every front profile, so each new profile costs three element groups, not one. Another argument for the generator.
