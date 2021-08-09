using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using ELFSharp;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;


namespace XamAsmUnZ
{
    public class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("XamAsmUnZ");
            Console.WriteLine("Author: Cihan");
            Console.WriteLine(string.Empty);

            if (args.Count() < 2)
            {
                Console.WriteLine("Usage: XamAsmUnZ -elf path/to/libmonodroid_bundle_app.so");
                Console.WriteLine("Usage: XamAsmUnZ -dir path/to/folder");
                Console.WriteLine(" ");
                Console.WriteLine("Example: XamAsmUnZ -elf O:/fakepath/libmonodroid_bundle_app.so");
                Console.WriteLine("Example: XamAsmUnZ -dir O:/fakepath/assemblies");
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                return;
            }

            string workingDirectory = Utilities.GetApplicationDirectory();
            string inputType = args[0];



            switch (inputType)
            {
                case "-elf":
                    HandleELF(workingDirectory, args[1]);
                    break;
                case "-dir":
                    HandleDIR(workingDirectory, args[1]);
                    break;
                default:
                    Console.WriteLine($"Invalid input type: {inputType}");
                    break;
            }

            Console.ReadKey();

        }

        static void inflateAssemblies(List<AssemblyBundle> assemblyBundles, string outFolderPath)
        {
            foreach (var assemblyBundle in assemblyBundles)
            {
                Console.WriteLine($"Deflating {assemblyBundle.ModuleName}");
                byte[] rawAssembly = Utilities.Decompress(assemblyBundle.gzData);
                if (rawAssembly == null || rawAssembly.Length == 0)
                {
                    Console.WriteLine("Error.");
                    continue;
                }

                string outAssemblyFullPath = Path.Combine(outFolderPath, assemblyBundle.ModuleName);
                File.WriteAllBytes(outAssemblyFullPath, rawAssembly);
                Console.WriteLine($"Done.");
            }
        }


        static void HandleELF(string workingDirectory, string inputFilePath)
        {
            ELF<uint> elf32 = null;
            ELF<ulong> elf64 = null;

            Section<uint> rodata32Section = null;
            Section<uint> data32Section = null;

            Section<ulong> rodata64Section = null;
            Section<ulong> data64Section = null;


            string elfFilePath = inputFilePath;
            if (!File.Exists(elfFilePath))
            {
                //Check the working directory
                string newWorkingDirPath = Path.Combine(workingDirectory, elfFilePath);
                if (File.Exists(newWorkingDirPath))
                    elfFilePath = newWorkingDirPath;
                else
                {
                    Console.WriteLine($"Error Input file {inputFilePath} doesn't seem to exist!");
                    return;
                }
            }

            var elfTypeCheck = ELFReader.CheckELFType(elfFilePath);
            if (elfTypeCheck == Class.Bit32)
                elf32 = ELFReader.Load<uint>(elfFilePath);
            else if (elfTypeCheck == Class.Bit64)
                elf64 = ELFReader.Load<ulong>(elfFilePath);
            else
            {
                Console.WriteLine("Input file is not an ELF.");
                return;
            }
            Console.WriteLine("ELF file is of type: " + elfTypeCheck.ToString());


            var fs = new FileStream(elfFilePath, FileMode.Open, FileAccess.Read);
            var binr = new BinaryReader(fs);

            Console.WriteLine("");
            Console.WriteLine("Finding assembly bundles.");
            Console.WriteLine("");

            if (elf32 != null)
                foreach (var section in elf32.Sections)
                {
                    if (section.Name == ".rodata")
                        rodata32Section = section;
                    else if (section.Name == ".data")
                        data32Section = section;
                }
            else
                foreach (var section in elf64.Sections)
                {
                    if (section.Name == ".rodata")
                        rodata64Section = section;
                    else if (section.Name == ".data")
                        data64Section = section;
                }


            if ((rodata32Section == null || data32Section == null) && (rodata64Section == null || data64Section == null))
            {
                Console.WriteLine(".rodata/.data not found");
                return;
            }


            //Get all GZ matches (lazy)
            List<GZipSegment> potentialGzEntries;
            if (elf32 != null)
                potentialGzEntries = GZipSegment.FindGZSegments32(binr, elf32, rodata32Section);
            else
                potentialGzEntries = GZipSegment.FindGZSegments(binr, elf64, rodata64Section);

            if (potentialGzEntries.Count == 0)
            {
                Console.WriteLine("Unable to find any GZip segments. File is potentially packed.");
                return;
            }

            List<AssemblyBundle> assemblyBundles;

            //Grab the asset pointers
            List<uint> dataAsmPointers32 = new List<uint>();
            List<ulong> dataAsmPointers = new List<ulong>();

            if (elf32 != null)
                dataAsmPointers32 = AssemblyBundle.GetBundleDataPointers32(binr, data32Section);
            else
                dataAsmPointers = AssemblyBundle.GetBundleDataPointers(binr, data64Section);


            if (dataAsmPointers32.Count == 0 && dataAsmPointers.Count == 0)
            {
                Console.WriteLine("Unable to find any data pointers. Data section could be empty inside ELF");
                return;
            }

            //Read Bundles
            if (elf32 != null)
                assemblyBundles = AssemblyBundle.ReadAllBundles32(binr, elf32, dataAsmPointers32, potentialGzEntries);
            else
                assemblyBundles = AssemblyBundle.ReadAllBundles(binr, elf64, dataAsmPointers, potentialGzEntries);


            //Uncompress and write to disk
            Console.WriteLine("");
            Console.WriteLine("Deflating bundles and writting to disk.");
            Console.WriteLine("");

            //Create output directory
            string outputBundleDirectory = Path.Combine(Path.GetDirectoryName(inputFilePath), elf32 != null ? "extracted_assemblies32" : "extracted_assemblies64");
            Directory.CreateDirectory(outputBundleDirectory);

            //inflate and write out binaries
            inflateAssemblies(assemblyBundles, outputBundleDirectory);

            Console.WriteLine("Extraction Complete.");

            assemblyBundles.Clear();
            binr.Close();

            if (elf32 != null)
                elf32.Dispose();
            else
                elf64.Dispose();

            return;
        }

        


        static void HandleDIR(string workingDirectory, string inputFolderPath)
        {
            if (!Directory.Exists(inputFolderPath))
            {
                Console.WriteLine($"Error directory {inputFolderPath} doesn't seem to exist!");
                return;
            }

            string[] assemblyFiles = Directory.GetFiles(inputFolderPath, "*.dll", SearchOption.AllDirectories);
            if (assemblyFiles.Length == 0)
            {
                Console.WriteLine($"No (.dll) assemblies found in folder {inputFolderPath}");
                return;
            }

            string outputDirectory = Path.Combine(inputFolderPath, "uncompressed_assemblies");
            Directory.CreateDirectory(outputDirectory);

            foreach (var file in assemblyFiles)
            {
                string fileName = Path.GetFileName(file);
                byte[] assemblyBytes = File.ReadAllBytes(file);
                if (assemblyBytes.Length == 0)
                {
                    Console.WriteLine($"Unable to read {file}");
                    continue;
                }

                if (assemblyBytes.Length < Utilities.SizeOf(typeof(XALZ.xalz_header)))
                {
                    Console.WriteLine($"Invalid file {file}");
                    continue;
                }

                using (MemoryStream ms = new MemoryStream(assemblyBytes))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    XALZ.xalz_header header = Utilities.FromBinaryReader<XALZ.xalz_header>(br);
                    if (header.magic != XALZ.XALZMagic)
                    {
                        Console.WriteLine($"Invalid XALZ magic found in {file}");
                        Console.WriteLine($"Most likely a normal PE");
                        continue;
                    }

                    Console.WriteLine($"Found assembly: {fileName}");

                    byte[] lz4Data = br.ReadBytes((int)(ms.Length - ms.Position));

                    Console.WriteLine("Decompressing");
                    byte[] rawData = Utilities.DecompressLz4(lz4Data, (int)header.uncompressed_length);

                    string outAssemblyFullPath = Path.Combine(outputDirectory, fileName);
                    File.WriteAllBytes(outAssemblyFullPath, rawData);

                    Console.WriteLine("Done.");
                }
            }


            Console.WriteLine("Extraction Complete.");
            return;
        }



    }

}
