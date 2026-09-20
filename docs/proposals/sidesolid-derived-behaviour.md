# What `sidesolid: false` turns off

- Status: Draft
- Created: 2026-09-20
- Reflects: three bugs found by playtest rather than by reading (decisions 0002, 0015, and the liquid barrier in `window-frames`); `Vintagestory.API.Common.Block` decompiled from `VintagestoryAPI.dll` 1.21

## Summary
`wall.json` sets `sidesolid: { all: false }` for one rendering reason, and vanilla reads that same flag to answer seven unrelated questions.
Each time one of those has surfaced, it surfaced as a player-visible bug.
This proposal lists all seven, says which we have answered, and decides the rest deliberately instead of waiting for the next playtest.

## Context
Decision 0002 turned `sidesolid` off on every face so a 4/16-thick wall doesn't cull its neighbours' faces.
That is a statement about *rendering*. Vanilla treats it as a statement about *being a wall*.

Three times now we have found that out the hard way:

- **Room retention** (decision 0002). `Block.GetRetention` reads `SideSolid`, so a sealed wall sealed nothing. Found while building the first room; fixed with an override.
- **Room skylight** (decision 0015). Not `SideSolid` directly, but the same shape of problem: a vanilla behaviour derived from a property we had quietly changed. Found by a cellar spoiling food too fast.
- **Liquid barrier** (`window-frames`). `Block.GetLiquidBarrierHeightOnSide` derives from `SideSolid`, so every wall since the first release has leaked. Found by a player watching water pour through a finished wall.

Three for three, found in the world rather than in the source.
The mod is small enough that the remaining list is short and can simply be read.

## Design

**The complete list, from `Block` in `VintagestoryAPI.dll` 1.21.**
Every member that reads `SideSolid` or `SideOpaque`:

| Member | What it decides | Ours? |
| --- | --- | --- |
| `GetRetention` | whether a wall seals a room | **answered** — decision 0002 |
| `GetLiquidBarrierHeightOnSide` | whether a wall dams liquid | **answered** — `window-frames` |
| `CanAttachBlockAt` | whether a torch, ladder, sign or lantern can hang on it | open |
| `AllowSnowCoverage` | whether snow settles on its top face | open |
| `CanCreatureSpawnOn` | whether mobs spawn on its top face | open |
| `DisplacesLiquids` | whether placing it pushes water out of the cell | open |
| `SideIsSolid` | whether water renders an edge against it | open |

`SideOpaque` is the rendering half and is decision 0002's actual subject; it is listed only so the table is complete.

**Each open one gets a decision, not a fix.**
Some of these are correctly `false` and should stay that way — the point is to have decided, not to override everything.
First reading, to be confirmed in play:

- **`CanAttachBlockAt` is the one that matters.** A finished wall is a wall; you should be able to hang a lantern on it. Today you cannot, on any siding wall, and nobody has reported it because nobody has tried. Likely wants the same "sealed, and on a claimed face" test as retention.
- **`AllowSnowCoverage`** — an exterior wall's top face is 4/16 wide. Snow on it is plausible but marginal; leaving it off is defensible.
- **`CanCreatureSpawnOn`** — leave off. A 4/16 ledge is not a floor, and mobs appearing on wall tops would be a bug report of its own.
- **`DisplacesLiquids`** — placing a frame happens before it is filled, so a frame not pushing water out is right. Revisit only if packing infill into a flooded cell looks wrong.
- **`SideIsSolid`** — cosmetic, and only against water. Cheapest to leave and look at once.

**The general rule this should leave behind**, for `CLAUDE.md` or decision 0002's successor:
when the mod overrides a vanilla *property* to get a rendering result, grep the API for every consumer of that property before shipping, because vanilla overloads its properties across unrelated systems.
The grep is a one-liner against the decompiled API and would have caught all three of the bugs above in minutes.

## Alternatives considered
- **Set `sidesolid: true` and fix the culling another way.** The culling is why 0002 turned it off; turning it back on returns the original bug and breaks the thin-wall look.
- **Override everything defensively.** Half the list is correctly false; overriding blind would put mobs on wall tops and snow in odd places, and hide which behaviours we actually thought about.
- **Wait for playtests, as we have been.** It has cost three player-visible bugs, one of them shipped. The remaining list is seven rows long and already written down.
- **A `liquidBarrierOnSides` JSON attribute instead of the override.** Vanilla supports it, but it is static, and whether a wall dams depends on whether it is filled — which is block-entity state.

## Consequences & open questions
- `CanAttachBlockAt` is the only one likely to need real work, and it needs its own playtest: attaching to a *bare frame* should presumably fail, which means the same entity-state check as retention.
- This list is 1.21's. A game update can add a consumer, so the grep belongs in the update checklist rather than being done once.
- Nothing here blocks `window-frames`; the liquid barrier it fixes is the entry that prompted the list.
