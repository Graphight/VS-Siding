# 0056 — Atlas integration tests

- Status: Accepted
- Created: 2026-10-01
- Reflects: branch `atlas-integration-tests`; `VSSiding.E2E.Tests/`, `justfile`, `CONTRIBUTING.md`; Atlas 0.15.1 (https://github.com/Pixnop/Atlas) on game 1.22.7; issues #55 and #82; decisions 0035, 0043, 0044

## Summary
`VSSiding.E2E.Tests` boots the built mod on a real headless Vintage Story server inside `dotnet test`, via Atlas, and runs one scenario per bug a playtest has caught.
`just e2e` runs it.
Six scenarios plus a boot check ship here; scroll racks (#82) and CI are left for their own PRs.

## Context
`VSSiding.Tests` calls the mod's static helpers and patch methods directly, so no world runs: asset loading, `AssetsFinalize`, the Harmony patches as applied, block entities ticking and chunk save and load were all untested.
The guest wall's exit crash (0035), animals climbing walls (#55) and fire deleting a whole wall (0043) were found in game, each costing a build, a deploy and a person at the wall.

## Design
**A second project, on Atlas.**
It sits beside `VSSiding.Tests` and is in the solution.
Atlas allows one server per process, so `AssemblyInfo.cs` sets `DisableTestParallelization`.
`just test` and `just shapes` run `VSSiding.Tests` only.

**Staging the built `Releases/vssiding` folder.**
A `ProjectReference` with `<AtlasMod>true</AtlasMod>` stages the dll, `modinfo.json` and `deps.json` but no `assets/`, so the server stops in `AssetsFinalize` with "Asset vssiding:config/materials.json could not be found".
The project stages the folder players load instead, through `[assembly: AtlasMods(...)]`.
`just e2e` depends on `build`, so the staged mod is never stale; `dotnet test VSSiding.E2E.Tests` on its own runs whatever `Releases/vssiding` last held.

**Project setup that is not obvious.**
- `Pixnop.Atlas.XUnit` does not bring xunit core.
Without `xunit` 2.9.3 referenced explicitly, the run reports "No test is available" and exits 0.
- Atlas reads `VINTAGE_STORY` at runtime, while the repo's `Directory.Build.props.user` sets only an MSBuild property.
The csproj writes `obj/vintagestory.runsettings` carrying it as an environment variable, so `dotnet test` needs no shell setup.
- The project references `VintagestoryAPI`, `VSEssentials` (`RoomRegistry`) and `VSSurvivalMod` (`BEBehaviorBurning`), not the mod assembly.
Wall and guest layers are read through the block entity's `ToTreeAttributes`, and `GuestWalls` by reflection, so a test sees what is saved and sent, not the mod's own types.

**Interaction goes through the block, server side only.**
Atlas has no click call, so a scenario gives the player the item and calls the block's own handler: `PlaceWallFrame` through the plank's `OnHeldInteractStart`, layers through `SidingWallBlock.OnBlockInteractStart` on the outward face, and hosting a chest through the wall's `OnBlockInteractStart` (`TryHost`, then the chest's `TryPlaceBlock`, then `GuestWalls.Set`).
The player stands outside the cell and the wall claims the face towards the room, since vanilla's `SuggestedHVOrientation` reads the eye position minus the hit position.
A plank finish clicked on the outward face lands in `Back`.

**Scenarios.**
All run under `StrictBootDiagnostics`, so any engine warning from asset loading onwards fails the class.
- Boot, and place a wall.
- A room sealed warm: wattle gives cooling 0, warm 6, exits 0.
Counts include the vanilla floor and roof.
- A cellar: clay gives cooling 4, warm 2, exits 0.
- A framed wall with no infill leaks.
- Fire burns the plank finish and leaves the oak and clay standing (0043).
- A guest wall survives `RestartWorld` (0035).
`RestartWorld` runs before a scenario body and fails while a player is joined, so this is two scenarios in one class, ordered by a `MethodNameOrderer`; Atlas's docs say a class needing order writes its own `ITestCaseOrderer`.
- A sheep pushed into a wall run never rises (#55, decision 0044's `canStep: false`).

**Each scenario was shown to fail.**
- Strict boot went red on a deliberately broken patch target.
- Fire went red with the `KillFire` layer-burn disabled: vanilla deleted the wall and the fire took the cell.
- The guest restart went red with `GuestRecord.EntityData` not serialized.
- The sheep went red with `canStep: true`: it climbed to y+1 and walked about 25 cells over.
It needed forced motion and a 5-cell run, because a wandering sheep or a single cell passed with the fix reverted.

## Alternatives considered
- **Keep unit tests and playtests only.** Every bug above lived in how vanilla called the mod, not in the mod's own logic.
- **A home-made headless harness.** Atlas already does server boot, mod staging, tick pumping, isolation and log capture.
- **Stage through `ProjectReference`.** Tried; it carries no assets.
- **Reference the mod assembly in the test project.** Tests would read the mod's own types instead of saved state, and could pass on a field that never serializes.

## Consequences & open questions
- macOS works: Atlas boots from the flat `/Applications/Vintage Story.app` bundle on 1.22.7 with no native-library workaround.
- Cost: about 12 s for a cold boot, and about 71 s for the whole run of 8 scenarios.
The fire scenario is about 20 s of real-time burn, since Atlas has no fast tick; `remainingBurnDuration` is public if it needs shortening.
- Not done here: the scroll rack scenario (#82), whose red test goes in the #82 fix PR; and a CI workflow, which is its own PR.
- Client-only behaviour stays playtest work: rendering, the light patches as drawn, the mode picker, the client `TryHost` and selection path, and the off-hand saw hosting trap.
- Open: whether `VSSiding.Tests` should move to the CDN server DLLs, so CI needs one game install.
