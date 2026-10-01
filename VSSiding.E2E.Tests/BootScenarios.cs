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
}
