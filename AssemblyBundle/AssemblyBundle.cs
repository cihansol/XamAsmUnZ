using System;
using System.IO;
using System.Collections.Generic;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;

namespace XamAsmUnZ
{
    public class AssemblyBundle
    {
        public assembly_bundle_entry32 bundleEntry32;
        public assembly_bundle_entry64 bundleEntry;
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

        public static AssemblyBundle FindMatchingGZData32(BinaryReader br, AssemblyBundle draftBundle, List<GZipSegment> gzDataEntries)
        {
            AssemblyBundle outBundle = draftBundle;
            for (int i = 0; i < gzDataEntries.Count; i++)
            {
                uint current_gz_entry = gzDataEntries[i].ofsStart32;
                if (current_gz_entry == 0)
                    continue;

                //probe GZ to see if this is correct
                uint eogzSegmentOfs = current_gz_entry + draftBundle.bundleEntry32.bundleGzDataCompressed;
                uint eogzSegmentSizeOfs = eogzSegmentOfs - 4; //4bytes for size

                br.BaseStream.Seek(eogzSegmentSizeOfs, SeekOrigin.Begin);

                uint gzSegSize = br.ReadUInt32();
                if (gzSegSize != draftBundle.bundleEntry32.bundleGzDataUncompressed)
                {
                    continue;
                }

                //We have a valid Gz entry!
                br.BaseStream.Seek(current_gz_entry, SeekOrigin.Begin);
                draftBundle.gzData = br.ReadBytes((int)draftBundle.bundleEntry32.bundleGzDataCompressed);
                outBundle = draftBundle;
                break;
            }
            return outBundle;
        }

        public static List<ulong> GetBundleDataPointers(BinaryReader br, Section<ulong> data64Section)
        {
            List<ulong> dataAsmPointers = new List<ulong>();
            br.BaseStream.Seek((long)data64Section.Offset, SeekOrigin.Begin);
            while (true)
            {
                ulong currentPtr = br.ReadUInt64();
                if (currentPtr == 0)
                    break;
                dataAsmPointers.Add(currentPtr);
            }
            return dataAsmPointers;
        }

        public static List<uint> GetBundleDataPointers32(BinaryReader br, Section<uint> data32Section)
        {
            List<uint> dataAsmPointers = new List<uint>();
            br.BaseStream.Seek(data32Section.Offset, SeekOrigin.Begin);
            while (true)
            {
                uint currentPtr = br.ReadUInt32();
                if (currentPtr == 0)
                    break;
                dataAsmPointers.Add(currentPtr);
            }
            return dataAsmPointers;
        }

        public static List<AssemblyBundle> ReadAllBundles(BinaryReader br, ELF<ulong> elf, List<ulong> dataAsmPointers, List<GZipSegment> potentialGzEntries)
        {
            List<AssemblyBundle> assemblyBundles = new List<AssemblyBundle>();

            //Loop each reference pointer
            for (int p = 0; p < dataAsmPointers.Count; p++)
            {
                ulong dptr = dataAsmPointers[p];
                AssemblyBundle bundle = new AssemblyBundle();

                Utilities.SeekToAddress(br, elf, dptr);
                bundle.bundleEntry = Utilities.FromBinaryReader<assembly_bundle_entry64>(br);

                //read the name
                Utilities.SeekToAddress(br, elf, bundle.bundleEntry.bundleNamePtr);
                bundle.ModuleName = Utilities.ReadASCIIZstring(br);


                //find matching GZ stream
                var generatedBundle = AssemblyBundle.FindMatchingGZData(br, bundle, potentialGzEntries);
                if (generatedBundle.gzData != null)
                {
                    assemblyBundles.Add(generatedBundle);
                    Console.WriteLine($"Bundle: [{generatedBundle.ModuleName}] \nGZData Offset: [{generatedBundle}] \n" +
                        $"GZData Size Compressed: [{generatedBundle.bundleEntry.bundleGzDataCompressed}] \n" +
                        $"GZData Size Uncompressed: [{generatedBundle.bundleEntry.bundleGzDataUncompressed}] \n");

                }
                else
                    Console.WriteLine($"Found invalid GZStream in library @ {generatedBundle} for .data pointer {dptr} module name: {bundle.ModuleName}");

            }

            return assemblyBundles;
        }

        public static List<AssemblyBundle> ReadAllBundles32(BinaryReader br, ELF<uint> elf, List<uint> dataAsmPointers, List<GZipSegment> potentialGzEntries)
        {
            List<AssemblyBundle> assemblyBundles = new List<AssemblyBundle>();

            //Loop each reference pointer
            for (int p = 0; p < dataAsmPointers.Count; p++)
            {
                uint dptr = dataAsmPointers[p];
                AssemblyBundle bundle = new AssemblyBundle();

                Utilities.SeekToAddress32(br, elf, dptr);
                bundle.bundleEntry32 = Utilities.FromBinaryReader<assembly_bundle_entry32>(br);

                //read the name
                Utilities.SeekToAddress32(br, elf, bundle.bundleEntry32.bundleNamePtr);
                bundle.ModuleName = Utilities.ReadASCIIZstring(br);


                //find matching GZ stream
                var generatedBundle = AssemblyBundle.FindMatchingGZData32(br, bundle, potentialGzEntries);
                if (generatedBundle.gzData != null)
                {
                    assemblyBundles.Add(generatedBundle);
                    Console.WriteLine($"Bundle: [{generatedBundle.ModuleName}] \nGZData Offset: [{generatedBundle}] \n" +
                        $"GZData Size Compressed: [{generatedBundle.bundleEntry32.bundleGzDataCompressed}] \n" +
                        $"GZData Size Uncompressed: [{generatedBundle.bundleEntry32.bundleGzDataUncompressed}] \n");

                }
                else
                    Console.WriteLine($"Found invalid GZStream in library @ {generatedBundle} for .data pointer {dptr} module name: {bundle.ModuleName}");

            }

            return assemblyBundles;
        }


    }
}
