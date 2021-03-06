using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO.Compression;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;

using LZ4;


namespace XamAsmUnZ
{
    static class Utilities
    {
        public static string GetApplicationDirectory()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(dir))
                return dir;
            else
                return Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        }

        public static int SizeOf(Type t)
        {
            return Marshal.SizeOf(t);
        }

        public static T ByteArrayToStructure<T>(byte[] byteData) where T : struct
        {
            byte[] buffer = byteData;
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            T data = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return data;
        }

        public static T FromBinaryReader<T>(BinaryReader reader) where T : struct
        {
            // Read in a byte array
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));
            T theStructure = ByteArrayToStructure<T>(bytes);
            return theStructure;
        }

        public static uint SeekToAddress32(BinaryReader br, ELFSharp.ELF.ELF<uint> elfFile, uint rva)
        {
            return (uint)br.BaseStream.Seek(GetFileOffsetFromRVA32(elfFile, rva), SeekOrigin.Begin);
        }

        public static ulong SeekToAddress(BinaryReader br, ELFSharp.ELF.ELF<ulong> elfFile, ulong rva)
        {
            return (ulong)br.BaseStream.Seek((long)GetFileOffsetFromRVA(elfFile, rva), SeekOrigin.Begin);
        }

        public static UInt64 GetRVAFromVA(ELF<ulong> elfFile, UInt64 VA)
        {
            Segment<ulong> low = null;
            foreach (var segment in elfFile.Segments)
            {
                if (segment.Type == SegmentType.Load)
                {
                    if (low == null)
                        low = segment;
                    else
                    {
                        if (segment.Address < low.Address)
                            low = segment;
                    }
                }
            }

            if (low == null)
                return 0xFFFFFFFFFFFFFFFF;

            var RVA = VA - low.Address;
            return RVA;
        }

        public static UInt32 GetRVAFromVA32(ELF<uint> elfFile, UInt32 VA)
        {
            Segment<uint> low = null;
            foreach (var segment in elfFile.Segments)
            {
                if (segment.Type == SegmentType.Load)
                {
                    if (low == null)
                        low = segment;
                    else
                    {
                        if (segment.Address < low.Address)
                            low = segment;
                    }
                }
            }

            if (low == null)
                return 0xFFFFFFFF;

            var RVA = VA - low.Address;
            return RVA;
        }


        public static UInt32 GetRVAFromFileOffset32(ELF<uint> elfFile, UInt32 fileOffset)
        {
            UInt32 rva = 0xFFFFFFFF;

            // Look up the section the RVA belongs to
            bool bFound = false;
            Section<uint> found_section = null;

            foreach (var section in elfFile.Sections)
            {
                var section32 = section;
                if ((fileOffset >= section32.Offset) && 
                    (fileOffset < section32.Offset + section32.Size))
                {
                    // Yes, the RVA belongs to this section
                    bFound = true;
                    found_section = section32;
                    break;
                }
            }

            if (!bFound)
            {
                // Section not found
                return rva;
            }

            // Calc Delta
            UInt32 Diff = (UInt32)(fileOffset - found_section.Offset);
            rva = found_section.LoadAddress + Diff;

            return rva;
        }

        public static UInt64 GetRVAFromFileOffset(ELFSharp.ELF.ELF<ulong> elfFile, UInt64 fileOffset)
        {
            UInt64 rva = 0xFFFFFFFFFFFFFFFF;

            // Look up the section the RVA belongs to
            bool bFound = false;
            Section<ulong> found_section = null;

            foreach (var section in elfFile.Sections)
            {
                if ((fileOffset >= section.Offset) &&
                          (fileOffset < section.Offset + section.Size))
                {
                    // Yes, the RVA belongs to this section
                    bFound = true;
                    found_section = section;
                    break;
                }
            }

            if (!bFound)
            {
                // Section not found
                return rva;
            }

            // Calc Delta
            UInt32 Diff = (UInt32)(fileOffset - found_section.Offset);
            rva = found_section.LoadAddress + Diff;

            return rva;
        }

        public static UInt32 GetFileOffsetFromRVA32(ELFSharp.ELF.ELF<uint> elfFile, UInt32 RVA)
        {
            UInt32 file_offset = 0xFFFFFFFF;

            // Look up the section the RVA belongs to
            bool bFound = false;
            Section<uint> found_section = null;

            foreach (var section in elfFile.Sections)
            {
                if ((RVA >= section.LoadAddress) &&
                          (RVA < section.LoadAddress + section.Size))
                {
                    // Yes, the RVA belongs to this section
                    bFound = true;
                    found_section = section;
                    break;
                }
            }

            if (!bFound)
            {
                // Section not found
                return file_offset;
            }

            // Look up the file offset using the section header
            UInt32 Diff = (UInt32)(found_section.LoadAddress - found_section.Offset);
            file_offset = RVA - Diff;

            // Complete
            return file_offset;
        }

        public static UInt64 GetFileOffsetFromRVA(ELFSharp.ELF.ELF<ulong> elfFile, UInt64 RVA)
        {
            UInt64 file_offset = 0xFFFFFFFFFFFFFFFF;

            // Look up the section the RVA belongs to
            bool bFound = false;
            Section<ulong> found_section = null;

            foreach (var section in elfFile.Sections)
            {
                if ((RVA >= section.LoadAddress) &&
                          (RVA < section.LoadAddress + section.Size))
                {
                    // Yes, the RVA belongs to this section
                    bFound = true;
                    found_section = section;
                    break;
                }
            }

            if (!bFound)
            {
                // Section not found
                return file_offset;
            }

            // Look up the file offset using the section header
            UInt32 Diff = (UInt32)(found_section.LoadAddress - found_section.Offset);
            file_offset = RVA - Diff;

            // Complete
            return file_offset;
        }

        public static string ReadASCIIZstring(BinaryReader reader, uint offset = 0xFFFFFFFF)
        {
            if (offset != 0xFFFFFFFF)
                reader.BaseStream.Position = offset;

            //Read till Null terminator \0
            string str = string.Empty;
            byte b = 0;
            b = reader.ReadByte();
            while (b != 0)
            {
                str += ((char)b).ToString();
                b = reader.ReadByte();
            }
            return str;
        }

        public static byte[] Decompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream(data))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        public static byte[] DecompressLz4(byte[] data, Int32 uncompressedSize)
        {
            var outData = new byte[uncompressedSize];
            LZ4Codec.Decode(data, 0, data.Length, outData, 0, uncompressedSize);
            return outData;
        }


    }
}
