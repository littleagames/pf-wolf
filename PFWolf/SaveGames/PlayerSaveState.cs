using PFWolf.Enums;
using PFWolf.Extensions;
using PFWolf.Gameplay;

namespace PFWolf.SaveGames;

/// <summary>A timed switch still waiting to turn back, as a save keeps it</summary>
internal sealed record SavedTimedSwitch(int X, int Y, bool Patch, int From, int To, ushort Tag, ControlDir Dir, List<string> Actions, int TicsLeft);

/// <summary>
/// What a save keeps of the player beyond the game state and the inventory, and of the level's
/// clocks: the mounted gun they're manning (Gameplay/MountedWeapons.cs), what's latched onto
/// them (Gameplay/Latching.cs), what they're riding (Gameplay/Vehicles.cs), going unseen
/// (Gameplay/Stealth.cs), their kill chain, and the timed switches still waiting to turn back.
/// The gun and the latcher are by their places among the level's saved actors
/// (MapManager.GetSavedActors), -1 for none.
/// </summary>
internal sealed record PlayerSaveState(
    int Mount, string? MountGun, string? MountFrom, string? MountMap,
    int Latch, string? LatchGun, string? LatchFrom, string? LatchMap, int LatchStrikes, int MoveSlowdown,
    string? Vehicle, string? VehicleMap, int VehicleSpeed, int VehicleThrottle,
    bool Undetected, int StealthKills, int SuspicionMeter, int SuspicionPending,
    int KillChain, int KillChainAt,
    List<SavedTimedSwitch> TimedSwitches)
{
    internal static PlayerSaveState Capture(Entities.PlayerState p, List<Entities.Actors.Actor> actors, IEnumerable<TimedSwitch> timed)
    {
        // By reference: actors are records, so IndexOf would match a look-alike by value
        int IndexOf(Entities.Actors.Actor? actor) => actor == null ? -1 : actors.FindIndex(a => ReferenceEquals(a, actor));
        return new PlayerSaveState(
            IndexOf(p.Mount), p.MountGun, p.MountFrom, p.MountMap,
            IndexOf(p.Latch), p.LatchGun, p.LatchFrom, p.LatchMap, p.LatchStrikes, p.MoveSlowdown,
            p.Vehicle, p.VehicleMap, p.VehicleSpeed, p.VehicleThrottle,
            p.Undetected, p.StealthKills, p.SuspicionMeter, p.SuspicionPending,
            p.KillChain, p.KillChainAt,
            timed.Select(t => new SavedTimedSwitch(t.X, t.Y, t.Patch, t.From, t.To, t.Tag, t.Dir, [.. t.Actions], t.TicsLeft)).ToList());
    }

    /// <summary>Puts it back on the player, the gun and latcher found among the level's restored actors</summary>
    internal void ApplyTo(Entities.PlayerState p, List<Entities.Actors.Actor> actors)
    {
        Entities.Actors.Actor? At(int index) => index >= 0 && index < actors.Count ? actors[index] : null;
        (p.Mount, p.MountGun, p.MountFrom, p.MountMap) = (At(Mount), MountGun, MountFrom, MountMap);
        (p.Latch, p.LatchGun, p.LatchFrom, p.LatchMap, p.LatchStrikes, p.MoveSlowdown) =
            (At(Latch), LatchGun, LatchFrom, LatchMap, LatchStrikes, Math.Max(MoveSlowdown, 1));
        (p.Vehicle, p.VehicleMap, p.VehicleSpeed, p.VehicleThrottle) = (Vehicle, VehicleMap, VehicleSpeed, VehicleThrottle);
        (p.Undetected, p.StealthKills, p.SuspicionMeter, p.SuspicionPending) = (Undetected, StealthKills, SuspicionMeter, SuspicionPending);
        (p.KillChain, p.KillChainAt) = (KillChain, KillChainAt);
    }

    internal void Write(BinaryWriter bw)
    {
        void Text(string? text) => bw.Write(text ?? "");
        bw.Write(Mount); Text(MountGun); Text(MountFrom); Text(MountMap);
        bw.Write(Latch); Text(LatchGun); Text(LatchFrom); Text(LatchMap); bw.Write(LatchStrikes); bw.Write(MoveSlowdown);
        Text(Vehicle); Text(VehicleMap); bw.Write(VehicleSpeed); bw.Write(VehicleThrottle);
        bw.Write(Undetected); bw.Write(StealthKills); bw.Write(SuspicionMeter); bw.Write(SuspicionPending);
        bw.Write(KillChain); bw.Write(KillChainAt);
        bw.Write(TimedSwitches.Count);
        foreach (var t in TimedSwitches)
        {
            bw.Write(t.X); bw.Write(t.Y); bw.Write(t.Patch); bw.Write(t.From); bw.Write(t.To); bw.Write(t.Tag); bw.Write((byte)t.Dir);
            bw.Write(t.Actions.Count);
            foreach (var action in t.Actions)
                bw.Write(action);
            bw.Write(t.TicsLeft);
        }
    }

    internal static PlayerSaveState Read(BinaryReader br)
    {
        string? Text() => br.ReadString() is { Length: > 0 } text ? text : null;
        return new PlayerSaveState(
            br.ReadInt32(), Text(), Text(), Text(),
            br.ReadInt32(), Text(), Text(), Text(), br.ReadInt32(), br.ReadInt32(),
            Text(), Text(), br.ReadInt32(), br.ReadInt32(),
            br.ReadBoolean(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32(),
            br.ReadInt32(), br.ReadInt32(),
            [.. Enumerable.Range(0, br.ReadCount()).Select(_ => new SavedTimedSwitch(
                br.ReadInt32(), br.ReadInt32(), br.ReadBoolean(), br.ReadInt32(), br.ReadInt32(), br.ReadUInt16(), (ControlDir)br.ReadByte(),
                [.. Enumerable.Range(0, br.ReadCount()).Select(_ => br.ReadString())], br.ReadInt32()))]);
    }
}
