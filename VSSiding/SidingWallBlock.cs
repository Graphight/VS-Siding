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

    // base.GetRetention is gated on SideSolid, which wall.json sets false on every face
    // (see decision 0002), so the sign has to be computed directly here. A wall only seals
    // once framing and infill are both built and both keys still exist in their dictionary
    // (see wall-layer-state) - an uninstalled material renders as "not built" rather than
    // crashing or wrongly sealing the room.
    internal static int ComputeRetention(bool claimed, string? framingKey, string? infillKey, JsonObject framings, JsonObject infills)
    {
        if (!claimed || framingKey == null || infillKey == null) return 0;
        if (!framings[framingKey].Exists || !infills[infillKey].Exists) return 0;

        string? materialName = infills[infillKey]["BlockMaterial"].AsString(null!);
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

        var stacks = new List<ItemStack>();
        foreach (var drop in drops)
        {
            drop.Resolve(world, "vssiding:wall drops", Code);
            var stack = drop.GetNextItemStack(dropQuantityMultiplier);
            if (stack != null) stacks.Add(stack);
        }
        return stacks.ToArray();
    }

    // Breaking a wall drops every built part (see wall-layer-state), each part's own Drops
    // entry - an unbuilt or no-longer-valid part (uninstalled material) drops nothing.
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
        if (key == null || !dictionary[key].Exists) return;
        foreach (var dropJson in dictionary[key]["Drops"].AsArray() ?? Array.Empty<JsonObject>())
        {
            drops.Add(ParseDrop(dropJson));
        }
    }

    private static BlockDropItemStack ParseDrop(JsonObject dropJson)
    {
        Enum.TryParse(dropJson["type"].AsString("item"), true, out EnumItemClass itemClass);
        return new BlockDropItemStack
        {
            Type = itemClass,
            Code = new AssetLocation(dropJson["code"].AsString(null!)),
            Quantity = NatFloat.createDirac(dropJson["quantity"].AsFloat(1f), 0),
        };
    }
}
