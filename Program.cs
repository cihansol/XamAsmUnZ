using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using ELFSharp;
using ELFSharp.ELF.Sections;



namespace XamAsmUnZ
{
    public class Program
    {

        static void Main(string[] args)
        {

            Console.WriteLine("XamAsmUnZ");
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

        static void DeflateAssemblies(List<AssemblyBundle> assemblyBundles, string outFolderPath)
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

            var elfFile = ELFSharp.ELF.ELFReader.Load<ulong>(elfFilePath);
            var fs = new FileStream(elfFilePath, FileMode.Open, FileAccess.Read);
            var binr = new BinaryReader(fs);

            Console.WriteLine("");
            Console.WriteLine("Finding assembly bundles.");
            Console.WriteLine("");

            Section<ulong> rodataSection = null;
            Section<ulong> dataSection = null;
            foreach (var section in elfFile.Sections)
            {
                if (section.Name == ".rodata")
                    rodataSection = section;
                else if (section.Name == ".data")
                    dataSection = section;
            }

            if (rodataSection == null || dataSection == null)
            {
                Console.WriteLine(".rodata/.data not found");
                return;
            }

            //Get all GZ matches (lazy)
            List<GZipSegment> potentialGzEntries = GZipSegment.FindGZSegments(binr, elfFile, rodataSection);
            if (potentialGzEntries.Count == 0)
            {
                Console.WriteLine("Unable to find any GZip segments. File is potentially packed.");
                return;
            }

            List<AssemblyBundle> assemblyBundles = new List<AssemblyBundle>();

            //Grab the asset pointers
            List<ulong> dataAsmPointers = new List<ulong>();
            binr.BaseStream.Seek((long)dataSection.Offset, SeekOrigin.Begin);
            while (true)
            {
                ulong currentPtr = binr.ReadUInt64();
                if (currentPtr == 0)
                    break;
                dataAsmPointers.Add(currentPtr);
            }

            if (dataAsmPointers.Count == 0)
            {
                Console.WriteLine("Unable to find any data pointers. Data section could be empty inside ELF");
                return;
            }

            //Loop each reference pointer
            for (int p = 0; p < dataAsmPointers.Count; p++)
            {
                ulong dptr = dataAsmPointers[p];
                AssemblyBundle bundle = new AssemblyBundle();

                ulong fo = Utilities.SeekToAddress(binr, elfFile, dptr);
                bundle.bundleEntry = Utilities.FromBinaryReader<assembly_bundle_entry>(binr);

                //read the name
                Utilities.SeekToAddress(binr, elfFile, bundle.bundleEntry.bundleNamePtr);
                bundle.ModuleName = Utilities.ReadASCIIZstring(binr);


                //find matching GZ stream
                var generatedBundle = AssemblyBundle.FindMatchingGZData(binr, bundle, potentialGzEntries);
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

            //Uncompress and write to disk
            Console.WriteLine("");
            Console.WriteLine("Deflating bundles and writting to disk.");
            Console.WriteLine("");

            //Create output directory
            string outputBundleDirectory = Path.Combine(workingDirectory, "extracted_assemblies");
            Directory.CreateDirectory(outputBundleDirectory);

            //Deflate and write out binaries
            DeflateAssemblies(assemblyBundles, outputBundleDirectory);

            Console.WriteLine("Extraction Complete.");

            assemblyBundles.Clear();
            binr.Close();
            elfFile.Dispose();

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

            string outputDirectory = Path.Combine(workingDirectory, "uncompressed_assemblies");
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
