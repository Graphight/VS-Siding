using System.Linq;
using System.Threading.Tasks;
using Atlas.Api;
using Atlas.XUnit;
using Vintagestory.API.MathTools;
using Xunit;

namespace VSSiding.E2E.Tests;

[AtlasWorld(StrictBootDiagnostics = true)]
public class BootScenarios : AtlasScenarioBase
{
    [AtlasScenario]
    public async Task Wall_Should_BePlaceable_When_ModIsLoaded()
    {
        BlockPos pos = World.Spawn.Offset(1, 1, 0);
        World.SetBlock("vssiding:wall-wall-north", pos);
        await World.Ticks(5);
        Assert.Equal("vssiding:wall-wall-north", World.BlockAt(pos).Code.ToString());
    }

    // The server sends each item's behaviours to a client by registered name (ItemTypeNet.GetItemTypePacket); an
    // unregistered one goes as null and the client crashes on join, which a headless server never shows.
    [AtlasScenario]
    public Task EveryItemBehavior_Should_HaveARegisteredName_When_ModIsLoaded()
    {
        var unnamed = World.Api.World.Items
            .Where(item => item?.CollectibleBehaviors != null)
            .SelectMany(item => item.CollectibleBehaviors.Select(behavior => (item.Code, Type: behavior.GetType())))
            .Where(pair => World.Api.ClassRegistry.GetCollectibleBehaviorClassName(pair.Type) == null)
            .Select(pair => $"{pair.Code}: {pair.Type.Name}")
            .ToList();
        Assert.Empty(unnamed);
        return Task.CompletedTask;
    }
}
