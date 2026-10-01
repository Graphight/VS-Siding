# Atlas integration tests

- Status: Draft
- Created: 2026-10-01
- Reflects: Atlas 0.15.1 (https://mods.vintagestory.at/atlas, source https://github.com/Pixnop/Atlas, MIT) and its wiki pages Getting Started, Writing Scenarios, Client-Side Testing, Mod Staging, Boot Diagnostics and CI Recipes; `VSSiding.Tests/VSSiding.Tests.csproj`, `justfile`, `CONTRIBUTING.md`; issues #55 and #82; decisions 0035, 0043; not yet tried

## Summary
Atlas is an xUnit framework that boots a real headless Vintage Story server inside `dotnet test`, with the mod under test loaded, and lets a test place blocks, join a player, run ticks and read the world back.
The proposal adds a second test project, `VSSiding.E2E.Tests`, built on it, a `just e2e` recipe, and a first set of scenarios that each replay a bug that only a playtest has caught so far.

## Context
**What the tests reach today.**
`VSSiding.Tests` links the game's DLLs and calls the mod's static helpers and patch methods directly: layer materials, peel order, drops, shape generation, family expansion over JSON.
No world runs, so nothing exercises asset loading, `AssetsFinalize`, the Harmony patches as applied, block entities ticking, chunk save and load, or the network channel.

**What playtests have caught instead.**
The guest wall's exit crash and one-tick flash (0035), animals climbing walls (#55), fire deleting a whole wall (0043), and the scroll racks that stop joining in a wall (#82) were all found in game.
Each costs a build, a deploy, a world, and a person walking up to the wall.
Several of those are server-side state, which a headless server can see.

**What Atlas gives.**
- `World.SetBlock`, `BlockAt`, `BlockEntityAt<T>`, `SpawnEntity`, `Ticks`, `Until` and `ExecuteCommand` on a superflat, fixed-seed world.
- `World.Api`, the raw `ICoreServerAPI`, for anything the session does not wrap.
- `JoinPlayer`, a test player on the game's own network path, with `GiveItem` and a live `IServerPlayer`, and the packets that player receives on any mod channel.
- `World.BootDiagnostics`, every engine warning and error from asset loading onwards, and `StrictBootDiagnostics` to fail a class on any of them.
- Three resets per scenario: a fresh world, a rollback to a snapshot (about 25 times cheaper), or a real save, shutdown and reboot (`RestartWorld`).
- A GitHub Actions recipe that fetches the Linux server from the Vintage Story CDN by version.

## Design
**A second test project.**
`VSSiding.E2E.Tests` beside `VSSiding.Tests`, on `Pixnop.Atlas.XUnit`, which pins xUnit 2.9.3 or later and allows one server per process, so it sets `DisableTestParallelization`.
Keeping it separate leaves the existing tests fast and unchanged, and lets each run alone.
The mod is staged through a `ProjectReference` with `<AtlasMod>true</AtlasMod>`, or as the built `Releases/vssiding` folder if the project-reference path does not carry `assets/`; the folder is what players load, so it is the fallback to prefer.

**`just e2e`.**
A recipe running `dotnet test VSSiding.E2E.Tests`, and `just test` left as it is.
`CONTRIBUTING.md`'s "Before you open a PR" gains it for any change touching patches, block entities or saved state.

**Interaction goes through the block.**
Atlas has no "right-click this block" call.
A scenario gives the test player the item, sets the off-hand slot through `Player.Entity.LeftHandItemSlot`, builds a `BlockSelection`, and calls `OnBlockInteractStart` on the block itself.
That is the server half of a click; the client's own selection and `TryHost` path is not run, so the trap where a saw in the off hand skips hosting still needs a playtest.

**The first scenarios, one per caught bug.**
- *Boots clean*: `StrictBootDiagnostics` on, so a broken `materials.json` entry, a family matching no vanilla item, a failed JSON patch or a Harmony target that moved in a game update fails the run.
- *Room seals*: four framed and filled walls with a roof, then `/sidingroom`, expecting a closed room, and a cellar with a cooling infill.
- *Fire takes one layer* (0043): a wall with planks over clay is set alight, ticked until the fire burns out, and expected to stand with its clay.
- *Guest wall survives a restart* (0035): a chest hosted in a wall cell, `RestartWorld`, and the guest wall's layers expected back from chunk mod data.
- *Animals stay down* (#55): a sheep spawned against a wall, ticked, and expected never to stand on it.
- *Scroll racks join in a wall* (#82): two racks hosted side by side, a scroll put on the slot between them, and expected in the neighbour's inventory; written red first, as the repro for the fix.

**CI after it runs locally.**
The repo has no CI today.
Once the scenarios pass on a desk, a workflow runs both projects on each PR against the game version the mod targets, from Atlas's recipe.
It also keeps the existing tests honest, since they need the same game DLLs the server download carries.

## Alternatives considered
- **Keep unit tests and playtests only.** Every bug listed above slipped past unit tests, because the bug lived in how vanilla called the mod, not in the mod's own logic.
- **A home-made headless server harness.** Atlas already solves server boot, mod staging, tick pumping, isolation and log capture, and is maintained against each game release.
- **A real client in CI.** Atlas measured it and does not ship one; nothing in this repo needs it badly enough to build it.

## Consequences & open questions
- Nothing renders, so meshes, textures (#81), flicker (#58, #64, #65), the mode picker and every client-only patch (`TesselateBlock`, the light patches as drawn) stay playtest work.
- Whether Atlas boots from the macOS app bundle: its docs and CI name Linux and Windows paths only.
- Whether the client-side light patches have a server-side value worth asserting, such as `GetLightLevel` in a sealed cell.
- Boot time per scenario class, and how far rollback brings it down.
- Whether `VSSiding.Tests` should move onto the CDN-downloaded server's DLLs too, so CI needs one game install.
