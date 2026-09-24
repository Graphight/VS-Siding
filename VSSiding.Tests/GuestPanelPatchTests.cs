using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Xunit;

namespace VSSiding.Tests;

public class GuestPanelPatchTests
{
    [Fact]
    public void PatchTargetsStillExist()
    {
        Assert.NotNull(AccessTools.Method(typeof(ChunkTesselator), "TesselateBlock",
            new[] { typeof(Block), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "vars"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "game"));
        Assert.NotNull(AccessTools.Field(typeof(ChunkTesselator), "jsonTesselator"));
        Assert.NotNull(AccessTools.Field(typeof(JsonTesselator), "helper"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "block"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "blockId"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "lx"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "ly"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "lz"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "finalX"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "finalY"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "finalZ"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "posX"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "posY"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "posZ"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "dimension"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "RenderPass"));
        Assert.NotNull(AccessTools.Field(typeof(TCTCache), "VertexFlags"));
    }
}
