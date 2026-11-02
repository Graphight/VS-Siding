# CollapseStory not supported

- Status: Accepted
- Created: 2026-11-01
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-245929 and issue #110; decisions 0001, 0020, 0035; CollapseStory 0.8.9 as shipped (its JSON patches and mod page; no CollapseStory DLL decompiled); vanilla `BlockBehaviorUnstableFalling` read decompiled; `VSSiding.E2E.Tests/CollapseStoryScenarios.cs` and `docs/compat/collapsestory.md`, probed on 2026-11-01 against game 1.22.7 with `just probe collapsestory`; branch `collapsestory-probe`

## Summary
CollapseStory adds structural physics to the game, and a player asked for Siding to work with it.
Siding changes nothing for it: walls and floors do not get `StructuralLoad`.
A probe with both mods loaded shows walls and floors stand, and shows two clashes: a block set on a wall falls, and some furniture cannot be hosted in an upper storey.

## Context
CollapseStory 0.8.9 is a closed-source beta for game 1.22 that depends on Expanded Matter (`em`).
A block takes part only if CollapseStory patches its server-side `StructuralLoad` or `UnstableFalling` behavior onto the block's file.
The material values (mass, crush strength, bending strength) live in its DLL.
Its page says a modded block it does not cover "won't collapse".
Its author has declined a compat for VS Roofing, because of the number of shape and material combinations to give values to.

Siding's wall and floor are one block each, with framing, infill and finishes stored in the block entity (decision 0001).
CollapseStory reads a block, so opting them in would give every wall one mass and one strength, whatever it is built from.

## Design
**Siding is not supported with CollapseStory.**
Walls and floors get no `StructuralLoad`, and no code or patch in Siding names CollapseStory.
Issue #110 stays open in the backlog, in case the work is taken up later.

**A probe records what happens with both mods loaded.**
`CollapseStoryScenarios` runs under `just probe collapsestory` and pins each result below.
1. `Wall_Should_Stand_When_ItIsFramedFilledAndFinished`: a wall of oak, blue clay and oak on planks stands with its layers intact after 200 ticks.
2. `CoveredBlock_Should_Fall_When_PlacedOnAWall`: planks placed on a wall are gone after 200 ticks, and the wall stays.
CollapseStory gives planks `StructuralLoad` and does not count a siding wall under them as support.
Vanilla planks never fall, so this is CollapseStory's doing.
3. `Wall_Should_Stay_When_RaisedOnAWall`: both walls stay.
4. `Floor_Should_Stay_When_ItSpansARoomBetweenTwoWalls`: with the planks under a thin floor removed across three cells between two walls, all three cells stay.
5. `Cabinet_Should_BeRefused_When_HostedOverAWall`, `ScrollRack_Should_BeRefused_When_HostedOverAWall`, and the two `_Should_FallAndLeaveItsWall_When_ThePlanksUnderItsWallBreak`: CollapseStory gives vanilla cabinets, scroll racks, chests, trunks, crates, bookshelves, lanterns, doors and trapdoors the vanilla `UnstableFalling` behavior (`patches/collapsestory-unstablefalling-fixtures.json`); without it none of them have it.
For a block that lists no `attachableFaces`, that behavior's `CanPlaceBlock` fails with `requiresolidground` unless the block below answers `CanAttachBlockAt` on its top, and a siding wall does not.
So a cabinet or scroll rack cannot be hosted in a wall cell over another wall, which is an upper storey.
Hosted in a wall standing on planks, it falls when the planks under the wall are broken, and the wall comes back with its layers and its guest record cleared, so no wall is lost.
Lanterns, doors and trapdoors list `attachableFaces` on all six faces, and antler mounts and torch holders on the four sides, so for them the behavior passes when any listed neighbour takes the attachment; they were not probed.
6. `Wall_Should_Stay_When_ThePlanksUnderItAreBroken`: breaking the planks through the player's break path leaves the wall standing in the air.

The probe's hosting table lists CollapseStory's own blocks.
Its building blocks (mortared bricks, cobble, mud brick, tile and the like) are refused for solid sides, as any full building block is.
Its two support beams are not hosted either: the stick beam is refused as replaceable, and the metal beam failed to place, for a reason the probe does not record.
That file is rewritten by each probe run, which is why the findings are recorded here.

## Alternatives considered
- **Opt walls and floors into `StructuralLoad` by a conditional patch.** Every wall would get one strength whatever it is built from, and whether CollapseStory would then count a wall as support for the block above was not tested.
- **Make walls answer `CanAttachBlockAt` upward so hosted furniture places over a wall.** It changes attachment for every block in every game (decision 0020's list of consumers), for one mod.
- **An issue-only answer with no scenarios.** It leaves no evidence for a later look.

## Consequences & open questions
- A player who runs both mods finds that a covered block set on a wall falls, and that a cabinet, scroll rack, chest, trunk, crate or bookshelf is not hosted in an upper storey.
- A wall stays standing with nothing under it.
- Not checked: lanterns, doors and trapdoors hosted in a wall, beams anchored on a wall face, Roofing on a wall, wind loads, a deck.
- Not checked: CollapseStory adds `UnstableFalling` on the server only, so a client predicts a cabinet's placement over a wall that the server refuses, and whether the client's guest record (#65) is left behind was not looked at.
- The scenarios run only under `just probe collapsestory`, so a CollapseStory update may change these results with nothing in `just e2e` noticing.
