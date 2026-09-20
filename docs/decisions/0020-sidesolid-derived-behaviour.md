# 0020 — What `sidesolid: false` turns off

- Status: Accepted
- Created: 2026-09-20
- Reflects: proposal `sidesolid-derived-behaviour`; `Vintagestory.API.Common.Block` from `VintagestoryAPI.xml`/`VintagestoryAPI.dll` 1.21; three bugs found by playtest rather than by reading (decisions 0002, 0015, 0019)

## Summary
`wall.json` sets `sidesolid: { all: false }` for one rendering reason, and vanilla reads that same flag to answer seven unrelated questions.
All seven are now decided: three were already answered by earlier decisions, `CanAttachBlockAt` gets an override here, and the remaining four are deliberately left false.

## Context
Decision 0002 turned `sidesolid` off on every face so a 4/16-thick wall doesn't cull its neighbours' faces.
That is a statement about *rendering*. Vanilla treats it as a statement about *being a wall*.

Three times we found that out the hard way, each in the world rather than in the source:

- **Room retention** (decision 0002). `Block.GetRetention` reads `SideSolid`, so a sealed wall sealed nothing. Found while building the first room.
- **Room skylight** (decision 0015). Not `SideSolid` directly, but the same shape of problem: a vanilla behaviour derived from a property we had quietly changed. Found by a cellar spoiling food too fast.
- **Liquid barrier** (decision 0019). `Block.GetLiquidBarrierHeightOnSide` derives from `SideSolid`, so every wall since the first release leaked. Found by a player watching water pour through a finished wall.

Three for three. The mod is small enough that the remaining list is short and can simply be read, so it was.

## Design

**The complete list**, every `Block` member reading `SideSolid` or `SideOpaque` in 1.21:

| Member | What it decides | Answer |
| --- | --- | --- |
| `GetRetention` | whether a wall seals a room | overridden — decision 0002 |
| `GetLiquidBarrierHeightOnSide` | whether a wall dams liquid | overridden — decision 0019 |
| `CanAttachBlockAt` | whether a torch, ladder, sign or lantern can hang on it | **overridden here** |
| `AllowSnowCoverage` | whether snow settles on its top face | left false |
| `CanCreatureSpawnOn` | whether mobs spawn on its top face | left false |
| `DisplacesLiquids` | whether placing it pushes water out of the cell | left false |
| `SideIsSolid` | whether water renders an edge against it | left false |

`SideOpaque` is the rendering half and is decision 0002's actual subject; it is listed only so the table is complete.

**`CanAttachBlockAt` is the one that mattered.**
With `sidesolid` off, nothing could be hung on any siding wall — no torch, no lantern, no ladder, on any wall the mod has ever shipped.
Nobody reported it because nobody tried.
A finished wall is a wall, so the override asks exactly what `GetRetention` asks: `ComputeRetention(ClaimsFace(blockFace), ...) != 0`.
A bare frame is not a wall and holds nothing; a `cornerout` holds on both its legs, because `ClaimsFace` already knows about the second leg (decision 0009).
`attachmentArea` is ignored — a sealed face is solid across its whole 16×16, so there is no sub-region to test.

**The other four are correctly false, and that is the decision.**
Overriding them blind would have been the easy move and the wrong one:

- **`AllowSnowCoverage`** — an exterior wall's top face is 4/16 wide. Snow on a 4/16 ledge is marginal at best, and it would have to line up with the neighbouring roof or wall top to look right. Off.
- **`CanCreatureSpawnOn`** — a 4/16 ledge is not a floor. Mobs appearing along wall tops would be a bug report of its own.
- **`DisplacesLiquids`** — a frame is placed before it is filled, so a frame that doesn't push water out is right: it is mostly air at that point. Revisit only if packing infill into a flooded cell looks wrong.
- **`SideIsSolid`** — cosmetic, and only against water. Cheapest to leave and look at once in play.

**The rule this leaves behind**, now in `CLAUDE.md`:
when the mod overrides a vanilla *property* to get a rendering result, grep the API for every consumer of that property before shipping, because vanilla overloads its properties across unrelated systems.
`grep -o 'M:Vintagestory.API.Common.Block.<Member>[^"]*' "$VINTAGE_STORY/VintagestoryAPI.xml"` gives the signatures in seconds, and reading `Block` for `SideSolid` would have caught all three of the bugs above in minutes.

## Alternatives considered
- **Set `sidesolid: true` and fix the culling another way.** The culling is why 0002 turned it off; turning it back on returns the original bug and breaks the thin-wall look.
- **Override everything defensively.** Half the list is correctly false; overriding blind would put mobs on wall tops and snow in odd places, and hide which behaviours we actually thought about.
- **Wait for playtests, as we had been.** It cost three player-visible bugs, one of them shipped. The list was seven rows long and already written down.
- **A `liquidBarrierOnSides` JSON attribute instead of the override** (decision 0019's territory). Vanilla supports it, but it is static, and whether a wall dams depends on block-entity state.

## Consequences & open questions
- Whether `blockFace` arrives as the wall's own face or the attaching block's is settled by playtest, not by the API docs, which say only "used by torches and other blocks". If attachment turns out inverted, the fix is `ClaimsFace(blockFace.Opposite)` and nothing else changes.
- Attaching to the *top* of a wall still falls through to vanilla, which says no. Nobody has asked for it.
- This list is 1.21's. A game update can add a consumer, so the grep belongs in the update checklist rather than being done once.
