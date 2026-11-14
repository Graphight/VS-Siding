# 0097 — Guest patches bind by position and reach abstract classes

- Status: Accepted
- Created: 2026-11-12
- Reflects: branch `audit-claims-and-hot-paths`; `EveryOverridePatches.FindBlockTypes`, the guest patch classes, `SidingWallBlock.TraitsOf` and `AddOpenPartBoxes`, `GuestWalls.GuestAt`, `SidingModSystem.GapShiftAt` and `AssetsFinalize`; amends decision 0035

## Summary
The guest wall's consumer patches take typed positional arguments, cover overrides declared on abstract block classes, and several per-cell answers are cached per `Attributes` object.

## Context
An audit of the guest wall patches found one coverage gap and several per-call allocations on paths vanilla calls for each cell a light flood visits, each collision query and each chunk mesh.

## Design
**Abstract classes are walked.**
`EveryOverridePatches.FindBlockTypes` no longer drops abstract types, and skips open generic types; `PatchEveryOverride` skips a method that is itself abstract.
`BlockShapeFromAttributes` is abstract and overrides `GetCollisionBoxes`, `GetParticleCollisionBoxes`, `GetSelectionBoxes` and `CanAttachBlockAt` without calling base once its type carries boxes of its own, so `BlockClutter`, `BlockClutterBookshelf` and `BlockRockTyped` bypassed the patch of `Block`'s own method.
Observed: with the old filter a vanilla clutter block of type `anvil-broken1` hosted in a wall's cell answered `GetCollisionBoxes` with its own one box and no panel, and with the fix the panel is there (`GuestWallScenarios.Clutter_Should_CarryThePanelInItsCollisionBoxes_When_HostedInAWallsCell`).
A clutter with no type falls back to base and was patched all along.

**Arguments bind by position.**
Every override a patch lands on shares one exact parameter list, so typed `__0`, `__1` bind on all of them and Harmony builds no `object[]` per call.

**Cached answers.**
- `TraitsOf` caches retention, transparent and porous per `Attributes` object, framing and infill.
- `AddOpenPartBoxes` caches its arrays per base array, layout, side, deck, outer deck and step.
- `AssetsFinalize` shares one expanded `JsonObject` between blocks with identical attribute JSON.

Nothing may write to a siding block's `Attributes` in place, or the cached answers and the shared object go stale.

**Misses allocate nothing.**
Observed: `SidingModSystem.GapShiftAt` allocated 48 bytes per call for a block that hosts nothing, because its lambdas captured locals of the method's top block and C# builds that closure on entry; the lambdas now live in `BedGuest` and `ShiftOff`, and `GapShiftAtTests.ABlockThatHostsNothingAllocatesNothing` measures zero.
`GuestWalls.GuestAt` builds its decode lambda only on a miss of the decoded table.

## Alternatives considered
- **Keep `object[] __args`.** Works, but allocates per call on every patched override.
- **Time the change first.** Nothing was timed; the changes remove allocations and repeated lookups without changing an answer.

## Consequences & open questions
- Recheck the abstract-class walk on a game update, alongside the list in decision 0035.
- No timing or frame-rate claim is made.
