using System;
using System.Collections.Generic;
using System.IO;

using ELFSharp;
using ELFSharp.ELF.Sections;


namespace XamAsmUnZ
{

    public class GZipSegment
    {
        public ulong ptrStart;
        public ulong ofsStart;

        public static List<GZipSegment> FindGZSegments(BinaryReader br, ELFSharp.ELF.ELF<ulong> elfFile, Section<ulong> targetSection)
        {
            List<GZipSegment> segs = new List<GZipSegment>();
            br.BaseStream.Seek((long)targetSection.Offset, SeekOrigin.Begin);

            for (ulong b = 0; b < targetSection.Size; b += 2)
            {
                ulong offset = (ulong)br.BaseStream.Position;
                byte[] gzAB = br.ReadBytes(2);
                if (gzAB[0] == 0x1f && gzAB[1] == 0x8b)
                {
                    segs.Add(new GZipSegment() { ptrStart = Utilities.GetRVAFromFileOffset(elfFile, offset), ofsStart = offset });
                }
            }
            return segs;
        }
    }

}
