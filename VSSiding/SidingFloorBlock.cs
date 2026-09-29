using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VSSiding;

// A thin floor: the wall's layers laid flat at the top of the cell (proposal thin-floor-framing).
// Deliberately not a SidingWallBlock, so none of the wall's patches ever see one.
public class SidingFloorBlock : Block
{
    // The unrotated floor's joists run along x, and RotationYDeg turns its west end to face `side`,
    // so the rim drawn as framing-bottom faces `side` and framing-top faces the opposite way. Each
    // drops where the next floor carries the joists on, the way a stacked wall drops its plates.
    internal (bool above, bool below, bool left, bool right) Joins(IBlockAccessor accessor, BlockPos pos)
    {
        var side = BlockFacing.FromCode(Variant["side"]);
        return (ContinuesJoists(accessor, pos.AddCopy(side.Opposite)), ContinuesJoists(accessor, pos.AddCopy(side)), false, false);
    }

    private bool ContinuesJoists(IBlockAccessor accessor, BlockPos neighbourPos)
        => accessor.GetBlock(neighbourPos) is SidingFloorBlock neighbour
            && JoistsAlign(Variant["side"], neighbour.Variant["side"])
            && accessor.GetBlockEntity<SidingFloorEntity>(neighbourPos)?.Framing != null;

    internal static bool JoistsAlign(string side, string neighbourSide)
        => BlockFacing.FromCode(side).Axis == BlockFacing.FromCode(neighbourSide).Axis;
}
