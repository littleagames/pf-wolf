using Wolf3D.Enums;

namespace Wolf3D;

internal struct ControlInfo
{
    public bool button0, button1, button2, button3;
    public short x, y;
    public short xaxis, yaxis;
    public Direction dir;
}