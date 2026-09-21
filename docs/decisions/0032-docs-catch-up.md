# 0032 — Docs catch-up

- Status: Accepted
- Created: 2026-09-21
- Reflects: branch `docs-catch-up`, after decision 0031

## Summary
`README.md` and `modinfo.json` describe the shipped mod, and `CLAUDE.md`'s architecture section is a file-to-responsibility map instead of a per-decision narration.

## Context
The README still said "early scaffolding, no gameplay yet" and called room retention "a possible future extension, not a v1 goal", both written before decisions 0004–0031 shipped.
It is the first thing anyone reads on the repo, and it was false.
`modinfo.json` named no gesture, so a player installing from ModDB never learned about the saw.
`CLAUDE.md`'s architecture section had grown a clause per decision and retold what the decisions already say.

## Design
The README drops the status line outright rather than replacing it.
Release state lives in `modinfo.json` and on the ModDB page, so there is nothing to keep in sync and no claim to have to mean later.
It gains a "building a wall" section and a materials list, and points players at the in-game handbook page rather than duplicating it.

`CLAUDE.md` keeps the three rules that exist nowhere else in full — attribute dictionaries not variants (0001), `*Families` are templates (0010), find every consumer before overriding a vanilla property (0020) — and compresses everything else to a map with decision numbers.

One test governed every cut: a removed sentence had to already exist in a decision doc, or it was a deletion rather than a compression and stayed.
The glazing paragraph went because 0019 carries all of it; the `SideSolid` XML rule stayed because it is the gotcha itself, not a retelling.

## Alternatives considered
- **"Feature-complete, pre-release" in the README.** A claim you make once and then have to mean, stale the moment scope moves.
- **Pointing the README at `docs/proposals/README.md`.** Self-updating, but puts internal planning in the first thing a player reads.
- **A `docs/` player guide.** Duplicates the handbook page and rots the moment the two disagree.
- **Rewriting old decisions to match today.** Forbidden by `docs/README.md`: accepted decisions are immutable, supersede instead.

## Consequences & open questions
- The material lists in the README are named by family, not enumerated. A new family means one more line there; the ~500 expanded keys stay out.
- `CLAUDE.md` now cites decision numbers without restating them, so it is only useful next to `docs/decisions/`. That is the intended trade.
