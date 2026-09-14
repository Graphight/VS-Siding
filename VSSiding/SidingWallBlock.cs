using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

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

        if (!claimed) return 0;

        // base.GetRetention is gated on SideSolid, which wall.json sets false on every
        // face (see decision 0002) - so the sign has to be computed directly here instead
        // of delegating, using the same wood-vs-cooling rule vanilla's default applies.
        bool cooling = BlockMaterial is EnumBlockMaterial.Stone or EnumBlockMaterial.Ore
            or EnumBlockMaterial.Soil or EnumBlockMaterial.Ceramic;
        return cooling ? -1 : 1;
    }
}
