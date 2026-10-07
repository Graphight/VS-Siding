# 0062 — Rough pole joists

- Status: Accepted
- Created: 2026-10-07
- Reflects: decision 0061 and its playtest of 2026-10-06; branch `feat/rough-pole-joists`; `SidingFloorEntity.SelectiveElements`/`OnTesselation`, `SidingWallEntity.CellAlternate`/`DeckElements`, `VSSiding.Tests/WallShapeGen` (`FloorPoleElements`, `FloorPole2Elements`, `PoleRim`, `DeckGroups`, `DeckSlots`), `blocktypes/floor.json` (`ignoreElements`), the generated `floor.json`, `wall.json` and `cornerout.json` shapes, `WallShapeGenTests`, `FinishElementGroupsTests`, `SidingFloorTests`, `SidingWallEntityTests`; unit and end-to-end tests pass; played on 2026-10-07, before and after the ledge was added

## Summary
A stick or bone floor, and a stick or bone deck, draw their joists as rough poles: recessed, uneven in width, with stubs, rims that read as running over the joist ends, and rope lashing.
It is the recipe of decision 0061 laid flat, with no new mechanism.

## Context
Decision 0061 drew stick and bone wall frames as rough poles and left floors and decks on the plain groups.
In its playtest a stick floor's joists "look a little weird without the spurs and texture" the walls now have.
Decision 0061 named rough joists as its first follow-up.
This graduates no proposal.

## Design
**No new mechanism.**
`SidingFloorEntity.SelectiveElements` gained a trailing `frame` parameter, defaulting to `framing`, and passes it on to the wall's `SelectiveElements`.
`OnTesselation` passes `FrameElements(...)` for the floor's framing.
`sticks` and `bone` already carry `Elements: "poles"`, and the floor's `TexSource` already resolves `lashing` through `SidingWallTexSource.ResolveTexture`.
A floor's groups reuse the wall's names, so `poles-left`, `poles-right`, `poles-top` and `poles-bottom`, and the `poles2-*` set, slot in with no naming work.
The eight new names are in `ignoreElements` of `blocktypes/floor.json`, or an unbuilt floor would draw poles over the plain joists.

**The recipe laid flat.**
Depth is y: front slab 15 to 16, frame 13 to 15, infill 13.5 to 14.5, back slab 12 to 13.
`FloorPoleElements` and `FloorPole2Elements` in `WallShapeGen` carry the numbers.
- Joists sit a quarter voxel inside the frame, y 13.25 to 14.75, at x 2 to 3, 7.5 to 8.5 and 13 to 14.
- Each joist is cut into three lengths along z.
- The middle length is a quarter voxel wider along x, on one side.
- The end lengths keep the plain width, so cells meet flush.
- The three joists cut at different places.
- Rims keep the plain boxes, at z 0 to 1 and z 15 to 16, cut between the joists.
- Each rim gains cheeks in the skins y 13 to 13.25 and y 14.75 to 15 across each joist, so a rim reads as running over the joist end.
- Lashing is cheeks in the same skins beside each rim, z 1 to 2.5 in `poles-top` and z 13.5 to 15 in `poles-bottom`, on the `lashing` slot, a quarter voxel wider than the joist on both sides.
- Stubs lie in the half voxel over the infill, y 13 to 13.5 on the underside and y 14.5 to 15 on top, a voxel wide along z, running one to two and a half voxels along x from a joist.
- Stubs stay between z 3 and z 13, off the cut positions, and never reach the next joist or the cell edge.
- The second variant moves the cuts, the stubs and their sides, and flips the side the middle length swells to.
- Its rims, cheeks and lashing are the first variant's renamed, as on the wall.

**The joists cannot open a line of sight.**
A floor's infill is one sheet across the cell, x 0 to 16, with the joists passing through it.
No joist shape, however recessed or cut, opens a line of sight through the floor.
A wall's post may swell inward only, or it leaves a gap beside the infill; a joist may swell to either side.

**Lashing shows at a floor's north and south edges.**
`SidingFloorBlock.Joins` drops a rim wherever the floor carries on north or south and never drops a joist.
Lashing is in the rim's group, so it drops with the rim.
Rope shows only at the north and south edges of a run of floors, and mid-floor a cell shows uneven joists and stubs.

**The floor's infill check is replaced.**
`SidingFloorEntity.OnTesselation` had its own `AnyVaries` check over the finishes and the framing, which left the infill out.
`SidingWallEntity.CellAlternate` replaces it.
A floor's `packeddirt` infill now varies under every joist frame, which closes the gap decision 0061 recorded for floors.

**Decks follow.**
A deck is the floor's elements clipped per side by `WallShapeGen.DeckGroups`, so the pole groups reach `wall.json` and `cornerout.json` as `deck-{side}-poles*`.
`SidingWallEntity.DeckElements` takes the deck's own frame prefix, read with `FrameElements` for the deck's framing.
`CellAlternate` keeps the parity when the deck's framing is rough as well as the wall's, so a plank wall with a stick deck alternates its deck.
`DeckSlots` maps `lashing` to itself.
Rope is one constant, so the deck copies use the wall shape's own `lashing` slot, and `EverySidesDeckGroupsLieInsideItsDeckBox` accepts `#lashing` beside the deck's own texture codes.

