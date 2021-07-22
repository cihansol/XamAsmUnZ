using System;
using System.Runtime.InteropServices;

namespace XamAsmUnZ
{
    public static class XALZ
    { 
        public static readonly int XALZMagic = 0x5A4C4158;

        //source from https://github.com/xamarin/xamarin-android/pull/4686
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct xalz_header
        {
            public UInt32 magic; // 'XALZ', little-endian
            public UInt32 index; // Index into an internal assembly descriptor table
            public UInt32 uncompressed_length;
        };
    }
}
