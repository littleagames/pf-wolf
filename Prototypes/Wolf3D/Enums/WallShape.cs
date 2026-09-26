namespace Wolf3D.Enums;

/// <summary>
/// The shape of a wall tile (MapManager.wallshape). A diagonal is named by its solid corner:
/// that corner's two tile edges stay ordinary square faces, and the wall between the other two
/// corners is the 45 degree face. The open triangle across from it is empty space.
/// </summary>
internal enum WallShape : byte
{
    Square,

    /// <summary>Solid north and west edges; the face runs from the NE corner to the SW corner.</summary>
    SolidNW,

    /// <summary>Solid north and east edges; the face runs from the NW corner to the SE corner.</summary>
    SolidNE,

    /// <summary>Solid south and west edges; the face runs from the NW corner to the SE corner.</summary>
    SolidSW,

    /// <summary>Solid south and east edges; the face runs from the NE corner to the SW corner.</summary>
    SolidSE,
}
