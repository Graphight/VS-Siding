using System;
using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.Tests;

public class HotPathAllocationTests
{
    private const int Calls = 10_000;

    // Stands for every block that hosts nothing: its overrides carry the guest wall's real patches.
    private class PlainBlock : Block
    {
        internal static readonly Cuboidf[] Boxes = { new(0, 0, 0, 1, 1, 1) };

        public override int GetLightAbsorption(IBlockAccessor blockAccessor, BlockPos pos) => 0;
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos) => Boxes;
        public override int GetRetention(BlockPos pos, BlockFacing facing, EnumRetentionType type) => 0;
    }

    private static long BytesPerCall(Action call)
    {
        call();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Calls; i++) call();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / Calls;
    }

    private static HarmonyMethod Patch(Type patches, string name) => new(AccessTools.Method(patches, name));

    // Vanilla asks these per light cell, per physics tick and per room cell, so each answer builds nothing.
    [Fact]
    public void TheGuestPatchesAndAWallsOwnAnswersAllocateNothing()
    {
        var harmony = new Harmony("vssiding.tests.allocations");
        harmony.Patch(AccessTools.Method(typeof(PlainBlock), nameof(Block.GetLightAbsorption), new[] { typeof(IBlockAccessor), typeof(BlockPos) }),
            postfix: Patch(typeof(GuestLightPatches), "AbsorptionAccessorPostfix"));
        harmony.Patch(AccessTools.Method(typeof(PlainBlock), nameof(Block.GetCollisionBoxes)),
            Patch(typeof(GapShiftCollisionPatches), "Prefix"), Patch(typeof(GapShiftCollisionPatches), "PostfixCollision"), finalizer: Patch(typeof(GapShiftCollisionPatches), "Finalizer"));
        harmony.Patch(AccessTools.Method(typeof(PlainBlock), nameof(Block.GetRetention)),
            Patch(typeof(GuestSealingPatches), "Prefix"), Patch(typeof(GuestSealingPatches), "RetentionPostfix"), finalizer: Patch(typeof(GuestSealingPatches), "Finalizer"));

        Block plain = new PlainBlock { BlockId = int.MaxValue };
        var pos = new BlockPos(5, 60, 9, 0);
        var wall = new SidingWallBlock { Attributes = new JsonObject(JToken.Parse("""{ "Framings": { "oak": {} }, "Infills": { "clay": { "BlockMaterial": "Soil" } } }""")) };
        wall.VariantStrict["layout"] = "wall";
        wall.VariantStrict["side"] = "north";
        var entity = new SidingWallEntity { Framing = "oak", Infill = "clay" };

        var actual = new Dictionary<string, long>
        {
            ["light absorption of a plain block"] = BytesPerCall(() => plain.GetLightAbsorption((IBlockAccessor)null!, pos)),
            ["collision boxes of a plain block"] = BytesPerCall(() => plain.GetCollisionBoxes(null!, pos)),
            ["retention of a plain block"] = BytesPerCall(() => plain.GetRetention(pos, BlockFacing.NORTH, EnumRetentionType.Heat)),
            ["a wall's retention"] = BytesPerCall(() => wall.RetentionOf(entity, BlockFacing.NORTH)),
            ["a wall's light absorption"] = BytesPerCall(() => wall.AbsorptionOf(entity)),
            ["a wall's attachability"] = BytesPerCall(() => wall.CanAttachOf(entity, BlockFacing.NORTH)),
            ["a decked wall's boxes"] = BytesPerCall(() => SidingWallBlock.AddOpenPartBoxes(PlainBlock.Boxes, "wall", "west", "oak", null)),
        };
        harmony.UnpatchAll("vssiding.tests.allocations");

        Assert.Equal(new Dictionary<string, long>
        {
            ["light absorption of a plain block"] = 0,
            ["collision boxes of a plain block"] = 0,
            ["retention of a plain block"] = 0,
            ["a wall's retention"] = 0,
            ["a wall's light absorption"] = 0,
            ["a wall's attachability"] = 0,
            ["a decked wall's boxes"] = 0,
        }, actual);
    }
}
