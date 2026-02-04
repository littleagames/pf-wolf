namespace Wolf3D;

internal partial class Program
{
    internal static objstruct SpawnNewObj(uint tilex, uint tiley, statestruct state)
    {
        objstruct newobj = GetNewActor();
        newobj.state = state;
        if (state.tictime != 0)
            newobj.ticcount = (short)(US_RndT() % state.tictime + 1);
        else
            newobj.ticcount = 0;

        newobj.tilex = (byte)tilex;
        newobj.tiley = (byte)tiley;
        newobj.x = (int)((tilex << TILESHIFT) + TILEGLOBAL / 2);
        newobj.y = (int)((tiley << TILESHIFT) + TILEGLOBAL / 2);
        newobj.dir = (int)objdirtypes.nodir;

        actorat[tilex, tiley] = MAXACTORS - objfreelist;
        newobj.areanumber = (byte)(MAPSPOT((int)tilex, (int)tiley, 0) - AREATILE);

        return newobj;

    }
}
