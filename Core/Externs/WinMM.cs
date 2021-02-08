using System.Runtime.InteropServices;

namespace VRCModUpdater.Core.Externs
{
    public static class WinMM
    {
        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint uPeriod);
    }
}
