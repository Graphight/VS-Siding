using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

[assembly: InternalsVisibleTo("VSSiding.Tests")]

namespace VSSiding;

public class SidingWallBlock : Block
{
    // cornerout's L covers `side` plus the face counter-clockwise from it:
    // west+north, south+west, east+south, north+east.
    private static readonly Dictionary<string, string> CorneroutSecondFace = new()
    {
        ["west"] = "north",
        ["south"] = "west",
        ["east"] = "south",
        ["north"] = "east",
    };

    public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type)
    {
        string side = Variant["side"];
        string layout = Variant["layout"];

        bool claimed = facing.Code == side;
        if (!claimed && layout == "cornerout")
        {
            claimed = facing.Code == CorneroutSecondFace[side];
        }

        var entity = api.World.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        return ComputeRetention(claimed, entity?.Framing, entity?.Infill, Attributes["Framings"], Attributes["Infills"]);
    }

    // sidesolid is false on every face (decision 0002), so base.GetRetention can't be
    // delegated to. A wall seals only once framing and infill are both built and still
    // valid in their dictionary; an uninstalled material counts as not built.
    internal static int ComputeRetention(bool claimed, string? framingKey, string? infillKey, JsonObject framings, JsonObject infills)
    {
        if (!claimed || framingKey == null || infillKey == null) return 0;

        var infill = infills[infillKey];
        if (!framings[framingKey].Exists || !infill.Exists) return 0;

        string? materialName = infill["BlockMaterial"].AsString(null!);
        bool cooling = Enum.TryParse(materialName, true, out EnumBlockMaterial material)
            && material is EnumBlockMaterial.Stone or EnumBlockMaterial.Ore
                or EnumBlockMaterial.Soil or EnumBlockMaterial.Ceramic;
        return cooling ? -1 : 1;
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier)
    {
        var entity = world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos);
        var drops = ComputeDrops(
            entity?.Framing, entity?.Infill, entity?.Front, entity?.Back,
            Attributes["Framings"], Attributes["Infills"], Attributes["Finishes"]);

        // Nothing built yet - fall back to the base drops so HorizontalOrientable still
        // hands back the placed block.
        if (drops.Count == 0) return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);

        var stacks = new List<ItemStack>();
        foreach (var drop in drops)
        {
            drop.Resolve(world, "vssiding:wall drops", Code);
            var stack = drop.GetNextItemStack(dropQuantityMultiplier);
            if (stack != null) stacks.Add(stack);
        }
        return stacks.ToArray();
    }

    internal static List<BlockDropItemStack> ComputeDrops(
        string? framing, string? infill, string? front, string? back,
        JsonObject framings, JsonObject infills, JsonObject finishes)
    {
        var drops = new List<BlockDropItemStack>();
        AddDrops(drops, framing, framings);
        AddDrops(drops, infill, infills);
        AddDrops(drops, front, finishes);
        AddDrops(drops, back, finishes);
        return drops;
    }

    private static void AddDrops(List<BlockDropItemStack> drops, string? key, JsonObject dictionary)
    {
        if (key == null) return;
        var entry = dictionary[key];
        if (!entry.Exists) return;

        foreach (var drop in entry["Drops"].AsObject(Array.Empty<BlockDropItemStack>()))
        {
            if (drop.Code != null) drops.Add(drop);
        }
    }
}
