namespace WinGDI
{
    public enum FontClipPrecision
    {
        Default = 0,
        Character = 1,
        Stroke = 2,

        Mask = 0xf,

        LHAngles = (1 << 4),
        TTAlways = (2 << 4),
        DFADisable = (4 << 4),
        Embedded = (8 << 4),
    }
}
