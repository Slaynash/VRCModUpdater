using System;
using System.Runtime.InteropServices;

namespace VRCModUpdater.Core.Externs
{
    public static class Il2Cpp
    {
        [DllImport("GameAssembly", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public extern static IntPtr il2cpp_resolve_icall(string name);
    }
}
