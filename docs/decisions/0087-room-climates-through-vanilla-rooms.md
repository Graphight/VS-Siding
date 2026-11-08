# 0087 — Room Climates through vanilla rooms

- Status: Accepted
- Created: 2026-11-07
- Reflects: mod page comment https://mods.vintagestory.at/vssiding#cmt-247520, the reply https://mods.vintagestory.at/vssiding#cmt-256367 and the Room Climates author's reply https://mods.vintagestory.at/vssiding#cmt-258027; issue #137; decisions 0002, 0003, 0019, 0020, 0033, 0074, 0082; `VSSiding.E2E.Tests/RoomScenarios.cs`; vanilla `Cuboidi.IntersectsOrTouches` read decompiled; branch `room-climates`; Room Climates never loaded, downloaded or decompiled

## Summary
A player asked for Siding to work with Room Climates, which gives rooms insulation and a cellar bonus.
Room Climates reads vanilla's rooms only, so Siding changes nothing for it: a siding room that vanilla counts as sealed, with cooling walls, gets the same counts under Room Climates.

## Context
Room Climates is closed source.
Before its author replied, the open question was which of a siding wall's answers it reads: its `GetRetention` (sign from the infill), the infill on its block entity, or its block material, which the outermost layer decides (decision 0033), not the infill.

The author's reply (#cmt-258027) settles it.
Room Climates reads the `Room` that vanilla's `RoomRegistry` builds, taking `CoolingWallCount` and `NonCoolingWallCount` for insulation and the cellar bonus, and does not read `BlockMaterial` or call `Block.GetRetention` itself.
It leaves `RoomRegistry` untouched, so a room that counts without Room Climates counts the same with it.
Two rooms are adjacent, in the author's words, when one room's `Location` cuboid, grown by 1 on every side, touches or intersects the other's, so a one-block wall between them makes them neighbours.
Vanilla's `Location` covers the cells the room's walk visits, bounds inclusive, and vanilla's `Cuboidi.IntersectsOrTouches` is true only when two cuboids share a cell.
By that method, two vanilla rooms either side of one solid block are not adjacent (one grown by 1 reaches the block's cell, the other starts past it), so either Room Climates' own test differs from that method or the reply leaves a step out.

Vanilla's room walk is where a siding wall's answers come in: `GetRetention` on the hugged face, with its sign from the infill (decisions 0002, 0003).

## Design
**Siding adds nothing for Room Climates.**
No code or patch in Siding names it.
The counts it reads are vanilla's, and `RoomScenarios` already pins them for siding walls: sealing, and cooling or non-cooling by infill.

**Two scenarios pin what Room Climates builds on top of those counts.**
1. `Rooms_Should_BeAdjacent_When_TheyShareOneWall`: two one-cell rooms with one siding wall between them are both sealed, are different rooms, and the first room's `Location` grown by 1 touches the second's.
The shared wall claims only its face toward the first room, so the second room's walk takes in the shared wall's cell, and the two `Location`s share it.
That puts them one cell closer than two vanilla rooms either side of a solid block, so whatever touch test Room Climates uses, if it counts the vanilla case it counts this one.
The outer wall lines carry on through the two cells beside the shared wall in plain walls raised against it.
Without them, the second room walks through the shared wall's cell and out of its unclaimed north and south faces into the open (1067 exits), and the first does not.
2. `Room_Should_HaveOnlyCoolingWalls_When_ClayWallsStandOnAndUnderStone`: clay walls on a granite floor under a granite roof give 0 non-cooling walls, 0 skylights and 0 exits, Room Climates' well-built cellar.
With a planks floor and roof, `Room_Should_BeACellar_When_WallsAreFilledWithClay` counts 4 cooling and 2 non-cooling sides, and the 2 are the floor and roof, not the walls.

## Alternatives considered
- **A `just probe` run with Room Climates loaded, as decision 0082 did for CollapseStory.** The author's reply names everything Room Climates reads, and those are vanilla answers that `just e2e` checks on every run with no other mod downloaded.
- **Decompiling Room Climates to confirm the reply.** Not done: the reply and the scenarios are the evidence.

## Consequences & open questions
- A siding room gets Room Climates' insulation, cellar bonus and adjacency wherever vanilla counts it as a sealed room.
- Room Climates was never loaded alongside Siding, so the finding rests on its author's reply and on vanilla's counts; a Room Climates update that reads something else would not show up in `just e2e`.
- Open: whether Room Climates treats a glazed cell (decision 0019) or a bare wattle wall (decision 0074) as a window. Neither can be opened.
- Not checked: a Siding thin floor or deck as a cellar's floor or ceiling.
- Open: how Room Climates' adjacency test passes two vanilla rooms with one block between them, since vanilla's `IntersectsOrTouches` does not; the comparison above holds whichever way it does.
