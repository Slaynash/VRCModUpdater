using System.Runtime.InteropServices;

namespace Winuser
{
    [StructLayout(LayoutKind.Sequential)]
    public struct DrawTextParams
    {
        public uint cbSize;
        public int iTabLength;
        public int iLeftMargin;
        public int iRightMargin;
        public uint uiLengthDrawn;
    }
}