**Clipped members.**
The clip drops some members whole: the rim against the wall, and a joist that falls inside the wall panel.
Where it removed a joist its stubs survived with nothing behind them.
Only a rim runs across a deck's edge, so `DeckGroups` drops any other pole piece an edge cuts: a stub, cheek or lashing whose joist is on the far side, or a sliver of a joist's swelling.
`APoleDeckKeepsTheMembersThePlainDeckKeeps` asserts a pole deck keeps exactly the members the plain deck keeps, per shape and side.
That check compares group names, and `poles-left` holds two joists, so its name survives the loss of one.
`EveryPieceOfAFloorOrDeckPoleGroupHangsOffAMember` covers that case: every stub, cheek and lashing is joined, through boxes of its group, to a joist or rim that crosses the frame's mid-plane.

**The ledge.**
In play the deck did not connect to the wall's frame.
Decision 0042 made a deck fill the open 12/16 of the cell, so it stops a voxel short of the frame, at the slot the wall's room-side finish takes.
A plank deck has the same gap; a stick wall shows it more, since its room side is often bare and its posts are recessed a further quarter voxel.
The shapes now carry the deck's groups a second time, clipped to that slot, as `ledge-{side}-{name}`.
`WallShapeGen.Ledge` reads the slot from the layout's own `back` boxes, at the deck's height, so a corner's ledge is the L its back finish is.
`SidingWallEntity.DeckElements` asks for the ledge while the wall's `Back` is null, so every layer of the deck, its infill and finishes included, runs to the frame, and steps back out of the slot when the finish is laid.
This holds for every deck, plank as well as pole, and changes what decision 0042 draws against a bare room side.
Collision is not changed: the deck's box still starts where it did.
`EveryLedgeBoxLiesInTheSlotOfTheRoomSideFinish` asserts each ledge box lies where that side's back slab would.

**Invariant tests.**
The three pole invariant theories of decision 0061 read the frame's depth axis, thickness range and mid-plane from the layout (`Frame` in `WallShapeGenTests`) and run for the floor too, with the floor as y, 13 to 15, mid-plane 14.
`FloorLaysTheWallsLayersFlatAtTheTopOfTheCell` writes the floor's element list out by hand and skips `poles*`, which the committed JSON and the invariants pin.

**Quad cost.**
`EachBuiltCellHandsTheTesselatorThisManyQuads` gained floor rows.

| Cell | Plain frame | Pole frame | Second pole frame |
| --- | --- | --- | --- |
| floor, bare | 66 | 240 | 240 |

A pole floor is about 3.6 times the quads of a plain one.

**Shape growth.**
Each floor pole box is copied into both wall shapes for up to four deck sides, and every floor box again for the ledge.
Lines of generated JSON, at `main` and at this branch:

| Shape | `main` | This branch |
| --- | --- | --- |
| `wall.json` | 24394 | 47352 |
| `cornerout.json` | 32400 | 54244 |
| `floor.json` | 2801 | 8532 |

**What keeps the plain groups.**
A glazed floor keeps its bezel, as a glazed wall does.
The lath battens under a daub ceiling stay plain, since they draw on the `framing` slot but belong to the finish.
Collision keeps the plain boxes, as for a wall.

## Alternatives considered
- **A `decklashing` slot.** Rope is one constant, so one slot serves, and the deck copies use the wall shape's `lashing`.
- **Keeping the orphan stubs on decks.** A stub left beside the wall with its joist clipped away has nothing to hang from, so `DeckGroups` drops it.
- **Leaving decks plain.** A stick deck would sit beside a rough stick floor with square joists.
- **A single ledger pole in the slot.** The deck's infill and top finish would still stop a voxel short, leaving a trench along the wall.
- **Starting the deck at the frame for good.** The slot is the room-side finish's, and a deck standing in it would show through the slab's relief.
- **A separate floor-only recipe.** The wall's recipe laid flat reuses the group names, `FrameElements` and `CellAlternate`, and a second recipe would need its own paths for each.

## Consequences & open questions
Played on 2026-10-07, before the ledge existed.
The frames and joists were liked: "I love the little character bits on the frames and joists now", and "everything else is looking amazing".
The one fault raised was that "it does look exceptionally weird that the deck does not connect to the frame", which the ledge answers.
The ledge was played the same day: "much better", though the deck "still looks a little weird floating below the frame rim".
A bare deck's joists and rims top out at y 15, a voxel under the top of the wall's plate, since the deck's top finish takes y 15 to 16 and sits flush with the plate once laid.
Whether a finished deck still looks as if it floats was not looked at.
Still open:
- Whether the deck stepping back out of the slot when the room side is finished is noticed.
- Whether a deck with dropped stubs near the wall looks bare.
- Whether 240 quads a cell matters on a large floor.
- Whether the larger shape files cost load time.

Deck groups are in no `ignoreElements` list, plain or pole, which is unchanged here.
