using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;

namespace VSSiding;

public class SidingModSystem : ModSystem
{
    private ICoreClientAPI? capi;

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;
        GuestWalls.StartClientSide(api);
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.RegisterBlockClass("SidingWallBlock", typeof(SidingWallBlock));
        api.RegisterBlockEntityClass("SidingWallEntity", typeof(SidingWallEntity));
        api.RegisterCollectibleBehaviorClass("vssiding.PlaceWallFrame", typeof(PlaceWallFrame));
        GuestWalls.Start(api);

        // Singleplayer runs client+server in one process, so patch once.
        if (Harmony.HasAnyPatches("vssiding")) return;
        var harmony = new Harmony("vssiding");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(RoomRegistry), "FindRoomForPosition"),
                transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(RoomSkylightTranspiler)));
        }
        catch (Exception e)
        {
            // RoomSkylightPatchTests catches a changed method at build time; players keep the mod, minus the fix.
            api.Logger.Error("vssiding: room skylight patch skipped, sealed walls will count as sky: {0}", e);
        }

        // One fix in two halves - the postfix picks the light, the prefix makes faces use it - so they stand or fall together.
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ChunkTesselator), "BuildExtendedChunkData"),
                postfix: new HarmonyMethod(typeof(SidingModSystem), nameof(SealedCellLightPostfix)));
            harmony.Patch(AccessTools.Method(typeof(TCTCache), "CalcBlockFaceLight"),
                prefix: new HarmonyMethod(typeof(SidingModSystem), nameof(SealedCellFaceLightPrefix)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: sealed cell light patch skipped, sealed rooms will glow at the wall base: {0}", e);
        }

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BlockAccessorBase), nameof(BlockAccessorBase.GetDistanceToRainFall)),
                prefix: new HarmonyMethod(typeof(SidingModSystem), nameof(RainFallFromOpenSidePrefix)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: rain fall distance patch skipped, wind will sound outdoors inside a wall's dead space: {0}", e);
        }

        try
        {
            harmony.Patch(AccessTools.Method(typeof(ChunkTesselator), "TesselateBlock",
                    new[] { typeof(Block), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }),
                transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(GapShiftTranspiler)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: gap shift patch skipped, furniture against a wall's open side will stand three-quarters clear: {0}", e);
        }

        try
        {
            GapShiftCollisionPatches.PatchAll(harmony, api);
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: gap shift collision patches skipped entirely, snapped furniture will not collide or select where it renders: {0}", e);
        }

        // Renderers that draw a snapped block from a position of their own, outside chunk
        // tesselation, so GapShiftAt's shift has to be reapplied to each one by hand.
        try
        {
            harmony.Patch(AccessTools.Constructor(typeof(AnimatableRenderer),
                    new[] { typeof(ICoreClientAPI), typeof(Vec3d), typeof(Vec3f), typeof(AnimatorBase),
                        typeof(Dictionary<string, AnimationMetaData>), typeof(MeshData), typeof(EnumRenderStage) }),
                prefix: new HarmonyMethod(typeof(SidingModSystem), nameof(AnimatableRendererPrefix)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: chest/trunk lid animation shift patch skipped, the lid will animate three-quarters clear of a snapped chest: {0}", e);
        }

        // Each of these builds its model matrix as ModelMat.Identity().Translate(pos - camera, ...),
        // with pos a whole-block BlockPos, so the fraction goes into the matrix instead.
        foreach (var renderer in new[] { typeof(FirepitContentsRenderer), typeof(PotInFirepitRenderer), typeof(BlockEntitySignRenderer) })
        {
            try
            {
                harmony.Patch(AccessTools.Method(renderer, "OnRenderFrame"),
                    transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(RendererMatrixTranspiler)));
            }
            catch (Exception e)
            {
                api.Logger.Error("vssiding: {0} shift patch skipped, it will draw three-quarters clear of a snapped block: {1}", renderer.Name, e);
            }
        }

        try
        {
            harmony.Patch(AccessTools.Method(typeof(Block), nameof(Block.OnAsyncClientParticleTick)),
                transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(ParticleSpawnTranspiler)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: block particle shift patch skipped, a snapped torch's flame will burn three-quarters clear of it: {0}", e);
        }

        try
        {
            harmony.Patch(AccessTools.Method(typeof(SystemRenderDecals), "UpdateDecal"),
                transpiler: new HarmonyMethod(typeof(SidingModSystem), nameof(DecalTesselationTranspiler)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: mining crack decal shift patch skipped, the crack overlay will show three-quarters clear of a snapped block: {0}", e);
        }

        // ExchangeBlock (firepit lit/unlit, torch burnout) never calls BreakAllDecorFast, so those
        // keep their guest with no code here (furniture-against-thin-walls, decision 0035 pending).
        try
        {
            harmony.Patch(AccessTools.Method(typeof(WorldChunk), nameof(WorldChunk.BreakAllDecorFast)),
                prefix: new HarmonyMethod(typeof(SidingModSystem), nameof(HostChangePrefix)));
        }
        catch (Exception e)
        {
            api.Logger.Error("vssiding: host change patch skipped, breaking hosted furniture will not restore or drop its guest wall: {0}", e);
        }
    }

    // The renderer keeps the Vec3d its caller passed in for its lifetime, so it gets an offset copy
    // rather than a shift in place. The shift is fixed at construction; a wall appearing or
    // disappearing mid-animation does not move it, and a chest lid swings in under a second.
    internal static void AnimatableRendererPrefix(ICoreClientAPI capi, ref Vec3d pos)
    {
        var blockPos = pos.AsBlockPos;
        var (dx, dz) = GapShiftAt(capi.World.BlockAccessor, blockPos, capi.World.BlockAccessor.GetBlock(blockPos));
        if (dx == 0 && dz == 0) return;
        pos = pos.AddCopy((float)dx, 0, (float)dz);
    }

    // Inserted after every Matrixf.Identity() in the renderer's OnRenderFrame. Translations
    // commute, so shifting first moves everything the renderer then draws at pos.
    internal static Matrixf ShiftMatrix(Matrixf matrix, ICoreClientAPI api, BlockPos pos)
    {
        var (dx, dz) = GapShiftAt(api.World.BlockAccessor, pos, api.World.BlockAccessor.GetBlock(pos));
        return dx == 0 && dz == 0 ? matrix : matrix.Translate(dx, 0, dz);
    }

    internal static IEnumerable<CodeInstruction> RendererMatrixTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var renderer = original.DeclaringType!;
        var apiField = AccessTools.Field(renderer, "api") ?? AccessTools.Field(renderer, "capi");
        var posField = AccessTools.Field(renderer, "pos");
        var identity = AccessTools.Method(typeof(Matrixf), nameof(Matrixf.Identity));
        var shift = AccessTools.Method(typeof(SidingModSystem), nameof(ShiftMatrix));

        int inserted = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(identity)) continue;
            inserted++;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, apiField);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, posField);
            yield return new CodeInstruction(OpCodes.Call, shift);
        }

        if (inserted == 0)
            throw new InvalidOperationException($"{renderer.Name}.OnRenderFrame no longer calls Matrixf.Identity().");
    }

    // Block.OnAsyncClientParticleTick rewrites basePos from pos on every tick right before
    // spawning, so nudging it here holds for that one spawn and never accumulates.
    internal static int SpawnShifted(IAsyncParticleManager manager, IParticlePropertiesProvider particles, Block block, BlockPos pos)
    {
        if (particles is AdvancedParticleProperties advanced)
        {
            var (dx, dz) = GapShiftAt(manager.BlockAccess, pos, block);
            advanced.basePos.X += dx;
            advanced.basePos.Z += dz;
        }
        return manager.Spawn(particles);
    }

    internal static IEnumerable<CodeInstruction> ParticleSpawnTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var spawn = AccessTools.Method(typeof(IAsyncParticleManager), nameof(IAsyncParticleManager.Spawn));
        var spawnShifted = AccessTools.Method(typeof(SidingModSystem), nameof(SpawnShifted));

        int replaced = 0;
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(spawn))
            {
                yield return instruction;
                continue;
            }
            replaced++;
            yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
            yield return new CodeInstruction(OpCodes.Ldarg_2);
            yield return new CodeInstruction(OpCodes.Call, spawnShifted);
        }

        if (replaced != 1)
            throw new InvalidOperationException($"Expected exactly one IAsyncParticleManager.Spawn call in Block.OnAsyncClientParticleTick, found {replaced}.");
    }

    // UpdateDecal calls this with the mesh still in block-local coordinates, then translates it by
    // decal.pos itself a few lines later. Shifting the mesh here, before that translation, lands the
    // crack overlay on the panel the same way the block itself was shifted at tesselation time.
    internal static void DecalTesselationShifted(Block block, IWorldAccessor world, MeshData decalMesh, BlockPos pos)
    {
        block.OnDecalTesselation(world, decalMesh, pos);
        var (dx, dz) = GapShiftAt(world.BlockAccessor, pos, block);
        if (dx == 0 && dz == 0) return;
        decalMesh.Translate((float)dx, 0, (float)dz);
    }

    internal static IEnumerable<CodeInstruction> DecalTesselationTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var onDecalTesselation = AccessTools.Method(typeof(Block), nameof(Block.OnDecalTesselation));
        var shifted = AccessTools.Method(typeof(SidingModSystem), nameof(DecalTesselationShifted));

        int replaced = 0;
        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(onDecalTesselation))
            {
                yield return instruction;
                continue;
            }
            replaced++;
            yield return new CodeInstruction(OpCodes.Call, shifted).MoveLabelsFrom(instruction);
        }

        if (replaced != 1)
            throw new InvalidOperationException($"Expected exactly one Block.OnDecalTesselation call in SystemRenderDecals.UpdateDecal, found {replaced}.");
    }

    // The search to open sky checks only the block it steps into, never the one it leaves, since
    // nobody stands inside a solid block. A siding wall's dead space is walkable, so from there the
    // first step goes out through the panel and the wind plays at full volume. Start from the cell
    // the dead space opens onto instead (decision 0034).
    internal static void RainFallFromOpenSidePrefix(IBlockAccessor __instance, ref BlockPos pos)
    {
        if (__instance.GetBlock(pos) is not SidingWallBlock wall) return;
        if (wall.GetRetention(pos, BlockFacing.FromCode(wall.Variant["side"]), EnumRetentionType.Sound) == 0) return;
        var (dx, dz) = SidingWallBlock.OpenSide(wall.Variant["layout"], wall.Variant["side"]);
        pos = pos.AddCopy(dx, 0, dz);
    }

    public override void Dispose()
    {
        new Harmony("vssiding").UnpatchAll("vssiding");
        // PlaceWallFrame does have CollectibleBehavior.OnUnloaded, but it is patched onto every
        // plank variant, so ~14 behavior instances share the one cached array and would each
        // dispose it. ClientMain.Dispose runs the mod systems before its item loop, so freeing
        // the icons here does it once, first.
        if (capi?.ObjectCache.TryGetValue("vssidingPlaceWallFrameToolModes", out var cached) == true)
        {
            foreach (var item in (SkillItem[])cached) item.Dispose();
            capi.ObjectCache.Remove("vssidingPlaceWallFrameToolModes");
        }
        base.Dispose();
    }

    // Swaps RoomRegistry's skylight sample for SidingWallBlock.RoomSunlight so sealed wall cells read dark (decision 0015).
    internal static IEnumerable<CodeInstruction> RoomSkylightTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var getLightLevel = AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetLightLevel),
            new[] { typeof(BlockPos), typeof(EnumLightLevelType) });
        var roomSunlight = AccessTools.Method(typeof(SidingWallBlock), nameof(SidingWallBlock.RoomSunlight));

        int replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(getLightLevel))
            {
                replaced++;
                yield return new CodeInstruction(OpCodes.Call, roomSunlight);
            }
            else
            {
                yield return instruction;
            }
        }

        if (replaced != 1)
            throw new InvalidOperationException($"Expected exactly one IBlockAccessor.GetLightLevel call to replace, found {replaced}.");
    }

    // Both halves of the fix run on the tessellation thread, one chunk at a time, so what a
    // chunk's postfix records is what its own face-light calls read back.
    [ThreadStatic] private static bool[]? sealedCells;
    [ThreadStatic] private static int[]? sealedCellRgbs;

    // A sealed wall's cell stores the sunlight flowing in from outside (decision 0015), and the
    // floor face under it samples that cell. Show the light of the cell the wall's dead space
    // opens onto instead - the room, or the outdoors if the panels face in.
    internal static void SealedCellLightPostfix(ClientMain ___game, Block[] ___currentChunkBlocksExt, int[] ___currentChunkRgbsExt,
        int chunkX, int chunkY, int chunkZ)
    {
        const int size = 34;
        var mask = sealedCells ??= new bool[___currentChunkBlocksExt.Length];
        Array.Clear(mask);
        sealedCellRgbs = ___currentChunkRgbsExt;

        var pos = new BlockPos(chunkY / 1024);
        for (int i = 0; i < ___currentChunkBlocksExt.Length; i++)
        {
            if (___currentChunkBlocksExt[i] is not SidingWallBlock wall) continue;

            int x = i % size, z = i / size % size, y = i / (size * size);
            var (dx, dz) = SidingWallBlock.OpenSide(wall.Variant["layout"], wall.Variant["side"]);
            // A border cell whose open side leaves the array opens away from this chunk, so its
            // dead space - and the floor face that shows it - belongs to the neighbour, which
            // has the same cell in its own interior and darkens it there.
            if (x + dx is < 0 or >= size || z + dz is < 0 or >= size) continue;

            // chunkY carries the dimension above the world's 32768 blocks (vanilla: dim = chunkY / 1024).
            pos.Set(chunkX * 32 + x - 1, chunkY * 32 % 32768 + y - 1, chunkZ * 32 + z - 1);
            if (!wall.IsSealed(___game.BlockAccessor.GetBlockEntity(pos))) continue;

            ___currentChunkRgbsExt[i] = ___currentChunkRgbsExt[i + dx + dz * size];
            mask[i] = true;
        }
    }

    // Smooth lighting averages a face's own sample with the cells ringing it, and for the floor
    // face under a thin wall one of those is the sunlit cell just outside - which is the daylight
    // that lit a sealed room's floor edges. Faces onto a sealed cell take the flat path instead,
    // dimmed by the occlusion vanilla gives a face onto a side-AO cell, or the dead space reads
    // brighter than the AO-shaded floor beside it (decision 0034). Where vanilla already takes its
    // own flat path it reads the rewritten light unaided, so those faces are left to it.
    internal static bool SealedCellFaceLightPrefix(TCTCache __instance, int tileSide, int extNeibIndex3d, ref long __result)
    {
        if (sealedCells is not { } mask || !mask[extNeibIndex3d]) return true;
        if (!__instance.aoAndSmoothShadows || !__instance.block.SideAo[tileSide]) return true;

        int light = Occlude(sealedCellRgbs![extNeibIndex3d], __instance.occ);
        var corners = __instance.CurrentLightRGBByCorner;
        corners[0] = corners[1] = corners[2] = corners[3] = light;
        __result = light * 4;   // int arithmetic, as vanilla's flat path returns it
        return false;
    }

    // Scales all four packed bytes, sunlight included, as TCTCache.CornerAoRGB does.
    internal static int Occlude(int rgb, float factor)
        => ((int)(((rgb >> 24) & 0xFF) * factor) << 24) | ((int)(((rgb >> 16) & 0xFF) * factor) << 16)
            | ((int)(((rgb >> 8) & 0xFF) * factor) << 8) | (int)((rgb & 0xFF) * factor);

    // Indexed by BlockId: whether the block can be hosted on a wall's panel (furniture-against-thin-walls,
    // decision 0035 pending). Built once after blocks load so terrain tesselation costs one array
    // read per block. The offset toolkit below (ShiftTowardWall, GapShiftAt) reads this table but
    // shifts nothing yet - that comes with stage 6, once there's a hosted block to shift.
    internal static bool[]? Hostable;

    // Most blocks are hostable (decision 0035). Excluded: our own walls, anything that already culls
    // a neighbour (SideSolid), fluid-layer blocks, anything not a plain JSON shape (cubes, crosses,
    // liquids, microblocks all draw or collide in ways this offset was never checked against), beds
    // (a "part" variant) and multiblock fillers, doors (1.22's are BlockGeneric with a "Door" BE
    // behaviour, so the class check alone misses them) and mechanical power blocks (BlockMPBase
    // networks by position).
    private static void BuildHostableTable(ICoreAPI api)
    {
        int maxId = api.World.Blocks.Where(b => b != null).Max(b => b.BlockId);
        var hostable = new bool[maxId + 1];

        foreach (var block in api.World.Blocks)
        {
            if (block == null) continue;
            hostable[block.BlockId] = IsHostable(block);
        }

        Hostable = hostable;
    }

    private static bool IsHostable(Block block)
    {
        if (block is SidingWallBlock) return false;
        if (block.SideSolid.Any) return false;
        if (block.ForFluidsLayer) return false;
        if (block.DrawType is not (EnumDrawType.JSON or EnumDrawType.JSONAndSnowLayer or EnumDrawType.JSONAndWater)) return false;
        if (block is BlockMicroBlock) return false;
        if (block.Variant.ContainsKey("part")) return false;
        if (block is BlockBaseDoor || block.BlockEntityBehaviors.Any(b => b.Name == "Door")) return false;
        if (block is BlockMultiblock) return false;
        if (block is BlockMPBase) return false;
        return true;
    }

    // Transpiled onto ChunkTesselator.TesselateBlock right after it stores vars.finalZ (see
    // GapShiftTranspiler), so both plain JSON meshes and block-entity OnTesselation meshes -
    // everything reading vars.finalX/finalZ downstream - pick up the shift once stage 6 gives one.
    internal static void ShiftTowardWall(ChunkTesselator tesselator, Block block)
    {
        if (Hostable is not { } hostable || block.BlockId >= hostable.Length || !hostable[block.BlockId]) return;
    }

    // What BreakAllDecorFast's prefix does with a guest once it sees the new block written into the
    // chunk (furniture-against-thin-walls, decision 0035 pending). Kept as a pure function of the
    // three inputs so HostChangeTests can exercise it with plain Block instances.
    internal enum HostChange { Restore, Keep, Drop }

    internal static HostChange ClassifyHostChange(Block newBlock, Block guestWallBlock, bool[]? hostable)
    {
        if (newBlock.BlockId == 0 || newBlock.IsReplacableBy(guestWallBlock)) return HostChange.Restore;
        if (hostable != null && newBlock.BlockId < hostable.Length && hostable[newBlock.BlockId]) return HostChange.Keep;
        return HostChange.Drop;
    }

    // BreakAllDecorFast runs on every solid-block SetBlock (BlockAccessorBase.SetSolidBlockInternal,
    // plus the bulk and movable accessors), with the new id already written into the chunk and the
    // old block's OnBlockRemoved still to come - the one place that sees every way a hosted cell can
    // change, without having to patch every tool that can break or place over one.
    internal static void HostChangePrefix(WorldChunk __instance, IWorldAccessor world, BlockPos pos)
    {
        if (world.Side != EnumAppSide.Server) return;
        var guest = GuestWalls.GuestAt(world.Api, __instance, pos);
        if (guest == null) return;

        Block newBlock = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid);
        switch (ClassifyHostChange(newBlock, guest.Block, Hostable))
        {
            case HostChange.Restore:
                // Deferred so the old host's OnBlockRemoved (a chest dropping its contents) finishes
                // first - it runs right after this prefix returns, and would otherwise fire against
                // the wall instead of the block it actually broke.
                world.RegisterCallback(_ => RestoreGuestWall(world, pos), 0);
                break;
            case HostChange.Drop:
                if (guest.Block is SidingWallBlock guestWall)
                {
                    var drops = SidingWallBlock.ComputeDrops(
                        guest.Framing, guest.Infill, guest.Front, guest.SecondFront, guest.Back,
                        guestWall.Attributes["Framings"], guestWall.Attributes["Infills"], guestWall.Attributes["Finishes"]);
                    foreach (var stack in guestWall.ResolveDrops(world, drops, 1f)) world.SpawnItemEntity(stack, pos);
                }
                GuestWalls.Set(__instance, pos, null);
                break;
            case HostChange.Keep:
                break;
        }
    }

    // Re-checks the cell and the guest before touching either - the cell could have taken another
    // hostable block in the meantime, and the deferred callback could outlive the guest itself.
    // Clears the guest before SetBlock: with the guest still recorded under a placed, non-hostable
    // wall, this same prefix would see the freshly-placed wall's own SetBlock next and drop the
    // layers it hasn't restored yet.
    private static void RestoreGuestWall(IWorldAccessor world, BlockPos pos)
    {
        IWorldChunk? chunk = world.BlockAccessor.GetChunkAtBlockPos(pos);
        if (chunk == null) return;
        var guest = GuestWalls.GuestAt(world.Api, chunk, pos);
        if (guest == null) return;
        Block current = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Solid);
        if (ClassifyHostChange(current, guest.Block, Hostable) != HostChange.Restore) return;

        GuestWalls.Set(chunk, pos, null);
        world.BlockAccessor.SetBlock(guest.Block.BlockId, pos);

        // The fresh entity starts with no infill, so relight and redraw it exactly as a saw would
        // when laying infill onto a bare frame (OnInfillChanged), instead of duplicating that here.
        if (guest.Block is SidingWallBlock wallBlock
            && world.BlockAccessor.GetBlockEntity<SidingWallEntity>(pos) is { } entity)
        {
            var tree = new TreeAttribute();
            guest.ToTreeAttributes(tree);
            entity.FromTreeAttributes(tree, world);
            wallBlock.OnInfillChanged(world, entity, pos, null);
        }
    }

    // The same query as ShiftTowardWall, off an IBlockAccessor instead of the tesselator's extended
    // chunk cache, for GapShiftCollisionPatches (collision/selection run every physics tick and off
    // the render thread, so they can't reach into ChunkTesselator's per-frame state). Returns zero
    // until stage 6 retargets it onto a hosted block's panel offset.
    internal static (double dx, double dz) GapShiftAt(IBlockAccessor blockAccessor, BlockPos pos, Block block)
    {
        if (Hostable is not { } hostable || block.BlockId >= hostable.Length || !hostable[block.BlockId]) return (0, 0);
        return (0, 0);
    }

    // Inserts the ShiftTowardWall call right after vars.finalZ = vars.lz is set (the int lz widens
    // to float via conv.r4 first) - the point Place On Slabs's own TesselateBlock transpiler anchors
    // on too, for the equivalent vertical offset - so a game update that moves it fails the build
    // via GapShiftPatchTests rather than silently no-opping. RandomDrawOffset stores to finalZ too,
    // further down, so the anchor also checks the value being stored came from vars.lz, not just
    // any store to the field.
    internal static IEnumerable<CodeInstruction> GapShiftTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var lzField = AccessTools.Field(typeof(TCTCache), nameof(TCTCache.lz));
        var finalZField = AccessTools.Field(typeof(TCTCache), nameof(TCTCache.finalZ));
        var shiftMethod = AccessTools.Method(typeof(SidingModSystem), nameof(ShiftTowardWall));

        var list = instructions.ToList();
        int inserted = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (i < 2 || !list[i].StoresField(finalZField)
                || list[i - 1].opcode != OpCodes.Conv_R4 || !list[i - 2].LoadsField(lzField)) continue;
            inserted++;
            list.InsertRange(i + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, shiftMethod),
            });
            break;
        }

        if (inserted != 1)
            throw new InvalidOperationException($"Expected exactly one vars.finalZ = vars.lz store to follow, found {inserted}.");

        return list;
    }

    // The server's CurrentBlockSelection is its own raytrace; the break packet's face only reaches this event.
    public override void StartServerSide(ICoreServerAPI api)
    {
        GuestWalls.StartServerSide(api);

        api.Event.BreakBlock += (IServerPlayer _, BlockSelection blockSel, ref float _, ref EnumHandling _)
            => SidingWallBlock.ServerBreakSelection = blockSel;

        api.ChatCommands.Create("sidingroom")
            .WithDescription("Prints the room counts and light level at the player's feet")
            .RequiresPrivilege(Privilege.controlserver)
            .RequiresPlayer()
            .HandleWith(args =>
            {
                var pos = args.Caller.Player.Entity.Pos.AsBlockPos;
                var room = api.ModLoader.GetModSystem<RoomRegistry>().GetRoomForPosition(pos);
                var light = api.World.BlockAccessor.GetLightLevel(pos, EnumLightLevelType.OnlySunLight);
                return TextCommandResult.Success(
                    $"cooling {room.CoolingWallCount}, warm {room.NonCoolingWallCount}, " +
                    $"sky {room.SkylightCount}/{room.SkylightCount + room.NonSkylightCount}, " +
                    $"exits {room.ExitCount}, small {room.IsSmallRoom}, light {light} (sun {api.World.SunBrightness})");
            });
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        BuildHostableTable(api);

        // Server side only from here: the client receives the expanded block attributes with the block list (decision 0010).
        if (api.Side != EnumAppSide.Server) return;

        var candidates = api.World.Items.Where(i => i?.Code != null)
            .Select(i => ("item", i.Code, (IDictionary<string, string>)i.Variant))
            .Concat(api.World.Blocks.Where(b => b?.Code != null)
                .Select(b => ("block", b.Code, (IDictionary<string, string>)b.Variant)))
            .ToList();

        foreach (var block in api.World.Blocks.OfType<SidingWallBlock>())
        {
            var attributes = (JObject)block.Attributes.Token.DeepClone();
            foreach (var (familiesKey, materialsKey) in new[] { ("FramingFamilies", "Framings"), ("InfillFamilies", "Infills"), ("FinishFamilies", "Finishes") })
            {
                if (attributes[familiesKey] is not JObject families) continue;
                attributes[materialsKey] = MaterialFamilies.Expand(families, attributes[materialsKey] as JObject ?? new JObject(), candidates,
                    message => api.Logger.Warning("{0}: {1}", block.Code, message));
                attributes.Remove(familiesKey);
            }
            block.Attributes = new JsonObject(attributes);
        }
    }
}
