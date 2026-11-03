# 0083 — Facing fronts

- Status: Accepted
- Created: 2026-11-01
- Reflects: branch `inside-corner-post`; `SidingWallBlock.FrontsFaceEachOther`/`HiddenBareFrontAtEnd`, `SidingWallEntity`'s facing-front fill, `VSSiding.Tests/WallShapeGen` (`infill-front`/`infill-secondfront` groups), `SidingWallEntityTests`, `VSSiding.E2E.Tests/FarFaceScenarios.cs`; decisions 0003, 0065, 0074, 0075; played in creative on 2026-11-02

## Summary
Two walls on either side of one cell boundary put their fronts on the same plane.
While a wall's front is bare, the wall draws its own infill in the front slot, so the 2/16 void closes.
A finish clicked on the wall's end goes onto a front that another front hides.

## Context
The front finish is the outer 1/16 of each slab, so two unfinished fronts that face each other leave a 2/16 void between the framings.
No click can reach either face.
One player wrote: "Putting two walls back-to back (for room symmetry) makes it so there is a gap between them, and since the facing is technically covered I have been unable to place the final siding component to complete either wall."
They suggested "door/window joist framing to cover the gaps", or "edge-on placement of siding".
No screenshot of the walls has arrived.
This decision reads the report as walls built front to front.

## Design
**The fill.**
`FrontsFaceEachOther` is true when the wall across a face is a wall whose front faces this one.
While a wall's front is bare, it has infill and `FrontsFaceEachOther` holds, the entity draws the infill in the front slot (and the second-front slot of a corner).
`WallShapeGen` adds the elements as `infill-front` and `infill-secondfront`; a `front` prefix would be picked up by the diagonal and rim generators, which match on it.
A bare frame without infill stays open, and a glazed infill is skipped.
The fill depends on the neighbour's presence, not its finish, so `MarkNeighboursDirty` is not extended.
`CacheKey` carries the flag.

**Light and seal unchanged.**
The fill is drawing only.
Absorption is unchanged, so a bare wattle pair still passes light (decision 0074).

**Tooltip.**
`DescribeFaces` treats a front closed this way as covered, as it does a buried one (decision 0065).

**End-face finish click.**
At the wall's null-face fall-through, a finish clicked on a plain wall's end goes onto its front, when that front is bare and hidden by a facing front (`HiddenBareFrontAtEnd`).
Every other end-face click extends the run as before, and a sneak-click still frames the next cell.
A `cornerout` takes no end-face click: each leg's end lies on a face that `ResolveFinishFace` already names as the corner's back, so the click finishes the back.
Its hidden front is reached through the back once that is finished, as a finish on a finished face goes on the face behind it (decision 0075).
`PlaceWallFrame` needs no guard: vanilla calls the block's `OnBlockInteractStart` before the held item's, so a click that finishes the front never reaches it.

## Alternatives considered
- **Door or window joist framing to cover the gap.** Rejected: it adds a piece to build for what is a drawing gap.
- **Edge-on siding as its own placement.** Rejected: the end-face click does it on the existing finish path.
- **Filling the void whatever the infill.** Rejected: a bare frame is meant to look open, and glazing is not a fill.
- **Extending `MarkNeighboursDirty`.** Rejected: the fill depends on whether the neighbour is there, which the existing neighbour-change redraw covers.

## Consequences & open questions
- A wall built front to front with another shows its infill in its bare front slot; a finish clicked on its end replaces the fill.
- The reading of the report is unconfirmed until the player's screenshot arrives.
- Played: in creative on 2026-11-02; the playtest raised no issue.
