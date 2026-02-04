namespace Wolf3D;

internal partial class Program
{
    internal static statestruct s_grddie4 = new statestruct(0, (short)spritenums.SPR_GRD_DEAD, 0, null, null, s_grddie4);

    internal static void SpawnBJVictory()
    {
        // TODO:
    }

    internal static void SpawnStand(int which, int tilex, int tiley, int dir)
    {

    }

    internal static void SpawnPatrol(int which, int tilex, int tiley, int dir)
    {

    }

    internal static void SpawnDeadGuard(int tilex, int tiley)
    {
        objstruct newobj = SpawnNewObj((uint)tilex, (uint)tiley, s_grddie4);
        newobj.flags |= (uint)objflags.FL_NONMARK; // walk through moving enemy fix
        newobj.obclass = (byte)classtypes.inertobj;
    }

    internal static void SpawnBoss(int tilex, int tiley)
    {

    }

    internal static void SpawnGretel(int tilex, int tiley)
    {

    }

    internal static void SpawnFakeHitler(int tilex, int tiley)
    {

    }
    internal static void SpawnHitler(int tilex, int tiley)
    {

    }
    internal static void SpawnGift(int tilex, int tiley)
    {

    }
    internal static void SpawnGhosts(int which, int tilex, int tiley)
    {

    }


    internal static void SpawnSchabbs(int tilex, int tiley)
    {

    }

    internal static void SpawnFat(int tilex, int tiley)
    {

    }
}
