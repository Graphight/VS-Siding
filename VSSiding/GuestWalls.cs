using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VSSiding;

// A wall's state once furniture takes its cell (decision 0035). Rides chunk mod data as vanilla's
// support beams do: loaded lazily from ModData, and flushed back by ServerChunk before both the save
// and the client chunk packet, so only live edits need the network channel below.
public static class GuestWalls
{
    private const string ChannelName = "vssiding-guests";
    private const string ModDataKey = "vssiding:guests";

    // The wall's block code, not its id - ids are assigned per session and don't survive a
    // reload with different mods installed, let alone reaching the client as a saved value.
    [ProtoContract]
    public class GuestRecord
    {
        [ProtoMember(1)] public string BlockCode = "";
        [ProtoMember(2)] public byte[] EntityData = Array.Empty<byte>();
    }

    [ProtoContract]
    private class GuestSyncPacket
    {
        [ProtoMember(1)] public BlockPos Pos = new(0);
        // Null on a remove.
        [ProtoMember(2)] public GuestRecord? Record;
    }

    private static IServerNetworkChannel? serverChannel;

    public static void Start(ICoreAPI api)
    {
        api.Network.RegisterChannel(ChannelName).RegisterMessageType<GuestSyncPacket>();
    }

    public static void StartServerSide(ICoreServerAPI sapi)
    {
        serverChannel = sapi.Network.GetChannel(ChannelName);
    }

    public static void StartClientSide(ICoreClientAPI capi)
    {
        capi.Network.GetChannel(ChannelName).SetMessageHandler<GuestSyncPacket>(packet => OnClientSync(capi, packet));
    }

    private static void OnClientSync(ICoreClientAPI capi, GuestSyncPacket packet)
    {
        IWorldChunk? chunk = capi.World.BlockAccessor.GetChunkAtBlockPos(packet.Pos);
        if (chunk == null) return;

        // A client may get the host block's own SetBlock before this packet, relight without the
        // guest, and never catch up otherwise - MarkAbsorptionChanged skips a no-op old == new
        // (decision 0034), so the relight has to happen here too, not just on the server.
        int oldAbsorption = Absorption(GuestAt(capi, chunk, packet.Pos));
        Replace(chunk, packet.Pos, packet.Record);
        int newAbsorption = Absorption(GuestAt(capi, chunk, packet.Pos));
        if (oldAbsorption != newAbsorption) capi.World.BlockAccessor.MarkAbsorptionChanged(oldAbsorption, newAbsorption, packet.Pos);

        capi.World.BlockAccessor.MarkBlockDirty(packet.Pos);
    }

    // A guest's own light absorption; 0 for no guest.
    internal static int Absorption(SidingWallEntity? guest)
        => guest?.Block is SidingWallBlock wall
            ? SidingWallBlock.ComputeLightAbsorption(guest.Framing, guest.Infill, wall.Attributes["Framings"], wall.Attributes["Infills"])
            : 0;

    // Decoded once per stored blob, not once per lookup - the blob rarely changes and this runs on
    // the lighting, tesselation and physics threads.
    private static readonly ConditionalWeakTable<byte[], Dictionary<int, GuestRecord>> decodedBlobs = new();

    // Read-only: singleplayer runs the lighting, tesselation and physics threads alongside
    // ServerChunk's own save and send passes, which iterate LiveModData directly, so a reader here
    // must never insert into it - only Replace (a main-thread writer) does that.
    private static Dictionary<int, GuestRecord>? Guests(IWorldChunk chunk)
    {
        if (chunk.LiveModData.TryGetValue(ModDataKey, out object? cached))
            return (Dictionary<int, GuestRecord>?)cached;

        // Most chunks have never hosted a guest, so this is a dictionary miss, not a deserialize.
        byte[]? raw = chunk.GetModdata(ModDataKey);
        return raw == null ? null : decodedBlobs.GetValue(raw, SerializerUtil.Deserialize<Dictionary<int, GuestRecord>>);
    }

    private static int LocalIndex3d(BlockPos pos)
    {
        int size = GlobalConstants.ChunkSize;
        return MapUtil.Index3d(GameMath.Mod(pos.X, size), GameMath.Mod(pos.Y, size), GameMath.Mod(pos.Z, size), size, size);
    }

