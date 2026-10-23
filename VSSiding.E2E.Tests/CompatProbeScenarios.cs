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
using Xunit;

namespace VSSiding.E2E.Tests;

// Not part of `just e2e`: `just probe <modid>` downloads that mod and its dependencies into compat/mods/slotN.zip, the fixed names Atlas stages, and runs this.
// It hosts one block of each type from the mod in a wall of its own and writes docs/compat/<modid>.md.
[Trait("Category", "Probe")]
[AtlasWorld(Mods = new[] { "../../../../compat/mods/slot0.zip", "../../../../compat/mods/slot1.zip", "../../../../compat/mods/slot2.zip", "../../../../compat/mods/slot3.zip", "../../../../compat/mods/slot4.zip", "../../../../compat/mods/slot5.zip" })]
public class CompatProbeScenarios : AtlasScenarioBase
{
    [AtlasScenario(FreshWorld = true)]
    public async Task Probe_Should_ReportEveryBlockTypeOfTheMod()
    {
        string? modid = Environment.GetEnvironmentVariable("SIDING_PROBE");
        if (string.IsNullOrEmpty(modid)) return;

        MethodInfo hostRefusal = World.Api.ModLoader.GetModSystem("VSSiding.SidingModSystem").GetType()
            .GetMethod("HostRefusal", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!;
        var blocks = World.Api.World.Blocks
            .Where(block => block.Code?.Domain == modid && block.Id != 0)
            .GroupBy(block => block.Code.FirstCodePart())
            .Select(group => group.FirstOrDefault(block => block.Code.Path.Contains("east")) ?? group.First())
            .OrderBy(block => block.Code.FirstCodePart(), StringComparer.Ordinal)
            .ToList();

        ITestPlayer player = await WallBuilder.JoinBuilder(World);
        var rows = new List<string>();
        try
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                Block block = blocks[i];
                string verdict;
                try
                {
                    verdict = (string?)hostRefusal.Invoke(null, new object[] { block }) is { } rule
                        ? $"refused: {rule}"
                        : await Host(player, World.Spawn.Offset(1 + 3 * (i % 10), 2, 3 * (i / 10)), block);
                }
                catch (Exception e)
                {
                    verdict = $"placement failed: {(e is TargetInvocationException { InnerException: { } inner } ? inner : e).GetType().Name}";
                }
                rows.Add($"| {block.Code.FirstCodePart()} | {verdict} |");
            }
        }
        finally
        {
            player.Player.Disconnect();
            await World.Until(() => !player.IsConnected);
        }

        var mod = World.Api.ModLoader.GetMod(modid)!;
        string[] lines =
        {
            $"# {mod.Info.Name} against a siding wall",
            "",
            $"Mod version {mod.Info.Version}, game version {GameVersion.ShortGameVersion}, probed {DateTime.Now:yyyy-MM-dd}.",
            "Each row is one block type of the mod, tried in the cell of a wall built for it.",
            "`hosts` means the wall became the block's guest.",
            "`refused` names the rule that keeps the block out of a wall's cell.",
            "",
            "| Block | Verdict |",
            "| --- | --- |",
        };
        string directory = Path.GetFullPath("../../../../docs/compat", Path.GetDirectoryName(typeof(CompatProbeScenarios).Assembly.Location)!);
        Directory.CreateDirectory(directory);
        File.WriteAllLines($"{directory}/{modid}.md", lines.Concat(rows));
    }

    private async Task<string> Host(ITestPlayer player, BlockPos cell, Block block)
    {
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = null;
        player.Entity.LeftHandItemSlot.Itemstack = new ItemStack(World.Api.World.GetItem(new AssetLocation("game:saw-copper")));
        World.SetBlock("game:planks-aged-ud", cell.DownCopy());
        await WallBuilder.Raise(World, player, cell, cell.WestCopy(), "game:plank-oak", "game:clay-blue", "game:plank-oak");

        player.Entity.LeftHandItemSlot.Itemstack = null;
        player.Player.InventoryManager.ActiveHotbarSlot.Itemstack = new ItemStack(block);
        var selection = new BlockSelection { Position = cell.Copy(), Face = BlockFacing.EAST, HitPosition = new Vec3d(0.5, 0.5, 0.5) };
        World.BlockAt(cell).OnBlockInteractStart(player.Entity.World, player.Player, selection);

        return World.BlockAt(cell).Code.FirstCodePart() == block.Code.FirstCodePart() && WallBuilder.GuestLayers(World, cell) != null
            ? "hosts"
            : "placement failed";
    }
}
