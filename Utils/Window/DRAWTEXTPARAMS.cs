using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public struct DRAWTEXTPARAMS
{
    public uint cbSize;
    public int iTabLength;
    public int iLeftMargin;
    public int iRightMargin;
    public uint uiLengthDrawn;
}
