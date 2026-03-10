namespace Wolf3D;

internal enum Direction
{
    dir_North, dir_NorthEast,
    dir_East, dir_SouthEast,
    dir_South, dir_SouthWest,
    dir_West, dir_NorthWest,
    dir_None
}

internal struct ControlInfo
{
    public bool button0, button1, button2, button3;
    public short x, y;
    public short xaxis, yaxis;
    public Direction dir;
}