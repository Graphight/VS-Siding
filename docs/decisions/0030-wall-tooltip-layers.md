# 0030 — Wall tooltip names its layers

- Status: Accepted
- Created: 2026-09-20
- Reflects: branch `wall-tooltip-layers`, after decision 0029

## Summary
`SidingWallEntity.GetBlockInfo` names a wall's framing, its infill, each finished face and whether it seals — the first code to render a material's `DisplayName`.

## Context
`SidingWallBlock` had no block-info override, so looking at a siding wall said "Siding Wall" and nothing else.
Once a face is boarded the infill is hidden, and the infill is what decides whether the room behind it is a cellar or a warm room (decision 0015) — so there was no way short of breaking the wall to tell wattle from stone rubble.

Every material entry in `wall.json` already carries a `DisplayName`, and no code had ever resolved one: the mesh path wants `Texture`, drops want `Drops`.
This is the first code to render one, for all ~500 expanded entries.

## Design
The hook is `BlockEntity.GetBlockInfo`, not `Block.GetPlacedBlockInfo`.
Decompiling `VintagestoryAPI.dll` showed that `Block.GetPlacedBlockInfo` already calls `world.BlockAccessor.GetBlockEntity(pos).GetBlockInfo(forPlayer, sb)` inside a try/catch and appends the `blockdesc-` line afterwards — so one override on `SidingWallEntity` is enough, and the existing handbook pointer in `blockdesc-wall-*` survives untouched.

The pure computation is `SidingWallBlock.Describe`, an `internal static` sitting beside `ComputeRetention`.
It takes the entity's `Framing`/`Infill`/`Front`/`SecondFront`/`Back` keys, the three material dictionaries, and a `Func<string, string?> translate` delegate — the entity passes `Lang.GetIfExists`, so the tests need no loaded `Lang` and assert on plain strings.
`DescribeLayer` resolves a key's `DisplayName` through `translate`, falling back to a title-cased material key when the entry has no `DisplayName` or the key has no translation — `TitleCase` just capitalizes each hyphen-split word.
A missing framing or infill prints `vssiding:tooltip-no-framing` / `-no-infill`.

Faces are labelled by compass direction, reusing vanilla's `game:facing-north`/`-east`/`-south`/`-west`, which answers the proposal's open question about naming a cornerout's three finishable faces in player-facing terms.
`DescribeFaces` builds a direction→finish list from `layout` and `side`: `side`→`Front`, its opposite→`Back`, and for a `cornerout` also `CorneroutSecondFace[side]`→`SecondFront` and *that* direction's opposite→`Back` too.
Directions are then grouped by resolved finish name in first-appearance order (not adjacency), which is what keeps a cornerout's two `Back` directions on one line even though a `SecondFront` direction sits between them in the raw list.
A null group prints `vssiding:tooltip-unfinished`.

A seal line closes it out, straight from the existing `ComputeRetention(true, framing, infill, framings, infills)`: `1` → `vssiding:tooltip-sealed`, `-1` → `vssiding:tooltip-sealed-cool`, `0` → `vssiding:tooltip-unsealed`.
This is the line a cellar builder actually reads, and it cost nothing new — the same keys the rest of `Describe` already has.

## Alternatives considered
- **`GetHeldItemInfo` on the plank instead.** The mode picker already names the mode; what's missing is about the wall in the world, not the stack in hand.
- **A `/sidingwall` command.** `sidingroom` exists for room debugging, but a command nobody runs teaches nobody.
- **Showing the raw keys.** `oak`/`wattle` are internal; the dictionaries carry display names so they need not leak.
- **Nothing, and let the handbook explain.** The handbook explains the system. The tooltip answers "what is *this* wall", which no static page can.
- **A startup warning for a missing lang key, beside the ones `MaterialFamilies.Expand` already logs.** Rejected in favour of a build-time test: `VSSiding.Tests/MaterialDisplayNameTests.cs` expands the families against the real game assets and checks every `DisplayName` against `en.json` — it found zero gaps.
  That covers everything the mod itself ships, and the runtime tooltip already degrades to a title-cased key, so a per-entry `AssetsFinalize` warning would only restate at load what the test already proves at build.
  Revive it if another mod's patched-in material turns out to be a real support question.

## Consequences & open questions
- The client entity can arrive after the chunk mesh (see the comment in `SidingWallEntity.FromTreeAttributes`), so a wall may read unbuilt for a frame or two on a fresh chunk load — unverified in play.
