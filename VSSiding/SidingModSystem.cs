using Vintagestory.API.Common;

namespace VSSiding;

public class SidingModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.RegisterBlockClass("SidingWallBlock", typeof(SidingWallBlock));
        api.RegisterBlockEntityClass("SidingWallEntity", typeof(SidingWallEntity));
        api.RegisterCollectibleBehaviorClass("vssiding.PlaceWallFrame", typeof(PlaceWallFrame));
    }
}
