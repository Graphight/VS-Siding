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

// Runs with `just probe vsroofing`, the only run that loads VS Roofing. It asserts nothing: it records what Roofing's own clicks
// build in open air and what they do to a siding wall's cell, and what a siding frame click does to a roof's, for the
// walls-under-roofing proposal. docs/compat/vsroofing-roofs.md records the run.
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
        var walls = new List<string>();
        var frames = new List<string>();
        try
        {
            List<int[]> picks = await Picks(player);
            for (int i = 0; i < picks.Count; i++)
            {
                var (mode, boxes, selected) = await Build(player, picks[i], Cell(i, 6));
                modes.Add(mode);
                collision.Add(boxes);
                selection.Add(selected);
            }

            int k = 0;
            foreach (bool admitted in new[] { false, true })
            {
                Admit(admitted);
                foreach (string? infill in new[] { null, "game:clay-blue" })
                    foreach (Target target in WallTargets)
                        walls.Add(await IntoWall(player, infill, target, admitted, Cell(k++, -12)));
            }
            Admit(false);

            int m = 0;
            foreach (bool thatched in new[] { false, true })
                foreach (Target target in RoofTargets)
                    frames.Add(await IntoRoof(player, thatched, target, Cell(m++, -24)));
        }
        finally
        {
            Admit(false);
            harmony.GetType().GetMethod("UnpatchAll", new[] { typeof(string) })!.Invoke(harmony, new object[] { HarmonyId });
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }

        WriteReport(modes, collision, selection, walls, frames);
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
            slot.Itemstack?.Collectible.OnHeldInteractStart(slot, player.Entity, selection, null, true, ref handling);
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
    private async Task<(string Mode, string Collision, string Selection)> Build(ITestPlayer player, int[] path, BlockPos cell)
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
            return ($"| {pick} | {written} | {took} | {roof.Code}, errors {ErrorList()} | | | | |", $"| {pick} | | | |", $"| {pick} | | | |");

        Block wall = World.Api.World.GetBlock(new AssetLocation("vssiding:wall-wall-west"));
        string sides = string.Concat(BlockFacing.ALLFACES.Select(face => roof.SideIsSolid(cell, face.Index) ? face.Code[0].ToString().ToUpperInvariant() : "-"));
        string retention = string.Join(" ", BlockFacing.ALLFACES.Select(face => roof.GetRetention(cell, face, EnumRetentionType.Heat)));
        string mode = $"| {pick} | {written} | {took} | {roof.Code} | {Keys(cell)} | {sides} | {retention} | {roof.IsReplacableBy(wall).ToString().ToLowerInvariant()} |";
        var boxes = new List<(string Collision, string Selection)> { Boxes(cell) };

        BlockSelection onRoof = Face(cell, BlockFacing.SOUTH);
        foreach (var (item, shift) in new[] { (Thatch, true), (Support, false) })
        {
            await Hold(player, item, 4);
            string material = Try(() => WithKeys(player, shift, !shift, onRoof));
            var (after, selected) = Boxes(cell);
            boxes.Add(material == "the clicked block" ? (after, selected) : ($"{material} took the click: {after}", $"{material} took the click: {selected}"));
        }

        return (mode, $"| {pick} | {string.Join(" | ", boxes.Select(box => box.Collision))} |", $"| {pick} | {string.Join(" | ", boxes.Select(box => box.Selection))} |");
    }

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
        string wall = IsWall(cell)
            ? WallBuilder.Layers(World, cell) == layers ? "stays" : "stays, layers changed"
            : WallBuilder.GuestLayers(World, cell) is { } guest ? guest == layers ? "guest, layers kept" : "guest, layers changed" : "gone";

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
        return $"| {roof} | {target.Name} | {took} | {Where(cell, "vssiding")} | {World.BlockAt(cell).Code} | {ErrorList()} |";
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
        if (World.BlockEntityAt<BlockEntity>(cell) is not { } entity) return "no entity";
        var tree = new TreeAttribute();
        entity.ToTreeAttributes(tree);
        return string.Join(", ", tree
            .Where(entry => entry.Key is not ("posx" or "posy" or "posz" or "blockCode"))
            .Select(entry => entry.Value.GetValue() switch
            {
                Array array => $"{entry.Key}[{array.Length}]",
                ITreeAttribute => $"{entry.Key}{{}}",
                var value => $"{entry.Key} {Convert.ToString(value, CultureInfo.InvariantCulture)?.ToLowerInvariant()}",
            }));
    }

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

    // The roof admitted as hostable for this run only, as BuildHostableTable would have written it had HostRefusal passed it.
    private void Admit(bool admit)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        Type modSystem = World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType();
        var hostable = (bool[])modSystem.GetField("Hostable", flags)!.GetValue(null)!;
        var shifts = (double[]?[])modSystem.GetField("FaceShiftByBlock", flags)!.GetValue(null)!;
        MethodInfo faceShifts = modSystem.GetMethod("FaceShifts", flags)!;
        foreach (Block roof in Roofs)
        {
            hostable[roof.BlockId] = admit;
            shifts[roof.BlockId] = admit ? (double[])faceShifts.Invoke(null, new object[] { roof })! : null;
        }
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

    private void WriteReport(IEnumerable<string> modes, IEnumerable<string> collision, IEnumerable<string> selection, IEnumerable<string> walls, IEnumerable<string> frames)
    {
        var mod = World.Api.ModLoader.GetMod("vsroofing")!;
        string[] header =
        {
            $"# {mod.Info.Name} roofs against siding walls",
            "",
            $"Mod version {mod.Info.Version}, game version {GameVersion.ShortGameVersion}, probed {DateTime.Now:yyyy-MM-dd}.",
            "Nothing here is asserted: each row is what the two mods did on this run.",
            "Every click goes through vanilla's right-click order, the clicked block's interaction first and then the held item's, run on the server with a hammer in the off hand for Roofing and a saw for Siding.",
            "`Took the click` names the step that handled it.",
            "In-game errors are the codes the server sent the player; an error raised on the client alone does not reach this run.",
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
        string[] wallsHeader =
        {
            "",
            "## A Roofing frame click aimed at a wall's cell",
            "",
            "A wall framed in oak, bare or with clay infill, claims its west face, with granite under it and north of it.",
            "The player stands three blocks south with planks in hand, so a roof built there slopes along the wall's plane.",
            "`Admitted` rows mark every roof block hostable in Siding's table for the run, which no shipped patch does.",
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
            "A roof frame built as in the first table, bare or thatched, with granite under it and east of it, and a siding wall frame clicked at its cell with planks.",
            "",
            "| Roof | Click on | Took the click | Siding block at | Roof cell after | In-game errors |",
            "| --- | --- | --- | --- | --- | --- |",
        };
        string directory = Path.GetFullPath("../../../../docs/compat", Path.GetDirectoryName(typeof(RoofingScenarios).Assembly.Location)!);
        Directory.CreateDirectory(directory);
        File.WriteAllLines($"{directory}/vsroofing-roofs.md", header.Concat(modes).Concat(boxesHeader).Concat(collision).Concat(selectionHeader).Concat(selection)
            .Concat(wallsHeader).Concat(walls).Concat(framesHeader).Concat(frames));
    }
}
