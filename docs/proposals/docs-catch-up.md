# Docs catch-up

- Status: Draft
- Created: 2026-09-20
- Reflects: release polish planning, after decision 0028

## Summary
Bring `README.md` and `modinfo.json`'s description in line with what the mod actually does now, and make `CLAUDE.md`'s architecture section a map rather than a changelog of 28 decisions.

## Context
`README.md` still says **"Status: early scaffolding, no gameplay yet"** and "no custom heat simulation beyond that - a possible future extension, not a v1 goal", written before decisions 0004–0028 shipped the mesh, the build flow, four board styles, glazing, stone, masonry relief, the corner upgrade and the lighting fixes.
It's the first thing anyone reads on the repo, and it's false.

`modinfo.json`'s description is one line and fine, but names no gesture, so a player installing from ModDB still doesn't know about the saw.

`CLAUDE.md`'s architecture section has grown by accretion — each decision added a clause — and now runs to dense paragraphs that repeat what the decisions say.
It should say *where things live* and point at the decisions for why.

## Design
Three edits, no new files:

1. **`README.md`** — drop the status line, or say what's true: feature-complete, pre-release. Add a "how you build a wall" paragraph (the saw gesture, the four modes) and a "what a wall is made of" list (framings, infills, finish styles). Build-from-source and licence stay as they are. No feature table.
2. **`modinfo.json`** — the description gains the gesture in one clause, so the ModDB blurb stands on its own. Version and dependency floor belong to `release-readiness`.
3. **`CLAUDE.md`** — keep the file-to-responsibility map and the three architecture rules (attribute dictionaries not variants, families are templates, check every consumer before overriding a vanilla property). Compress the per-decision narration: the decisions directory is the changelog and `ls` is its index.

## Alternatives considered
- **A `docs/` guide for players.** Duplicates the handbook page, and rots the moment they disagree. The handbook is the one player-facing text; the README points at it.
- **Generating the material lists into the README.** 500 keys, regenerated on every family expansion. Name the families instead.
- **Rewriting the decision docs to match today.** Explicitly forbidden by `docs/README.md`: accepted decisions are immutable, supersede instead.

## Consequences & open questions
- Cutting `CLAUDE.md` down risks losing a hard-won gotcha that only survives as a clause there. The test: every sentence removed must exist in a decision doc. If it doesn't, it isn't a cut, it's a deletion.
- "Feature-complete" in the README is a claim we make once and then have to mean.
