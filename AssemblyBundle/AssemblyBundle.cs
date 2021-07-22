using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace XamAsmUnZ
{
    public class AssemblyBundle
    {
        public assembly_bundle_entry bundleEntry;
        public string ModuleName;
        public byte[] gzData;

        public AssemblyBundle()
        {
        }

        public static AssemblyBundle FindMatchingGZData(BinaryReader br, AssemblyBundle draftBundle, List<GZipSegment> gzDataEntries)
        {
            AssemblyBundle outBundle = draftBundle;
            for (int i = 0; i < gzDataEntries.Count; i++)
            {

                ulong current_gz_entry = gzDataEntries[i].ofsStart;
                if (current_gz_entry == 0)
                    continue;

                //probe GZ to see if this is correct
                ulong eogzSegmentOfs = current_gz_entry + draftBundle.bundleEntry.bundleGzDataCompressed;
                ulong eogzSegmentSizeOfs = eogzSegmentOfs - 4; //4bytes for size

                br.BaseStream.Seek((long)eogzSegmentSizeOfs, SeekOrigin.Begin);

                uint gzSegSize = br.ReadUInt32();
                if (gzSegSize != draftBundle.bundleEntry.bundleGzDataUncompressed)
                {
                    continue;
                }

                //We have a valid Gz entry!
                br.BaseStream.Seek((long)current_gz_entry, SeekOrigin.Begin);
                draftBundle.gzData = br.ReadBytes((int)draftBundle.bundleEntry.bundleGzDataCompressed);
                outBundle = draftBundle;
                break;
            }
            return outBundle;
        }


    }
}
