using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Xunit;

namespace VSSiding.E2E.Tests;

// Runs with `just probe vsroofing`, the only run that loads VS Roofing. The probe asserts nothing: it records what Roofing's own clicks
// build in open air, as single pieces, gables and corners, what they do to a siding wall's cell, what its base fill does to a
// roof hosting one, and what a siding frame click does to a roof's cell, for the walls-under-roofing proposal.
// docs/compat/vsroofing-roofs.md records the run.
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class RoofingScenarios : AtlasScenarioBase
{
    private static bool RoofingLoaded => Environment.GetEnvironmentVariable("SIDING_PROBE") == "vsroofing";

    private const string Support = "game:rock-granite";
    private const string Plank = "game:plank-oak";
    private const string Thatch = "game:thatch";
    private const string Hammer = "game:hammer-copper";
    private const string Saw = "game:saw-copper";
    private const string HarmonyId = "vssiding.probe.roofing";

    // GetToolModes takes the client's player and its list is not built on a server, so the picks are walked by index from a fresh
    // stack: the top page holds 14, the last four writing orientation 0 to 3, and a page a top pick opens ends at its back pick.
    private const int TopPicks = 14;

    private static readonly List<string> Errors = new();

    private static void RecordError(string __0) => Errors.Add(__0);

    private sealed record Target(string Name, System.Func<BlockPos, BlockSelection> Select);

    private static readonly Target[] WallTargets =
    {
        new("the top of the block below", cell => Face(cell.DownCopy(), BlockFacing.UP)),
        new("the wall's room face", cell => Face(cell, BlockFacing.WEST)),
        new("the wall's outer face", cell => Face(cell, BlockFacing.EAST)),
        new("the wall's top", cell => Face(cell, BlockFacing.UP)),
        new("the side neighbour's face", cell => Face(cell.NorthCopy(), BlockFacing.SOUTH)),
    };

    private static readonly Target[] RoofTargets =
    {
        new("the top of the block below", cell => Face(cell.DownCopy(), BlockFacing.UP)),
        new("the roof's top", cell => Face(cell, BlockFacing.UP)),
        new("the side neighbour's face", cell => Face(cell.EastCopy(), BlockFacing.WEST)),
    };

    // Page 1's manual picks by slope and placement, the orientation picks, and the top page's ridge.
    private const int Manual = 3, RidgeAuto = 8, RisingNorth = 10, RisingEast = 11, RisingSouth = 12;

    private sealed record Gable(string Name, (int Dz, int Dy, int Pick)[] Side);

    // One side of each gable, upper piece first: south of the ridge's column, rising north, and mirrored north of it. Built top
    // down, so the granite under each cell is never a roof's cell, and the ridge goes on last, one level over the upper pieces.
    private static readonly Gable[] Gables =
    {
        new("3x1", new[] { (1, 0, 2), (2, 0, 1), (3, 0, 0) }),
        new("1x1", new[] { (1, 0, 7), (2, -1, 7) }),
        new("1x3", new[] { (1, 0, 14), (1, -1, 13), (1, -2, 12) }),
    };

    // A roof rising north, then one rising east behind it (north, uphill) or in front of it (south, downhill).
    private static readonly (string Name, BlockFacing Second)[] Corners = { ("outer", BlockFacing.NORTH), ("inner", BlockFacing.SOUTH) };

    private static readonly (bool Hosted, bool OnPanel)[] FillClicks = { (false, false), (true, false), (true, true) };

    private BlockPos Cell(int index, int row) => World.Spawn.Offset(4 * (index % 10) - 18, 3, row + 6 * (index / 10));

    private IEnumerable<Block> Roofs => World.Api.World.Blocks.Where(block => block.Code?.Domain == "vsroofing");

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true, TimeoutMs = 600000)]
    public async Task Probe_Should_RecordHowRoofingMeetsASidingWall()
    {
        if (!RoofingLoaded) return;

        Errors.Clear();
        ITestPlayer player = await WallBuilder.JoinBuilder(World, Hammer);
        object harmony = PatchIngameErrors(player.Player);
        var modes = new List<string>();
        var collision = new List<string>();
        var selection = new List<string>();
        var sealing = new List<string>();
        var gables = new List<string>();
        var corners = new List<string>();
        var walls = new List<string>();
        var fills = new List<string>();
        var frames = new List<string>();
        try
        {
            List<int[]> picks = await Picks(player);
            for (int i = 0; i < picks.Count; i++)
            {
                var (mode, boxes, selected, seal) = await Build(player, picks[i], Cell(i, 6));
                modes.Add(mode);
                collision.Add(boxes);
                selection.Add(selected);
                sealing.Add(seal);
            }

            for (int g = 0; g < Gables.Length; g++)
                gables.AddRange(await BuildGable(player, Gables[g], World.Spawn.Offset(8 * g - 14, 6, 0)));
            for (int c = 0; c < Corners.Length; c++)
            {
                var (name, second) = Corners[c];
                corners.AddRange(await RoofsFrom(player, name, World.Spawn.Offset(8 * c + 10, 3, 0), 2,
                    ((0, 0, 0), new[] { 0, RisingNorth }), ((second.Normali.X, 0, second.Normali.Z), new[] { 0, RisingEast })));
            }

            int k = 0;
            foreach (bool admitted in new[] { false, true })
            {
                System.Action? restore = admitted ? Admit() : null;
                try
                {
                    foreach (string? infill in new[] { null, "game:clay-blue" })
                        foreach (Target target in WallTargets)
                            walls.Add(await IntoWall(player, infill, target, admitted, Cell(k++, -12)));
                }
                finally
                {
                    restore?.Invoke();
                }
            }

            System.Action restoreTable = Admit();
            try
            {
                int f = 0;
                foreach (bool thatched in new[] { false, true })
                    foreach (var (hosted, onPanel) in FillClicks)
                        fills.Add(await Fill(player, thatched, hosted, onPanel, Cell(f++, -18)));
            }
            finally
            {
                restoreTable();
            }

            int m = 0;
            foreach (bool thatched in new[] { false, true })
                foreach (Target target in RoofTargets)
                    frames.Add(await IntoRoof(player, thatched, target, Cell(m++, -24)));
        }
        finally
        {
            harmony.GetType().GetMethod("UnpatchAll", new[] { typeof(string) })!.Invoke(harmony, new object[] { HarmonyId });
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }

        WriteReport(modes, collision, selection, sealing, gables, corners, walls, fills, frames);
    }

    // A roof built from the block below takes the wall's cell where it stands in open air, off no panel, and keeps the wall as its
    // guest through thatch, which turns roof-bare into roof-filled.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Roof_Should_HostTheWallUnshiftedAndKeepItThroughThatch_When_BuiltFromTheBlockBelow()
    {
        if (!RoofingLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            BlockPos open = Cell(0, 6), cell = Cell(1, 6);
            World.SetBlock(Support, open.DownCopy());
            World.SetBlock(Support, cell.DownCopy());
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), Plank, "game:clay-blue");
            (string?, string?, string?, string?, string?)? layers = WallBuilder.Layers(World, cell);

            player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
            async Task RoofFromBelow(BlockPos at)
            {
                Stand(player, at);
                await Hold(player, Plank, 4);
                RightClick(player, Face(at.DownCopy(), BlockFacing.UP));
            }
            string RoofBoxes(BlockPos at)
                => Format(World.BlockAt(at).GetSelectionBoxes(World.Api.World.BlockAccessor, at).Where(box => box.GetType().Name != "PanelSelectionBox").ToArray());

            await RoofFromBelow(open);
            await RoofFromBelow(cell);
            var hosted = (World.BlockAt(cell).Code.Path, RoofBoxes(cell), WallBuilder.GuestLayers(World, cell));

            await Hold(player, Thatch, 4);
            WithKeys(player, shift: true, ctrl: false, Face(cell, BlockFacing.SOUTH));

            Assert.Equal(((World.BlockAt(open).Code.Path, RoofBoxes(open), layers), hosted.Path.Replace("-bare-", "-filled-"), layers),
                (hosted, World.BlockAt(cell).Code.Path, WallBuilder.GuestLayers(World, cell)));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Roof_Should_GoInTheCellAbove_When_ClickedOntoTheWallsTop()
    {
        if (!RoofingLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            BlockPos cell = Cell(0, 6);
            World.SetBlock(Support, cell.DownCopy());
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), Plank, "game:clay-blue");
            var wall = (World.BlockAt(cell).Code.ToString(), WallBuilder.Layers(World, cell));

            player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
            Stand(player, cell);
            await Hold(player, Plank, 4);
            string took = RightClick(player, Face(cell, BlockFacing.UP));

            Assert.Equal(("the held item", "vsroofing", wall), (took, World.BlockAt(cell.UpCopy()).Code.Domain, (World.BlockAt(cell).Code.ToString(), WallBuilder.Layers(World, cell))));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // A frame clicked onto the block under a roof goes in the roof's cell as its guest, the wall a frame in open air would be, paid
    // for in full; one clicked onto the roof's top still goes in the cell above.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Frame_Should_BecomeTheRoofsGuest_When_ClickedOntoTheBlockBelow()
    {
        if (!RoofingLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            BlockPos open = Cell(0, 6), cell = Cell(1, 6);
            World.SetBlock(Support, open.DownCopy());
            World.SetBlock(Support, cell.DownCopy());
            player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
            Stand(player, cell);
            await Hold(player, Plank, 4);
            RightClick(player, Face(cell.DownCopy(), BlockFacing.UP));
            string roof = World.BlockAt(cell).Code.ToString();

            player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Saw)));
            ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
            async Task<(string Took, int Left)> Frame(BlockPos at, BlockSelection selection)
            {
                Stand(player, at);
                await Hold(player, Plank, 2);
                return (RightClick(player, selection), slot.Itemstack?.StackSize ?? 0);
            }
            (string?, (string?, string?, string?, string?, string?)?) Wall(BlockPos at) => (World.BlockAt(at).Code.ToString(), WallBuilder.Layers(World, at));

            await Frame(open, Face(open.DownCopy(), BlockFacing.UP));
            var framed = Wall(open);
            var underRoof = await Frame(cell, Face(cell.DownCopy(), BlockFacing.UP));
            var guest = (WallBuilder.GuestCode(World, cell), WallBuilder.GuestLayers(World, cell));
            var onTop = await Frame(cell, Face(cell, BlockFacing.UP));

            Assert.Equal((roof, ("the held item", 0), framed, ("the held item", 0), framed),
                (World.BlockAt(cell).Code.ToString(), underRoof, guest, onTop, Wall(cell.UpCopy())));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // A wall a roof hosts takes layers from a click on its panel: clay fills it, a trapdoor puts the guest on the vent block, and a
    // stair is refused, the roof keeping its cell throughout.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Guest_Should_TakeLayersFromItsPanel_When_ARoofHostsIt()
    {
        if (!RoofingLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        object harmony = PatchIngameErrors(player.Player);
        try
        {
            BlockPos clay = Cell(0, 6), vent = Cell(1, 6);
            var roofs = new List<string>();
            foreach (BlockPos cell in new[] { clay, vent })
            {
                World.SetBlock(Support, cell.DownCopy());
                Stand(player, cell);
                player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
                await Hold(player, Plank, 4);
                RightClick(player, Face(cell.DownCopy(), BlockFacing.UP));
                roofs.Add(World.BlockAt(cell).Code.ToString());
                player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Saw)));
                await Hold(player, Plank, 2);
                RightClick(player, Face(cell.DownCopy(), BlockFacing.UP));
            }

            async Task<(string, string?, (string?, string?, string?, string?, string?)?, string)> OnPanel(BlockPos cell, string code)
            {
                Stand(player, cell);
                await Hold(player, code, 4);
                Errors.Clear();
                string took = RightClick(player, PanelHit(cell, BlockFacing.SOUTH));
                return (took, WallBuilder.GuestCode(World, cell), WallBuilder.GuestLayers(World, cell), ErrorList());
            }

            var clayed = await OnPanel(clay, "game:clay-blue");
            var vented = await OnPanel(vent, "game:trapdoor-solid-oak-1");
            var stepped = await OnPanel(clay, "game:plankstairs-oak-up-north-free");
            static (string?, string?, string?, string?, string?)? Filled(string infill) => ("oak", infill, null, null, null);

            Assert.Equal(
                (("the clicked block", "vssiding:wall-wall-north", Filled("clay"), "none"),
                 ("the clicked block", "vssiding:wallvent-wall-north", Filled("trapdoor-solid-oak-1"), "none"),
                 ("the clicked block", "vssiding:wall-wall-north", Filled("clay"), "vssiding:underroof"), roofs[0], roofs[1]),
                (clayed, vented, stepped, World.BlockAt(clay).Code.ToString(), World.BlockAt(vent).Code.ToString()));
        }
        finally
        {
            harmony.GetType().GetMethod("UnpatchAll", new[] { typeof(string) })!.Invoke(harmony, new object[] { HarmonyId });
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // Mining a roof-hosted wall's panel peels its top layer as a wall's own break does, dropping it, and the last break takes the bare
    // frame, the roof keeping its cell throughout. The server runs the BreakBlock event, then OnBlockBroken while it stays PassThrough.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task Panel_Should_PeelALayerAndLeaveTheRoof_When_Mined()
    {
        if (!RoofingLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            BlockPos cell = Cell(0, 6);
            World.SetBlock(Support, cell.DownCopy());
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), Plank, "game:clay-blue", Plank);
            player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
            Stand(player, cell);
            await Hold(player, Plank, 4);
            RightClick(player, Face(cell.DownCopy(), BlockFacing.UP));
            string roof = World.BlockAt(cell).Code.ToString();
            player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;

            string State() => $"{World.BlockAt(cell).Code} {WallBuilder.GuestLayers(World, cell)?.ToString() ?? "no guest"}";
            string Mine()
            {
                BlockSelection hit = PanelHit(cell, BlockFacing.EAST);
                if (WallBuilder.TriggerBreakBlock(World, player, hit) == EnumHandling.PassThrough)
                    World.BlockAt(cell).OnBlockBroken(player.Entity.World, cell, player.Player);
                return State();
            }
            string hosted = State();
            string mined = string.Join(" | ", Mine(), Mine(), Mine());
            await World.Ticks(2);
            int Dropped(string code) => World.Api.World.GetEntitiesAround(cell.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, entity => entity is EntityItem)
                .Cast<EntityItem>().Where(item => item.Itemstack.Collectible.Code.ToString() == code).Sum(item => item.Itemstack.StackSize);

            Assert.Equal(
                ($"{roof} (oak, clay, , , planks)", $"{roof} (oak, clay, , , ) | {roof} (oak, , , , ) | {roof} no guest", 4, 4, (BlockSelection?)null),
                (hosted, mined, Dropped(Plank), Dropped("game:clay-blue"), WallBuilder.ServerBreakSelection(World)));
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // Roofing's ctrl click with a block, which gives a roof in open air its base fill, is refused on a roof hosting a wall, the roof
    // taking no fill and keeping its guest; a frame clicked into the filled roof's cell is refused too, the planks kept.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task BaseFill_Should_BeRefused_When_ItWouldShareARoofWithAWall()
    {
        if (!RoofingLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        object harmony = PatchIngameErrors(player.Player);
        try
        {
            BlockPos open = Cell(0, 6), hosted = Cell(1, 6);
            World.SetBlock(Support, open.DownCopy());
            World.SetBlock(Support, hosted.DownCopy());
            await WallBuilder.Raise(World, player, hosted, hosted.WestCopy(), Plank, "game:clay-blue");
            var layers = WallBuilder.Layers(World, hosted);

            ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
            void OffHand(string code) => player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(code)));
            async Task<(string Took, string Roof, bool Filled, string Errors)> RoofThenFill(BlockPos cell)
            {
                OffHand(Hammer);
                Stand(player, cell);
                await Hold(player, Plank, 4);
                RightClick(player, Face(cell.DownCopy(), BlockFacing.UP));
                await Hold(player, Support, 4);
                Errors.Clear();
                string took = WithKeys(player, shift: false, ctrl: true, Face(cell, BlockFacing.SOUTH));
                return (took, World.BlockAt(cell).Code.ToString(), Entries(cell).Any(entry => entry.Key == "cuboids"), ErrorList());
            }

            var filled = await RoofThenFill(open);
            var refused = await RoofThenFill(hosted);
            var guest = WallBuilder.GuestLayers(World, hosted);

            OffHand(Saw);
            Stand(player, open);
            await Hold(player, Plank, 2);
            Errors.Clear();
            var framed = (RightClick(player, Face(open.DownCopy(), BlockFacing.UP)), WallBuilder.GuestCode(World, open), slot.Itemstack?.StackSize ?? 0, ErrorList());

            Assert.Equal(
                (("the clicked block", "vsroofing:roof-filled-north", true, "none"), ("the clicked block", "vsroofing:roof-bare-north", false, "vssiding:rooffill"),
                 layers, ("the held item", (string?)null, 2, "vssiding:rooffill")),
                (filled, refused, guest, framed));
        }
        finally
        {
            harmony.GetType().GetMethod("UnpatchAll", new[] { typeof(string) })!.Invoke(harmony, new object[] { HarmonyId });
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // A corner wall claiming its west and north faces, with a roof rising north built into its cell and then one rising east beside
    // it, as the Corners rows are: the cell's roof turns into the corner piece, and the guest's panel is cut to the lower of y = 1 - z
    // and y = x under the outer corner, a hip, or to the higher under the inner one, a valley, each strip as high as that at its
    // middle.
    [AtlasScenario(RollbackWorld = true, StrictIsolation = true)]
    public async Task CornerWall_Should_BeCutToTheHipOrValley_When_TwoRoofsMeetOverIt()
    {
        if (!RoofingLoaded) return;

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        try
        {
            var expected = new List<(string, string?, string, int, string)>();
            var actual = new List<(string, string?, string, int, string)>();
            for (int c = 0; c < Corners.Length; c++)
            {
                var (name, second) = Corners[c];
                BlockPos cell = Cell(c, 6);
                World.SetBlock(Support, cell.DownCopy());
                player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Saw)));
                player.Entity.WatchedAttributes.SetString("vssidingFraming", "wall");
                player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
                await WallBuilder.Raise(World, player, cell, cell.WestCopy(), Plank, "game:clay-blue");
                player.Entity.WatchedAttributes.SetString("vssidingFraming", "corner");
                await WallBuilder.Press(World, player, cell, "game:stick", BlockFacing.WEST, new Vec3d(0, 0.5, 0.2));
                await RoofsFrom(player, name, cell, 0, ((0, 0, 0), new[] { 0, RisingNorth }), ((second.Normali.X, 0, second.Normali.Z), new[] { 0, RisingEast }));

                Cuboidf[] boxes = World.BlockAt(cell).GetSelectionBoxes(World.Api.World.BlockAccessor, cell);
                Cuboidf[] panel = boxes.Where(box => box.GetType().Name == "PanelSelectionBox").ToArray();
                System.Func<double, double, double> corner = name == "outer" ? Math.Min : Math.Max;
                string Tops(System.Func<Cuboidf, double> top) => string.Join(" ", panel.Select(box => Math.Round(16 * top(box), 2).ToString(CultureInfo.InvariantCulture)));

                expected.Add((name, "vssiding:wall-cornerout-west",
                    name == "outer"
                        ? "(0,0,8)-(16,8,16) (0,0,0)-(8,8,16) (8,8,0)-(16,16,8) (8,8,0)-(16,16,8)"
                        : "(0,0,8)-(8,8,16) (0,0,8)-(8,8,16) (0,8,0)-(16,16,8) (8,8,0)-(16,16,16)",
                    2 * 8, Tops(box => Math.Min(1, corner(1 - (box.Z1 + box.Z2) / 2, (box.X1 + box.X2) / 2)))));
                actual.Add((name, WallBuilder.GuestCode(World, cell), Format(boxes.Where(box => box.GetType().Name != "PanelSelectionBox").ToArray()),
                    panel.Length, Tops(box => box.Y2)));
            }

            Assert.Equal(expected, actual);
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }
    }

    // A hit on the room side of the first panel box the roof's selection appends.
    private BlockSelection PanelHit(BlockPos cell, BlockFacing face)
    {
        Cuboidf[] boxes = World.BlockAt(cell).GetSelectionBoxes(World.Api.World.BlockAccessor, cell);
        int index = Array.FindIndex(boxes, box => box.GetType().Name == "PanelSelectionBox");
        Assert.True(index >= 0, "the roof selects no panel box");
        Cuboidf panel = boxes[index];
        return new BlockSelection
        {
            Position = cell.Copy(), Face = face, SelectionBoxIndex = index,
            HitPosition = new Vec3d((panel.X1 + panel.X2) / 2, (panel.Y1 + panel.Y2) / 2, face == BlockFacing.SOUTH ? panel.Z2 : panel.Z1),
        };
    }

    private static BlockSelection Face(BlockPos pos, BlockFacing face)
        => new() { Position = pos.Copy(), Face = face, HitPosition = new Vec3d(0.5, 0.5, 0.5).Add(face.Normalf.X * 0.5, face.Normalf.Y * 0.5, face.Normalf.Z * 0.5) };

    // A fresh stack in hand, with each tool mode of the path picked in turn.
    private static async Task<ItemStack> Hold(ITestPlayer player, string code, int quantity, params int[] path)
    {
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        slot.Itemstack = null;
        await player.GiveItem(code, quantity);
        foreach (int mode in path) slot.Itemstack!.Collectible.SetToolMode(slot, player.Player, Face(player.Entity.Pos.AsBlockPos, BlockFacing.UP), mode);
        return slot.Itemstack!;
    }

    // Every pick that writes more than the page it is on: a page opener and the page's empty slots write nothing of their own.
    private static async Task<List<int[]>> Picks(ITestPlayer player)
    {
        var paths = new List<int[]>();
        for (int top = 0; top < TopPicks; top++)
        {
            ITreeAttribute opened = (await Hold(player, Plank, 1, top)).Attributes;
            if (!opened.HasAttribute("vsroofing.page"))
            {
                paths.Add(new[] { top });
                continue;
            }
            for (int sub = 0; sub < 32; sub++)
            {
                ITreeAttribute picked = (await Hold(player, Plank, 1, top, sub)).Attributes;
                if (picked.GetInt("vsroofing.page") == 0) break;
                if (picked.Count > opened.Count) paths.Add(new[] { top, sub });
            }
        }
        return paths;
    }

    // The order vanilla's client tries a right click in (SystemMouseInWorldInteractions), run on the server, naming the step that
    // took it. A block in hand that nothing took would be placed next, which no click here needs.
    private string RightClick(ITestPlayer player, BlockSelection selection)
    {
        ItemSlot slot = player.Player.InventoryManager.ActiveHotbarSlot;
        Block clicked = World.BlockAt(selection.Position);
        bool shift = player.Entity.Controls.ShiftKey;
        bool heldFirst = slot.Itemstack?.Collectible.HeldPriorityInteract == true;
        bool Use() => clicked.OnBlockInteractStart(player.Entity.World, player.Player, selection);
        bool Held()
        {
            var handling = EnumHandHandling.NotHandled;
            slot.Itemstack?.Collectible.OnHeldUseStart(slot, player.Entity, selection, null, EnumHandInteract.HeldItemInteract, true, ref handling);
            return handling != EnumHandHandling.NotHandled;
        }

        if (!shift && Use()) return "the clicked block";
        if ((!shift || heldFirst) && Held()) return "the held item";
        if (shift && clicked.PlacedPriorityInteract && Use()) return "the clicked block";
        if (shift && !heldFirst && Held()) return "the held item";
        if (shift && !clicked.PlacedPriorityInteract && Use()) return "the clicked block";
        return "nothing";
    }

    private string WithKeys(ITestPlayer player, bool shift, bool ctrl, BlockSelection selection)
    {
        // A held key reaches the server as both its raw flag and the action it is bound to by default.
        EntityControls controls = player.Entity.Controls;
        controls.ShiftKey = controls.Sneak = shift;
        controls.CtrlKey = controls.Sprint = ctrl;
        try
        {
            return RightClick(player, selection);
        }
        finally
        {
            controls.ShiftKey = controls.Sneak = controls.CtrlKey = controls.Sprint = false;
        }
    }

    // A frame on a granite block in open air, the player three blocks south of it, then thatch and a granite fill clicked onto it.
    private async Task<(string Mode, string Collision, string Selection, string Sealing)> Build(ITestPlayer player, int[] path, BlockPos cell)
    {
        string pick = string.Join(" then ", path);
        World.SetBlock(Support, cell.DownCopy());
        Stand(player, cell);
        ItemStack stack = await Hold(player, Plank, 4, path);
        string written = string.Join(", ", stack.Attributes.Select(entry => $"{entry.Key.Replace("vsroofing.", "")} {entry.Value.GetValue()}"));
        Errors.Clear();
        string took = Try(() => RightClick(player, Face(cell.DownCopy(), BlockFacing.UP)));
        Block roof = World.BlockAt(cell);
        if (roof.Code.Domain != "vsroofing")
            return ($"| {pick} | {written} | {took} | {roof.Code}, errors {ErrorList()} | | | | |", $"| {pick} | | | |", $"| {pick} | | | |", $"| {pick} | | | | |");

        Block wall = World.Api.World.GetBlock(new AssetLocation("vssiding:wall-wall-west"));
        string mode = $"| {pick} | {written} | {took} | {roof.Code} | {Keys(cell)} | {Sides(cell)} | {Retention(cell)} | {roof.IsReplacableBy(wall).ToString().ToLowerInvariant()} |";
        var boxes = new List<(string Collision, string Selection)> { Boxes(cell) };
        var sealing = new List<string>();

        BlockSelection onRoof = Face(cell, BlockFacing.SOUTH);
        foreach (var (item, shift) in new[] { (Thatch, true), (Support, false) })
        {
            await Hold(player, item, 4);
            string material = Try(() => WithKeys(player, shift, !shift, onRoof));
            string by = material == "the clicked block" ? "" : $"{material} took the click: ";
            var (after, selected) = Boxes(cell);
            boxes.Add(($"{by}{after}", $"{by}{selected}"));
            sealing.Add($"{by}{Sides(cell)} | {Retention(cell)}");
        }

        return (mode, $"| {pick} | {string.Join(" | ", boxes.Select(box => box.Collision))} |", $"| {pick} | {string.Join(" | ", boxes.Select(box => box.Selection))} |",
            $"| {pick} | {string.Join(" | ", sealing)} |");
    }

    private string Sides(BlockPos cell)
        => string.Concat(BlockFacing.ALLFACES.Select(face => World.BlockAt(cell).SideIsSolid(cell, face.Index) ? face.Code[0].ToString().ToUpperInvariant() : "-"));

    private string Retention(BlockPos cell)
        => string.Join(" ", BlockFacing.ALLFACES.Select(face => World.BlockAt(cell).GetRetention(cell, face, EnumRetentionType.Heat)));

    // Each side's pieces upper first with a manual pick, then the ridge with Roofing's own ridge pick.
    private Task<List<string>> BuildGable(ITestPlayer player, Gable gable, BlockPos ridgeColumn)
    {
        var clicks = new List<((int, int, int), int[])>();
        foreach (var (sign, rising) in new[] { (1, RisingNorth), (-1, RisingSouth) })
            clicks.AddRange(gable.Side.Select(piece => ((0, piece.Dy, sign * piece.Dz), new[] { rising, Manual, piece.Pick })));
        clicks.Add(((0, 1, 0), new[] { RisingNorth, RidgeAuto }));
        return RoofsFrom(player, gable.Name, ridgeColumn, 4, clicks.ToArray());
    }

    // Each click a plank click on the top of granite put under its cell for the click and broken again after, then every roof
    // block within reach of the origin, and any click whose cell holds no roof.
    private async Task<List<string>> RoofsFrom(ITestPlayer player, string name, BlockPos origin, int reach, params ((int X, int Y, int Z) Offset, int[] Path)[] clicks)
    {
        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
        var took = new Dictionary<(int, int, int), (string Pick, string Took)>();
        foreach (var (offset, path) in clicks)
        {
            BlockPos cell = origin.AddCopy(offset.X, offset.Y, offset.Z), below = cell.DownCopy();
            bool support = World.BlockAt(below).BlockId == 0;
            if (support) World.SetBlock(Support, below);
            Stand(player, cell);
            await Hold(player, Plank, 4, path);
            Errors.Clear();
            string result = Try(() => RightClick(player, Face(below, BlockFacing.UP)));
            took[offset] = (string.Join(" then ", path), Errors.Count == 0 ? result : $"{result}, errors {ErrorList()}");
            if (support) World.Api.World.BlockAccessor.BreakBlock(below, null, 0);
        }
        await World.Ticks(5);

        var rows = new List<string>();
        for (int dy = -reach; dy <= reach; dy++)
            for (int dz = -reach; dz <= reach; dz++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    BlockPos cell = origin.AddCopy(dx, dy, dz);
                    bool clicked = took.Remove((dx, dy, dz), out var click);
                    Block block = World.BlockAt(cell);
                    if (block.Code?.Domain != "vsroofing" && !clicked) continue;
                    if (block.Code?.Domain != "vsroofing")
                    {
                        rows.Add($"| {name} | ({dx},{dy},{dz}) | {click.Pick} | {click.Took} | {block.Code} | | | |");
                        continue;
                    }
                    var (collision, selection) = Boxes(cell);
                    rows.Add($"| {name} | ({dx},{dy},{dz}) | {click.Pick} | {click.Took} | {block.Code} | {Keys(cell)} | {collision} | {selection} |");
                }
        return rows;
    }

    // A roof frame from a plank click on the block below, over a clay wall claiming its west face unless in open air, thatched or
    // not, then a ctrl click with granite on the roof's own first box or on the guest wall's panel.
    private async Task<string> Fill(ITestPlayer player, bool thatched, bool hosted, bool onPanel, BlockPos cell)
    {
        World.SetBlock(Support, cell.DownCopy());
        (string?, string?, string?, string?, string?) layers = default;
        if (hosted)
        {
            player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Saw)));
            await WallBuilder.Raise(World, player, cell, cell.WestCopy(), Plank, "game:clay-blue");
            layers = WallBuilder.Layers(World, cell);
        }

        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
        Stand(player, cell);
        await Hold(player, Plank, 4);
        RightClick(player, Face(cell.DownCopy(), BlockFacing.UP));
        if (thatched)
        {
            await Hold(player, Thatch, 4);
            WithKeys(player, shift: true, ctrl: false, Face(cell, BlockFacing.SOUTH));
        }
        string roof = $"{World.BlockAt(cell).Code} | {(hosted ? WallState(cell, layers) : "no wall")} | {(onPanel ? "the panel's west face" : "the roof's south face")}";
        var entriesBefore = Entries(cell);

        BlockSelection selection = Face(cell, BlockFacing.SOUTH);
        if (onPanel)
        {
            Cuboidf[] boxes = World.BlockAt(cell).GetSelectionBoxes(World.Api.World.BlockAccessor, cell);
            int index = Array.FindIndex(boxes, box => box.GetType().Name == "PanelSelectionBox");
            if (index < 0) return $"| {roof} | no panel box | | | | | | |";
            Cuboidf panel = boxes[index];
            selection = new BlockSelection
            {
                Position = cell.Copy(), Face = BlockFacing.WEST, SelectionBoxIndex = index,
                HitPosition = new Vec3d(panel.X1, (panel.Y1 + panel.Y2) / 2, (panel.Z1 + panel.Z2) / 2),
            };
        }
        await Hold(player, Support, 4);
        Errors.Clear();
        string took = Try(() => WithKeys(player, shift: false, ctrl: true, selection));
        var (collision, selected) = Boxes(cell);
        return $"| {roof} | {took} | {World.BlockAt(cell).Code} | {Changes(entriesBefore, Entries(cell))} | {collision} | {selected} | {(hosted ? WallState(cell, layers) : "no wall")} | {ErrorList()} |";
    }

    private string WallState(BlockPos cell, (string?, string?, string?, string?, string?) layers)
        => IsWall(cell)
            ? WallBuilder.Layers(World, cell) == layers ? "stays" : "stays, layers changed"
            : WallBuilder.GuestLayers(World, cell) is { } guest ? guest == layers ? "guest, layers kept" : "guest, layers changed" : "gone";

    // A wall claiming its west face with granite under it and north of it, then a Roofing frame click aimed at its cell.
    private async Task<string> IntoWall(ITestPlayer player, string? infill, Target target, bool admitted, BlockPos cell)
    {
        World.SetBlock(Support, cell.DownCopy());
        World.SetBlock(Support, cell.NorthCopy());
        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Saw)));
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), Plank, infill);
        var layers = WallBuilder.Layers(World, cell);

        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
        Stand(player, cell);
        await Hold(player, Plank, 4);
        Errors.Clear();
        string took = Try(() => RightClick(player, target.Select(cell)));
        string errors = ErrorList();
        string roofAt = Where(cell, "vsroofing");
        string wall = WallState(cell, layers);

        string broken = "";
        if (World.BlockAt(cell).Code.Domain == "vsroofing")
        {
            World.Api.World.BlockAccessor.BreakBlock(cell, player.Player);
            try
            {
                await World.Until(() => IsWall(cell), 40);
                broken = WallBuilder.Layers(World, cell) == layers ? "wall back" : "wall back, layers changed";
            }
            catch (ScenarioTimeoutException)
            {
                broken = $"no wall: {World.BlockAt(cell).Code}";
            }
        }
        return $"| {(infill == null ? "bare frame" : "clay infill")} | {target.Name} | {(admitted ? "yes" : "no")} | {took} | {roofAt} | {wall} | {errors} | {broken} |";
    }

    // A roof frame from a plank click, thatched or not, with granite under it and east of it, then a siding frame click aimed at its cell.
    private async Task<string> IntoRoof(ITestPlayer player, bool thatched, Target target, BlockPos cell)
    {
        World.SetBlock(Support, cell.DownCopy());
        World.SetBlock(Support, cell.EastCopy());
        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Hammer)));
        Stand(player, cell);
        await Hold(player, Plank, 4);
        RightClick(player, Face(cell.DownCopy(), BlockFacing.UP));
        if (thatched)
        {
            await Hold(player, Thatch, 4);
            WithKeys(player, shift: true, ctrl: false, Face(cell, BlockFacing.SOUTH));
        }
        string roof = World.BlockAt(cell).Code.ToString();

        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation(Saw)));
        await Hold(player, Plank, 4);
        Errors.Clear();
        string took = Try(() => RightClick(player, target.Select(cell)));
        string guest = WallBuilder.GuestCode(World, cell) is { } code ? $"{code}, framing {WallBuilder.GuestLayers(World, cell)?.Framing}" : "none";
        return $"| {roof} | {target.Name} | {took} | {Where(cell, "vssiding")} | {guest} | {World.BlockAt(cell).Code} | {ErrorList()} |";
    }

    private static void Stand(ITestPlayer player, BlockPos cell)
    {
        player.Entity.Pos.SetPos(cell.X + 0.5, cell.Y, cell.Z + 3.5);
        player.Entity.Pos.Yaw = 0;
    }

    private static string Try(System.Func<string> click)
    {
        try
        {
            return click();
        }
        catch (Exception e)
        {
            return $"throws {(e is TargetInvocationException { InnerException: { } inner } ? inner : e).GetType().Name}";
        }
    }

    private static string ErrorList() => Errors.Count == 0 ? "none" : string.Join(", ", Errors);

    private bool IsWall(BlockPos cell) => World.BlockAt(cell).Code is { Domain: "vssiding" } code && code.Path.StartsWith("wall");

    // Where blocks of the domain stand in the cells round the given one, by direction from it.
    private string Where(BlockPos cell, string domain)
    {
        var found = new List<string>();
        for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (World.BlockAt(cell.AddCopy(dx, dy, dz)).Code?.Domain != domain) continue;
                    string[] parts = { dz < 0 ? "north" : dz > 0 ? "south" : "", dx > 0 ? "east" : dx < 0 ? "west" : "", dy > 0 ? "above" : dy < 0 ? "below" : "" };
                    found.Add(parts.All(part => part == "") ? "the cell" : string.Join(" ", parts.Where(part => part != "")));
                }
        return found.Count == 0 ? "nowhere" : string.Join(", ", found);
    }

    // The saved tree's keys past the position and code; a scalar with its value, an array with its length.
    private string Keys(BlockPos cell)
    {
        if (World.BlockEntityAt<BlockEntity>(cell) is null) return "no entity";
        return string.Join(", ", Entries(cell).Select(entry => entry.Value.GetValue() switch
        {
            Array array => $"{entry.Key}[{array.Length}]",
            ITreeAttribute => $"{entry.Key}{{}}",
            var value => $"{entry.Key} {Render(value, full: false)}",
        }));
    }

    private List<KeyValuePair<string, IAttribute>> Entries(BlockPos cell)
    {
        if (World.BlockEntityAt<BlockEntity>(cell) is not { } entity) return new();
        var tree = new TreeAttribute();
        entity.ToTreeAttributes(tree);
        return tree.Where(entry => entry.Key is not ("posx" or "posy" or "posz" or "blockCode")).ToList();
    }

    // Each key added, dropped or changed, compared in full and shown with an array of more than 16 by its length.
    private static string Changes(List<KeyValuePair<string, IAttribute>> before, List<KeyValuePair<string, IAttribute>> after)
    {
        var was = before.ToDictionary(entry => entry.Key, entry => entry.Value.GetValue());
        var changes = new List<string>();
        foreach (var (key, attribute) in after)
        {
            object value = attribute.GetValue();
            if (!was.Remove(key, out object? old)) changes.Add($"+{key} {Render(value, full: false)}");
            else if (Render(old, full: true) != Render(value, full: true)) changes.Add($"{key} {Render(old, full: false)} to {Render(value, full: false)}");
        }
        changes.AddRange(was.Keys.Select(key => $"-{key}"));
        return changes.Count == 0 ? "none" : string.Join(", ", changes);
    }

    private static string Render(object? value, bool full) => value switch
    {
        ITreeAttribute tree => $"{{{string.Join(", ", tree.Select(entry => $"{entry.Key} {Render(entry.Value.GetValue(), full)}"))}}}",
        Array array when full || array.Length <= 16 => $"[{string.Join(" ", array.Cast<object?>().Select(item => Render(item, full)))}]",
        Array array => $"[{array.Length}]",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)?.ToLowerInvariant() ?? "null",
    };

    private (string Collision, string Selection) Boxes(BlockPos cell)
    {
        Block block = World.BlockAt(cell);
        IBlockAccessor accessor = World.Api.World.BlockAccessor;
        return (Format(block.GetCollisionBoxes(accessor, cell)), Format(block.GetSelectionBoxes(accessor, cell)));
    }

    // In sixteenths, rounded to a tenth.
    private static string Format(Cuboidf[]? boxes)
    {
        static string S(float value)
        {
            double rounded = Math.Round(value * 16, 1);
            return rounded == 0 ? "0" : rounded.ToString(CultureInfo.InvariantCulture);
        }
        return boxes is not { Length: > 0 } ? "none" : string.Join(" ", boxes.Select(b => $"({S(b.X1)},{S(b.Y1)},{S(b.Z1)})-({S(b.X2)},{S(b.Y2)},{S(b.Z2)})"));
    }

    // The roof admitted as hostable, as BuildHostableTable would have written it had HostRefusal passed it; the action returned
    // puts back what the table held.
    private System.Action Admit()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        Type modSystem = World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType();
        var hostable = (bool[])modSystem.GetField("Hostable", flags)!.GetValue(null)!;
        var shifts = (double[]?[])modSystem.GetField("FaceShiftByBlock", flags)!.GetValue(null)!;
        MethodInfo faceShifts = modSystem.GetMethod("FaceShifts", flags)!;
        var saved = Roofs.Select(roof => (roof.BlockId, Hostable: hostable[roof.BlockId], Shift: shifts[roof.BlockId])).ToArray();
        foreach (Block roof in Roofs)
        {
            hostable[roof.BlockId] = true;
            shifts[roof.BlockId] = (double[])faceShifts.Invoke(null, new object[] { roof })!;
        }
        return () =>
        {
            foreach (var (id, admitted, shift) in saved)
            {
                hostable[id] = admitted;
                shifts[id] = shift;
            }
        };
    }

    // Each class from the roof's own up to Block, with the members it overrides.
    private static IEnumerable<string> Overrides(Block roof)
    {
        for (Type type = roof.GetType(); type != typeof(Block); type = type.BaseType!)
            yield return $"- `{type.FullName}`: " + string.Join(", ", type
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => method.GetBaseDefinition().DeclaringType != type)
                .Select(method => $"`{method.Name}`").Distinct().OrderBy(name => name, StringComparer.Ordinal));
    }

    // A refusal reaches the player as an in-game error, a packet Atlas does not decode, so a prefix on the server player's
    // SendIngameError keeps each code. Harmony is the copy the game loads, reached by name.
    private static object PatchIngameErrors(IServerPlayer player)
    {
        Assembly lib = AppDomain.CurrentDomain.GetAssemblies().First(assembly => assembly.GetName().Name == "0Harmony");
        object harmony = Activator.CreateInstance(lib.GetType("HarmonyLib.Harmony")!, HarmonyId)!;
        MethodInfo patch = harmony.GetType().GetMethods().First(method => method.Name == "Patch");
        var args = new object?[patch.GetParameters().Length];
        args[0] = player.GetType().GetMethod("SendIngameError");
        args[1] = Activator.CreateInstance(lib.GetType("HarmonyLib.HarmonyMethod")!, typeof(RoofingScenarios).GetMethod(nameof(RecordError), BindingFlags.NonPublic | BindingFlags.Static)!);
        patch.Invoke(harmony, args);
        player.SendIngameError(HarmonyId);
        if (!Errors.Remove(HarmonyId)) throw new InvalidOperationException("The SendIngameError prefix recorded nothing.");
        return harmony;
    }

    private void WriteReport(IEnumerable<string> modes, IEnumerable<string> collision, IEnumerable<string> selection, IEnumerable<string> sealing,
        IEnumerable<string> gables, IEnumerable<string> corners, IEnumerable<string> walls, IEnumerable<string> fills, IEnumerable<string> frames)
    {
        var mod = World.Api.ModLoader.GetMod("vsroofing")!;
        string[] header =
        {
            $"# {mod.Info.Name} roofs against siding walls",
            "",
            $"Mod version {mod.Info.Version}, game version {GameVersion.ShortGameVersion}, probed {DateTime.Now:yyyy-MM-dd}.",
            "Nothing here is asserted: each row is what the two mods did on this run.",
            "Every click goes through vanilla's right-click order, the clicked block's interaction first and then the held item's, run on the server with a hammer in the off hand for Roofing and a saw for Siding.",
            "`Took the click` names the step that handled it; `nothing` means vanilla would next place a held block against the clicked face.",
            "In-game errors are the codes the server sent the player; an error raised on the client alone does not reach this run.",
            "",
            "## Roof block overrides",
            "",
            "The roof's block class and each class above it up to `Block`, with every member it declares over its base, read by reflection.",
            "",
        };
        string[] modesHeader =
        {
            "",
            "## Tool modes",
            "",
            "GetToolModes takes the client's player and Roofing builds its list only there, so each pick is the index SetToolMode takes from a fresh stack of planks, with a second index for a pick on a page the first opens.",
            "The stack attributes it writes name it. A page opener and a page's empty slots write nothing of their own and have no row.",
            "Each frame is one plank click on the top of a granite block in open air, with the player three blocks south.",
            "`Sides solid` is SideIsSolid per face, north, east, south, west, up and down, with a dash where the face is not solid; `Heat retention` is GetRetention per face in the same order.",
            "`Wall may replace` is IsReplacableBy for `vssiding:wall-wall-west`.",
            "",
            "| Pick | Stack attributes | Took the click | Block | Entity keys | Sides solid | Heat retention | Wall may replace |",
            "| --- | --- | --- | --- | --- | --- | --- | --- |",
        };
        string[] boxesHeader =
        {
            "",
            "## Boxes",
            "",
            "In sixteenths of a block, (x1,y1,z1)-(x2,y2,z2), z running south.",
            "`Thatched` is after a sneak click with thatch on the frame, `Filled` after a ctrl click with granite on the thatched roof.",
            "",
            "### Collision boxes",
            "",
            "| Pick | Frame | Thatched | Filled |",
            "| --- | --- | --- | --- |",
        };
        string[] selectionHeader =
        {
            "",
            "### Selection boxes",
            "",
            "| Pick | Frame | Thatched | Filled |",
            "| --- | --- | --- | --- |",
        };
        string[] sealingHeader =
        {
            "",
            "## Sealing after thatch and fill",
            "",
            "SideIsSolid and GetRetention as in the tool modes table, after the thatch and then the granite fill.",
            "",
            "| Pick | Thatched sides solid | Thatched heat retention | Filled sides solid | Filled heat retention |",
            "| --- | --- | --- | --- | --- |",
        };
        string[] gablesHeader =
        {
            "",
            "## Gables",
            "",
            "Each gable is one slice across, running north and south: the pieces south of the ridge's column rise north, and those north of it rise south.",
            "Each piece is one plank click on granite put under its cell and broken after, built from the upper piece down with the orientation pick, then the manual slope and placement pick.",
            "The ridge goes on last with Roofing's own ridge pick, one level over the upper pieces.",
            "`Cell` is in blocks from the ridge's column at the upper pieces' level, (x,y,z); every roof block within four blocks has a row, and a click whose cell holds no roof has one too.",
            "Boxes are in sixteenths of a block, as above.",
            "",
            "| Gable | Cell | Pick | Took the click | Block | Entity keys | Collision | Selection |",
            "| --- | --- | --- | --- | --- | --- | --- | --- |",
        };
        string[] cornersHeader =
        {
            "",
            "## Corners",
            "",
            "Two perpendicular roofs, each one Standard pick then an orientation pick, built as the gables are: a roof rising north at (0,0,0), then a roof rising east north of it (behind, uphill) for the outer corner or south of it (in front, downhill) for the inner one.",
            "Every roof block within two blocks has a row.",
            "",
            "| Corner | Cell | Pick | Took the click | Block | Entity keys | Collision | Selection |",
            "| --- | --- | --- | --- | --- | --- | --- | --- |",
        };
        string[] fillsHeader =
        {
            "",
            "## Base fill on a hosted roof",
            "",
            "A Standard roof frame from a plank click on granite under the cell, in open air or over a wall framed in oak with clay infill claiming its west face, the roof admitted as hostable for the run; then thatch with a sneak click on the roof's south face, or not; then a ctrl click with granite.",
            "The ctrl click lands on the roof's first selection box, on its south face, or on the guest wall's first panel box, on its west face.",
            "A hosted roof takes no off-panel shift, so its own boxes span the cell over the panel's.",
            "`Wall before` is the wall's cell once the roof is built and thatched, and `Key changes` the saved tree's keys the ctrl click added (+), dropped (-) or changed.",
            "",
            "| Roof | Wall before | Click on | Took the click | Roof after | Key changes | Collision after | Selection after | Wall after | In-game errors |",
            "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
        };
        string[] wallsHeader =
        {
            "",
            "## A Roofing frame click aimed at a wall's cell",
            "",
            "A wall framed in oak, bare or with clay infill, claims its west face, with granite under it and north of it.",
            "The player stands three blocks south with planks in hand, so a roof built there slopes along the wall's plane.",
            "`Admitted` rows mark every roof block hostable in Siding's table for the run; the others leave the table as the mod built it.",
            "`Roof at` is where a roof block stands after the click, from the wall's cell, and `Wall` whether that cell still holds the wall, holds it as a guest or neither.",
            "`Roof broken` is the wall's cell after a roof in it is broken.",
            "",
            "| Wall | Click on | Admitted | Took the click | Roof at | Wall | In-game errors | Roof broken |",
            "| --- | --- | --- | --- | --- | --- | --- | --- |",
        };
        string[] framesHeader =
        {
            "",
            "## A siding frame click aimed at a roof's cell",
            "",
            "A roof frame built as in the tool modes table, bare or thatched, with granite under it and east of it, and a siding wall frame clicked at its cell with planks.",
            "`Guest` is the wall the roof's cell holds as its guest after the click.",
            "",
            "| Roof | Click on | Took the click | Siding block at | Guest | Roof cell after | In-game errors |",
            "| --- | --- | --- | --- | --- | --- | --- |",
        };
        string directory = Path.GetFullPath("../../../../docs/compat", Path.GetDirectoryName(typeof(RoofingScenarios).Assembly.Location)!);
        Directory.CreateDirectory(directory);
        File.WriteAllLines($"{directory}/vsroofing-roofs.md", header.Concat(Overrides(Roofs.First())).Concat(modesHeader).Concat(modes)
            .Concat(boxesHeader).Concat(collision).Concat(selectionHeader).Concat(selection).Concat(sealingHeader).Concat(sealing)
            .Concat(gablesHeader).Concat(gables).Concat(cornersHeader).Concat(corners)
            .Concat(wallsHeader).Concat(walls).Concat(fillsHeader).Concat(fills).Concat(framesHeader).Concat(frames));
    }
}
