# Furniture against thin walls

- Status: Draft
- Created: 2026-09-20
- Reflects: revived from the Parked list in `README.md`, for release polish

## Summary
Find out, in play, how bad the empty three-quarter cell behind a wall really is for furniture, then pick the cheapest of three answers — document the workaround, offer a thinner partition, or merge voxels.

## Context
A wall is a quarter-block panel hugging one face of its cell (decision 0002), and the other three quarters are dead space no other block can enter, because vanilla allows one block per cell and gates placement on cell occupancy.

Against an *exterior* wall this costs nothing: the panel hugs the room-side face, so a chest in the next cell inside the room sits flush against it, and the dead space falls outside the room.
Against an *interior partition* the panel can only hug one of the two rooms, so the other room's furniture stands three-quarters of a block away from the wall it is supposed to be against.
Which room eats it is already the player's choice, because the wall's orientation follows how it was placed.

The mod is otherwise feature-complete, so this is the last known shape-level compromise, and the parked note explicitly said to revive it if the partition case matters in play — and to say so in the handbook meanwhile.

## Design
Three stages, and it may stop at the first.

**Stage 1 — measure it.** Build a two-room interior partition and try the furniture people actually put against walls: chest, cupboard, shelf, bed, barrel, quern, ground-stored tools, a torch and a sign (the last two should already work — decision 0020 has walls claiming sealed faces for attachment). Screenshot each side. The question is not "is there a gap" — we know there is — but "does the room read as broken", and only play answers it.

**Stage 2 — the workaround, written down.** Two walls back-to-back in *two* cells, each panel hugging its own room, gives both rooms a flush face and costs one cell of floor. That works today with no code at all. If stage 1 says the gap is a minor annoyance, this plus a line in the handbook page is the whole fix.

**Stage 3 — only if stage 1 says it's bad.** Chisel-style voxel merging: the wall and the furniture block become one block entity with both meshes. This is a large feature — mesh merging, two block entities' state in one, drops, breaking, every interaction the merged block used to answer — and it is out of proportion to one side of one partition unless play says otherwise.

## Alternatives considered
- **Centre the panel in its cell.** Then *both* rooms get a three-eighths gap instead of one room getting three-quarters. Worse in total and worse to look at.
- **A full-cell "partition" layout.** A wall as thick as a solid block is a solid block; it throws away the whole point of the mod.
- **Two panels in one cell, facing out both ways.** That is `multiple-walls-per-cell`, parked for its own reasons, and it only moves the dead space to the middle — where, as it happens, nobody needs it. Worth reconsidering *here*, as the cheap version of stage 3, if stage 1 says the partition case is common: one extra layout, the `Back` finish already exists, sealing and retention are unchanged.
- **Letting furniture replace the wall panel.** Silently eats the player's wall. No.

## Consequences & open questions
- Stage 1 is a playtest, not a code change, so it produces a decision doc with screenshots and possibly no diff at all. That's a fine outcome.
- If stage 3 ever happens it wants its own proposal; this one should not smuggle in a voxel-merging design.
- The back-to-back workaround doubles the framing cost of a partition. Cheap in planks, expensive in floor.
