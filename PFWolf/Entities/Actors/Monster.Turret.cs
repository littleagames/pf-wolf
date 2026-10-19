namespace PFWolf.Entities.Actors;

/*
=============================================================================

                                SCANNING

End of Destiny's turret (wl_act2.cpp T_Scan): it doesn't move. T_Scan("AttackState") as its
chase think: on first seeing the player it turns to the way next round from them (their
direction, as GetCardinalDirection rounds it, plus one eighth), and from then on goes to the
attack state only while the player stands in front of it (where it shows its front: within an
eighth's width of the way it faces). Then it locks on (no way of its own) and attacks for as long
as it sees them; once they're out of sight it turns away again.

=============================================================================
*/

internal partial record Monster
{
    internal void Scan(string[] args)
    {
        if (World?.Session.GameState.VictoryFlag == true)
            return;

        var attack = args.ElementAtOrDefault(0)?.Trim() is { Length: > 0 } a ? a : "Attack";
        if (CheckSight() && Target is { } seen)
        {
            if (RuntimeFlags.Has(ObjFlags.FirstAttack))
            {
                Dir = TurnedFrom(seen);
                RuntimeFlags &= ~ObjFlags.FirstAttack;
            }
            else if (Dir == ObjDir.NoDir || Faces(seen))
            {
                Dir = ObjDir.NoDir;             // locked onto them until they're out of sight
                SetState(attack);
            }
        }
        else if (Dir == ObjDir.NoDir && TargetIfAny is { } lost)
            Dir = TurnedFrom(lost);
    }

    // The way next round from where someone is, as EoD's GetCardinalDirection rounds it: east
    // from 315 degrees, then each eighth on from 0 (0-44 northeast, 45-89 north, ...)
    ObjDir TurnedFrom(Actor who)
    {
        int angle = AngleTo(who);
        int cardinal = angle > 315 ? 0 : Math.Min(angle / 45 + 1, 7);
        return (ObjDir)((cardinal + 1) % 8);
    }

    // Whether someone stands where it shows its front: within half an eighth of the way it faces
    bool Faces(Actor who)
    {
        int diff = Math.Abs(AngleTo(who) - (int)Dir * 45) % 360;
        return Math.Min(diff, 360 - diff) <= 22;
    }

    // Degrees from here to someone (0 east, 90 north)
    int AngleTo(Actor who)
    {
        double angle = Math.Atan2(Y - who.Y, who.X - X) * 180 / Math.PI;
        return ((int)Math.Floor(angle) % 360 + 360) % 360;
    }
}
