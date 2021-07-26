using System;
using System.Runtime.InteropServices;

namespace XamAsmUnZ
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct assembly_bundle_entry32
    {
        public UInt32 bundleNamePtr;
        public UInt32 bundleGzDataPtr;
        public UInt32 bundleGzDataUncompressed;
        public UInt32 bundleGzDataCompressed;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct assembly_bundle_entry64
    {
        public UInt64 bundleNamePtr;
        public UInt64 bundleGzDataPtr;
        public UInt64 bundleGzDataUncompressed;
        public UInt64 bundleGzDataCompressed;
    };

}
