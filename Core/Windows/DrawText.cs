namespace Windef
{
    public enum DrawText : int
    {
        TOP                    = 0x00000000,
        LEFT                   = 0x00000000,
        CENTER                 = 0x00000001,
        RIGHT                  = 0x00000002,
        VCENTER                = 0x00000004,
        BOTTOM                 = 0x00000008,
        WORDBREAK              = 0x00000010,
        SINGLELINE             = 0x00000020,
        EXPANDTABS             = 0x00000040,
        TABSTOP                = 0x00000080,
        NOCLIP                 = 0x00000100,
        EXTERNALLEADING        = 0x00000200,
        CALCRECT               = 0x00000400,
        NOPREFIX               = 0x00000800,
        INTERNAL               = 0x00001000,
        EDITCONTROL            = 0x00002000,
        PATH_ELLIPSIS          = 0x00004000,
        END_ELLIPSIS           = 0x00008000,
        MODIFYSTRING           = 0x00010000,
        RTLREADING             = 0x00020000,
        WORD_ELLIPSIS          = 0x00040000,
        NOFULLWIDTHCHARBREAK   = 0x00080000,
        HIDEPREFIX             = 0x00100000,
        PREFIXONLY             = 0x00200000
    }
}