    // Singleplayer runs both sides in one process, each with its own Block/SidingWallBlock
    // instances (and its own world), so a decode for one side must never be handed to the other -
    // hence one table per side rather than one keyed on the record alone. A write replaces the
    // record (and the dictionary that holds it), so a stale decode can never be read back.
    private static readonly ConditionalWeakTable<GuestRecord, SidingWallEntity> decodedServer = new();
    private static readonly ConditionalWeakTable<GuestRecord, SidingWallEntity> decodedClient = new();
    private static ConditionalWeakTable<GuestRecord, SidingWallEntity> DecodedFor(ICoreAPI api)
        => api.Side == EnumAppSide.Server ? decodedServer : decodedClient;

    public static SidingWallEntity? GuestAt(ICoreAPI api, IWorldChunk chunk, BlockPos pos)
    {
        var guests = Guests(chunk);
        if (guests == null || !guests.TryGetValue(LocalIndex3d(pos), out GuestRecord? record)) return null;
        return DecodedFor(api).GetValue(record, r => Decode(api, r, pos));
    }

    public static SidingWallEntity? GuestAt(ICoreAPI api, BlockPos pos)
    {
        IWorldChunk? chunk = api.World.BlockAccessor.GetChunkAtBlockPos(pos);
        return chunk == null ? null : GuestAt(api, chunk, pos);
    }

    // FromTreeAttributes relights and re-dirties the cell when Api is already the client and the
    // infill changed (SidingWallEntity.FromTreeAttributes), and it overwrites Pos from the record's
    // saved posx/posy/posz with no dimension - so Block goes in, FromTreeAttributes runs with Api
    // still unset, and only then do Pos and Api take the values a decode actually needs.
    internal static SidingWallEntity Decode(ICoreAPI api, GuestRecord record, BlockPos pos)
    {
        var entity = new SidingWallEntity { Block = api.World.GetBlock(new AssetLocation(record.BlockCode)) };
        entity.FromTreeAttributes(TreeAttribute.CreateFromBytes(record.EntityData), api.World);
        entity.Pos = pos.Copy();
        entity.Api = api;
        return entity;
    }

    internal static GuestRecord Encode(SidingWallEntity entity)
    {
        var tree = new TreeAttribute();
        entity.ToTreeAttributes(tree);
        return new GuestRecord { BlockCode = entity.Block.Code.ToString(), EntityData = tree.ToBytes() };
    }

    // Copy-on-write: never mutate the dictionary a reader might be iterating (see Guests above).
    // Only called from the main thread - the server's Set below, and the client's packet handler.
    private static void Replace(IWorldChunk chunk, BlockPos pos, GuestRecord? record)
    {
        var guests = Guests(chunk);
        var next = guests == null ? new Dictionary<int, GuestRecord>() : new Dictionary<int, GuestRecord>(guests);
        int index = LocalIndex3d(pos);
        if (record != null) next[index] = record;
        else next.Remove(index);
        chunk.LiveModData[ModDataKey] = next;
    }

    // Server side: replaces the record, relights the cell, marks the chunk to save and tells every
    // client. MarkAbsorptionChanged skips old == new (decision 0034), hence the real before and after.
    public static void Set(IWorldAccessor world, IWorldChunk chunk, BlockPos pos, GuestRecord? record)
    {
        int oldAbsorption = Absorption(GuestAt(world.Api, chunk, pos));
        Replace(chunk, pos, record);
        int newAbsorption = Absorption(GuestAt(world.Api, chunk, pos));
        if (oldAbsorption != newAbsorption) world.BlockAccessor.MarkAbsorptionChanged(oldAbsorption, newAbsorption, pos);

        chunk.MarkModified();
        serverChannel?.BroadcastPacket(new GuestSyncPacket { Pos = pos, Record = record });
    }

    // Every guest a chunk holds, at absolute positions, for SealedCellLightPostfix's whole-chunk
    // scan. chunkY carries the dimension, as in the tessellator (decision 0018).
    public static IEnumerable<(BlockPos pos, SidingWallEntity entity)> GuestsIn(
        ICoreAPI api, IWorldChunk chunk, int chunkX, int chunkY, int chunkZ)
    {
        var guests = Guests(chunk);
        if (guests == null) yield break;

        int size = GlobalConstants.ChunkSize;
        foreach (var (index, record) in guests)
        {
            int x = index % size, z = index / size % size, y = index / (size * size);
            var pos = new BlockPos(chunkX * size + x, chunkY * size % 32768 + y, chunkZ * size + z, chunkY / 1024);
            yield return (pos, DecodedFor(api).GetValue(record, r => Decode(api, r, pos)));
        }
    }
}
