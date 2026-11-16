using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `just probe realsmoke`, the only run that loads Real Smoke (issue #101). Each arena lights a firepit in a cell of
// its own and reads the smoke Real Smoke holds in the cell beyond the block under test, through the mod's own controller.
// Every verdict is what the mod does today; a fix flips the row it fixes, and docs/compat/realsmoke-smoke.md records the run.
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class RealSmokeScenarios : AtlasScenarioBase
{
    private static bool RealSmokeLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "realsmoke";

    private const string Stone = "game:rock-granite";
    private const string Trapdoor = "game:trapdoor-solid-oak-1";
    private const int PollTicks = 100;
    // A hundred seconds of game time: smoke crosses a stretch of open air in a stone box slowly, and the open control has to get through.
    private const int Polls = 30;

    private sealed record Arena(string Name, BlockPos Fire, BlockPos Between, BlockPos Far);

    private readonly List<Arena> arenas = new();
    private readonly List<(string Name, BlockPos Inside)> roomCells = new();
    private readonly List<(string Name, BlockPos Cell, bool Hosted, bool Lit, string? Rule)> hostedCells = new();

    private BlockPos Origin(int index) => World.Spawn.Offset(6 * (index % 4), 3, 6 * (index / 4) - 6);

    private void Fill(string code, params BlockPos[] cells)
    {
        foreach (BlockPos cell in cells) World.SetBlock(code, cell);
    }

    private static BlockPos[] Ring(BlockPos cell, params BlockFacing[] faces) => faces.Select(face => cell.AddCopy(face)).ToArray();

    private async Task Click(ITestPlayer player, BlockPos cell, string item, int quantity, BlockFacing face, int boxIndex = 0)
    {
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
        await player.GiveItem(item, quantity);
        var selection = new BlockSelection { Position = cell.Copy(), Face = face, HitPosition = new Vec3d(0.5, face == BlockFacing.UP ? 1 : 0.5, 0.5), SelectionBoxIndex = boxIndex };
        Assert.True(World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection));
    }

    // fire, wall cell, measured cell in a line east. A stone box round the fire leaves the wall as its one way out, and a box round the
    // measured cell leaves the wall as its one way in. fireOnRoomSide puts the fire where the wall's claimed face is.
    private async Task Horizontal(string name, bool fireOnRoomSide, System.Func<BlockPos, BlockPos, Task> build)
    {
        BlockPos origin = Origin(arenas.Count);
        BlockPos fire = origin, wall = origin.EastCopy(), far = origin.EastCopy(2);
        Fill(Stone, fire.DownCopy(), wall.DownCopy(), far.DownCopy());
        await build(wall, fireOnRoomSide ? fire : far);
        Fill(Stone, Ring(fire, BlockFacing.UP, BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.WEST));
        Fill(Stone, Ring(wall, BlockFacing.UP, BlockFacing.NORTH, BlockFacing.SOUTH));
        Fill(Stone, Ring(far, BlockFacing.UP, BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST));
        arenas.Add(new Arena(name, fire, wall, far));
    }

    // fire, cell above it, measured cell above that, with the same boxes. The fire's cell is built in planks and cleared afterwards, as a floor is raised from a block below.
    private async Task Vertical(string name, System.Func<BlockPos, Task> build, Action<BlockPos>? place = null)
    {
        BlockPos fire = Origin(arenas.Count), above = fire.UpCopy(), far = fire.UpCopy(2);
        Fill(Stone, fire.DownCopy());
        World.SetBlock("game:planks-aged-ud", fire);
        await build(above);
        place?.Invoke(above);
        World.SetBlock("game:air", fire);
        Fill(Stone, Ring(fire, BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST));
        Fill(Stone, Ring(above, BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST));
        Fill(Stone, Ring(far, BlockFacing.NORTH, BlockFacing.SOUTH, BlockFacing.EAST, BlockFacing.WEST, BlockFacing.UP));
        arenas.Add(new Arena(name, fire, above, far));
    }

    private async Task Wall(ITestPlayer player, BlockPos cell, BlockPos inside, string? infill, int quantity)
    {
        await WallBuilder.Raise(World, player, cell, inside, "game:plank-oak", null);
        if (infill == null) return;
        BlockFacing outward = BlockFacing.FromVector(cell.X - inside.X, 0, cell.Z - inside.Z);
        await Click(player, cell, infill, quantity, outward);
    }

    private async Task Deck(ITestPlayer player, BlockPos cell, bool filled, string fill = "game:clay-blue", int amount = 4)
    {
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "deck");
        await Click(player, cell, "game:plank-oak", 2, BlockFacing.EAST);
        if (filled) await Click(player, cell, fill, amount, BlockFacing.UP, World.BlockAt(cell).SelectionBoxes.Length);
        player.Entity.WatchedAttributes.SetString("vssidingDeck", "");
    }

    private async Task Floor(ITestPlayer player, BlockPos cell, bool filled, string fill = "game:clay-blue", int amount = 4)
    {
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "floor");
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", null);
        if (filled) await Click(player, cell, fill, amount, BlockFacing.UP);
        player.Entity.WatchedAttributes.SetString("vssidingFraming", "wall");
    }

    private async Task Lantern(ITestPlayer player, BlockPos wall, BlockPos inside)
    {
        await Wall(player, wall, inside, "game:clay-blue", 4);
        player.Entity.LeftHandItemSlot.Itemstack = null;
        ItemStack stack = WallBuilder.LanternStack(World);
        var selection = new BlockSelection { Position = wall.Copy(), Face = BlockFacing.UP, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        string failureCode = "";
        stack.Block.TryPlaceBlock(player.Entity.World, player.Player, stack, selection, ref failureCode);
        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation("game:saw-copper")));
    }

    // A one-cell room with clay-filled walls under a planks roof, whose centre cell holds the given block.
    private async Task Room(ITestPlayer player, string name, string? roofCenter)
    {
        BlockPos inside = World.Spawn.Offset(4 * roomCells.Count, 3, -14);
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                World.SetBlock("game:planks-aged-ud", inside.Offset(dx, -1, dz));
        foreach (BlockFacing side in BlockFacing.HORIZONTALS)
            await WallBuilder.Raise(World, player, inside.AddCopy(side), inside, "game:plank-oak", "game:clay-blue");
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (dx != 0 || dz != 0) World.SetBlock("game:planks-aged-ud", inside.Offset(dx, 1, dz));
        if (roofCenter != null) World.SetBlock(roofCenter, inside.UpCopy());
        roomCells.Add((name, inside));
    }

    private async Task Host(ITestPlayer player, string name, Block block, bool lit)
    {
        BlockPos cell = World.Spawn.Offset(4 * hostedCells.Count, 3, -22);
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation("game:saw-copper")));
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue");
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(block);
        player.Entity.LeftHandItemSlot.Itemstack = null;
        player.Entity.Pos.SetPos(cell.X + 2.5, cell.Y, cell.Z + 0.5);
        player.Entity.Pos.Yaw = 3 * GameMath.PIHALF;
        var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection);
        bool hosted = World.BlockAt(cell).Code.FirstCodePart() == block.Code.FirstCodePart() && WallBuilder.GuestLayers(World, cell) != null;
        if (hosted && lit) Light(cell, place: false);
        string? rule = (string?)World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType().GetMethod("HostRefusal", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { block });
        hostedCells.Add((name, cell, hosted, lit, rule));
    }

    private Block FirstBlock(string prefix, string? contains = null)
        => World.Api.World.Blocks.First(block => block.Code != null && block.Code.Domain == "game" && block.Code.Path.StartsWith(prefix) && (contains == null || block.Code.Path.Contains(contains)));

    private int Smoke(BlockPos cell)
    {
        object system = World.Api.ModLoader.GetModSystem("RealSmoke.ModSystem");
        object controller = system.GetType().GetProperty("SmokeController")!.GetValue(system)!;
        Type box = system.GetType().Assembly.GetType("RealSmoke.Smoke.Simulation.SmokeQueryBox")!;
        object region = Activator.CreateInstance(box, cell.X, cell.Y, cell.Z, cell.X + 1.0, cell.Y + 1.0, cell.Z + 1.0)!;
        MethodInfo amount = controller.GetType().GetMethod("GetSmokeAmount")!.MakeGenericMethod(box);
        return (int)amount.Invoke(controller, new[] { region })!;
    }

    // The firepit burns while it has fuel time, which Real Smoke's emitter reads; a hosted one is already placed and only needs the time.
    private void Light(BlockPos fire, bool place = true)
    {
        if (place && World.BlockAt(fire).Code.Path != "firepit-lit") World.SetBlock("game:firepit-lit", fire);
        object entity = World.BlockEntityAt<BlockEntity>(fire)!;
        entity.GetType().GetField("fuelBurnTime")!.SetValue(entity, 100000f);
        entity.GetType().GetField("furnaceTemperature")!.SetValue(entity, 300f);
    }

    // What vanilla and Real Smoke read of a face: the liquid barrier and SideIsSolid.
    private string Face(BlockPos cell, BlockFacing face)
    {
        Block block = World.BlockAt(cell);
        return $"barrier {block.GetLiquidBarrierHeightOnSide(face, cell)}, side solid {block.SideIsSolid(cell, face.Index).ToString().ToLowerInvariant()}";
    }

    private string Faces(Arena arena)
    {
        BlockFacing toward = BlockFacing.FromVector(arena.Between.X - arena.Fire.X, arena.Between.Y - arena.Fire.Y, arena.Between.Z - arena.Fire.Z);
        return $"fire side {Face(arena.Between, toward.Opposite)}; far side {Face(arena.Between, toward)}";
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true, TimeoutMs = 300000)]
    public async Task Probe_Should_RecordWhereSmokeGoesAtSidingBlocks()
    {
        if (!RealSmokeLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            await Horizontal("control: open cell", false, (_, _) => Task.CompletedTask);
            await Horizontal("control: stone", false, (wall, _) => { Fill(Stone, wall); return Task.CompletedTask; });

            foreach (bool onRoomSide in new[] { false, true })
            {
                string side = onRoomSide ? "fire on the room face" : "fire on the outer face";
                await Horizontal($"wall, bare frame, {side}", onRoomSide, (wall, inside) => Wall(player, wall, inside, null, 0));
                await Horizontal($"wall, clay infill, {side}", onRoomSide, (wall, inside) => Wall(player, wall, inside, "game:clay-blue", 4));
                await Horizontal($"wall, wattle infill, {side}", onRoomSide, (wall, inside) => Wall(player, wall, inside, "game:stick", 4));
                await Horizontal($"vent wall, trapdoor infill, {side}", onRoomSide, (wall, inside) => Wall(player, wall, inside, Trapdoor, 1));
                await Horizontal($"wall, glass infill, {side}", onRoomSide, (wall, inside) => Wall(player, wall, inside, "game:glass-plain", 1));
                await Horizontal($"wall, clay infill hosting a lantern, {side}", onRoomSide, (wall, inside) => Lantern(player, wall, inside));
            }
            // A trapdoor deck puts the whole cell on the vent block, so the clay beside it opens too. The deck goes on from the open east side.
            await Horizontal("wall, clay infill under a vent deck, fire on the room face", true, async (wall, inside) => { await Wall(player, wall, inside, "game:clay-blue", 4); await Deck(player, wall, true, Trapdoor, 1); });

            await Vertical("control: open cell above", _ => Task.CompletedTask);
            await Vertical("control: stone above", cell => { Fill(Stone, cell); return Task.CompletedTask; });
            await Vertical("floor, bare joists", cell => Floor(player, cell, false));
            await Vertical("floor, clay infill", cell => Floor(player, cell, true));
            await Vertical("wall deck, bare", async cell => { await Wall(player, cell, cell.WestCopy(), "game:clay-blue", 4); await Deck(player, cell, false); });
            await Vertical("wall deck, clay infill", async cell => { await Wall(player, cell, cell.WestCopy(), "game:clay-blue", 4); await Deck(player, cell, true); });
            await Vertical("vent floor, trapdoor", cell => Floor(player, cell, true, Trapdoor, 1));
            await Vertical("vent wall deck, trapdoor", async cell => { await Wall(player, cell, cell.WestCopy(), "game:clay-blue", 4); await Deck(player, cell, true, Trapdoor, 1); });
            await Vertical("clay deck on a vent wall", async cell => { await Wall(player, cell, cell.WestCopy(), Trapdoor, 1); await Deck(player, cell, true); });
            await Vertical("roof of a wooden trapdoor", _ => Task.CompletedTask, cell => World.SetBlock(FirstBlock("trapdoor-solid-").Code.ToString(), cell));
            await Vertical("roof of a wooden window trapdoor", _ => Task.CompletedTask, cell => World.SetBlock(FirstBlock("trapdoor-window-").Code.ToString(), cell));
            await Vertical("roof of a barred trapdoor", _ => Task.CompletedTask, cell => World.SetBlock(FirstBlock("trapdoor-bars").Code.ToString(), cell));
            await Vertical("roof of a stick layer", _ => Task.CompletedTask, cell => World.SetBlock("game:stickslayer-wooden-ew-free", cell));
            await Vertical("chimney course over a clay floor", cell => Floor(player, cell, true), cell => World.SetBlock(FirstBlock("claybrickchimney-").Code.ToString(), cell.UpCopy()));

            await Room(player, "planks roof (control)", "game:planks-aged-ud");
            await Room(player, "no roof cell", null);
            await Room(player, "vent floor, trapdoor", null);
            BlockPos ventRoof = roomCells[^1].Inside.UpCopy();
            World.SetBlock("game:planks-aged-ud", ventRoof.DownCopy());
            await Floor(player, ventRoof, true, Trapdoor, 1);
            World.SetBlock("game:air", ventRoof.DownCopy());
            await Room(player, "wooden trapdoor", FirstBlock("trapdoor-solid-").Code.ToString());
            await Room(player, "wooden window trapdoor", FirstBlock("trapdoor-window-").Code.ToString());
            await Room(player, "barred trapdoor", FirstBlock("trapdoor-bars").Code.ToString());
            await Room(player, "stick layer", "game:stickslayer-wooden-ew-free");

            await Host(player, "firepit", World.Api.World.GetBlock(new AssetLocation("game:firepit-construct1")), true);
            await Host(player, "forge", World.Api.World.GetBlock(new AssetLocation("game:forge")), false);
            await Host(player, "clayoven", FirstBlock("clayoven-", "east"), false);
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }

        var faces = arenas.ToDictionary(arena => arena.Name, Faces);
        var peak = arenas.ToDictionary(arena => arena.Name, _ => 0);
        long started = World.Api.World.ElapsedMilliseconds;
        for (int poll = 0; poll < Polls; poll++)
        {
            foreach (Arena arena in arenas) Light(arena.Fire);
            foreach (var host in hostedCells.Where(host => host.Lit && host.Hosted)) Light(host.Cell, place: false);
            await World.Ticks(PollTicks);
            foreach (Arena arena in arenas) peak[arena.Name] = Math.Max(peak[arena.Name], Smoke(arena.Far));
        }
        double seconds = (World.Api.World.ElapsedMilliseconds - started) / 1000.0;

        var verdicts = arenas.ToDictionary(arena => arena.Name, arena => peak[arena.Name] > 0 ? "smoke passes" : "smoke held");
        var registry = World.Api.ModLoader.GetModSystem<RoomRegistry>();
        var roomRows = roomCells.Select(room =>
        {
            Vintagestory.GameContent.Room found = registry.GetRoomForPosition(room.Inside);
            return (room.Name, Verdict: found.ExitCount == 0 ? "sealed" : "leaks", Detail: $"exits {found.ExitCount}, skylights {found.SkylightCount}, warm walls {found.NonCoolingWallCount}");
        }).ToList();
        var layers = ("oak", "clay", (string?)null, (string?)null, (string?)null);
        var hostRows = hostedCells.Select(host => (host.Name, Verdict: !host.Hosted ? (host.Rule is { } rule ? $"refused: {rule}" : "placement failed") : WallBuilder.GuestLayers(World, host.Cell) == layers ? "hosts, wall layers intact" : "hosts, wall layers changed")).ToList();

        WriteReport(
            seconds,
            arenas.Select(arena => $"| {arena.Name} | {verdicts[arena.Name]} ({peak[arena.Name]}) | {faces[arena.Name]} |"),
            roomRows.Select(row => $"| {row.Name} | {row.Verdict} | {row.Detail} |"),
            hostRows.Select(row => $"| {row.Name} | {row.Verdict} |"));

        Assert.Equal(ExpectedSmoke, verdicts);
        Assert.Equal(ExpectedRooms, roomRows.ToDictionary(row => row.Name, row => row.Verdict));
        Assert.Equal(ExpectedHosts, hostRows.ToDictionary(row => row.Name, row => row.Verdict));
    }

    private const string Passes = "smoke passes", Held = "smoke held";

    private static readonly Dictionary<string, string> ExpectedSmoke = new()
    {
        ["control: open cell"] = Passes,
        ["control: stone"] = Held,
        ["wall, bare frame, fire on the outer face"] = Passes,
        ["wall, clay infill, fire on the outer face"] = Held,
        ["wall, wattle infill, fire on the outer face"] = Held,
        ["wall, glass infill, fire on the outer face"] = Held,
        ["wall, clay infill hosting a lantern, fire on the outer face"] = Held,
        ["wall, bare frame, fire on the room face"] = Passes,
        ["wall, clay infill, fire on the room face"] = Held,
        ["wall, wattle infill, fire on the room face"] = Held,
        ["wall, glass infill, fire on the room face"] = Held,
        ["wall, clay infill hosting a lantern, fire on the room face"] = Held,
        ["control: open cell above"] = Passes,
        ["control: stone above"] = Held,
        ["floor, bare joists"] = Held,
        ["floor, clay infill"] = Held,
        ["wall deck, bare"] = Held,
        ["wall deck, clay infill"] = Held,
        ["vent wall, trapdoor infill, fire on the outer face"] = Passes,
        ["vent wall, trapdoor infill, fire on the room face"] = Passes,
        ["vent floor, trapdoor"] = Passes,
        ["vent wall deck, trapdoor"] = Passes,
        ["wall, clay infill under a vent deck, fire on the room face"] = Passes,
        ["clay deck on a vent wall"] = Passes,
        ["roof of a wooden trapdoor"] = Held,
        ["roof of a wooden window trapdoor"] = Held,
        ["roof of a barred trapdoor"] = Held,
        ["roof of a stick layer"] = Passes,
        ["chimney course over a clay floor"] = Held,
    };

    private static readonly Dictionary<string, string> ExpectedRooms = new()
    {
        ["planks roof (control)"] = "sealed",
        ["no roof cell"] = "leaks",
        ["vent floor, trapdoor"] = "sealed",
        ["wooden trapdoor"] = "sealed",
        ["wooden window trapdoor"] = "leaks",
        ["barred trapdoor"] = "leaks",
        ["stick layer"] = "leaks",
    };

    private static readonly Dictionary<string, string> ExpectedHosts = new()
    {
        ["firepit"] = "hosts, wall layers intact",
        ["forge"] = "hosts, wall layers intact",
        ["clayoven"] = "refused: solid side",
    };

    private void WriteReport(double seconds, IEnumerable<string> smoke, IEnumerable<string> rooms, IEnumerable<string> hosts)
    {
        var mod = World.Api.ModLoader.GetMod("realsmoke")!;
        string[] lines =
        {
            $"# {mod.Info.Name} against siding walls and floors",
            "",
            $"Mod version {mod.Info.Version}, game version {GameVersion.ShortGameVersion}, probed {DateTime.Now:yyyy-MM-dd}.",
            "",
            "## Where smoke goes",
            "",
            $"Each row lights a firepit in a stone box whose only way out is the block under test, and reads the smoke Real Smoke holds in a second stone box the block opens onto, for {seconds:0} seconds of game time.",
            "`smoke passes` means some smoke reached the second box, with the most it held at one reading; `smoke held` means none did.",
            "The open rows are the controls: smoke crosses open air and stone stops it.",
            "A wall's room face is the one it claims, so a fire on it is a fire inside the room.",
            "The last column is what the block under test answers for the face towards the fire and the face towards the second box.",
            "",
            "| Arrangement | Smoke | Faces |",
            "| --- | --- | --- |",
        };
        string[] roomHeader =
        {
            "",
            "## Room retention under a flue",
            "",
            "A one-cell room with clay-filled walls and a planks floor, its roof planks except the cell above the room, which holds the block named.",
            "",
            "| Roof cell | Room | Counts |",
            "| --- | --- | --- |",
        };
        string[] hostHeader =
        {
            "",
            "## Fires hosted in a wall's cell",
            "",
            "Each is placed into the cell of a framed and clay-filled wall by a click, as furniture is. The firepit is lit and burns for the whole run.",
            "",
            "| Block | Result |",
            "| --- | --- |",
        };
        string directory = Path.GetFullPath("../../../../docs/compat", Path.GetDirectoryName(typeof(RealSmokeScenarios).Assembly.Location)!);
        Directory.CreateDirectory(directory);
        File.WriteAllLines($"{directory}/realsmoke-smoke.md", lines.Concat(smoke).Concat(roomHeader).Concat(rooms).Concat(hostHeader).Concat(hosts));
    }
}
