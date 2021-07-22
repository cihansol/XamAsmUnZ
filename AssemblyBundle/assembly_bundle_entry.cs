using System;
using System.Runtime.InteropServices;

namespace XamAsmUnZ
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct assembly_bundle_entry
    {
        public UInt64 bundleNamePtr;
        public UInt64 bundleGzDataPtr;
        public UInt64 bundleGzDataUncompressed;
        public UInt64 bundleGzDataCompressed;
    };


}
