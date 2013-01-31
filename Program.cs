// --------------------------------------------------------------------------------
// <copyright file="Program.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
// --------------------------------------------------------------------------------

namespace stuffer
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml.Serialization;

    using Microsoft.Deployment.Compression;
    using Microsoft.Deployment.Compression.Cab;
    using Microsoft.Deployment.Resources;

    /// <summary>
    /// Main program
    /// </summary>
    public static class Program
    {
        private static bool classicMode;

        /// <summary>
        /// Main program entry
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        /// <returns>0 if successful, 1 for errors</returns>
        public static int Main(string[] args)
        {
            var sourcePath = string.Empty;
            var targetExe = string.Empty;
            var guid = string.Empty;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i].ToUpper())
                {
                    case "-B":
                    case "--PATH":
                        if (args.Length > i + 1)
                        {
                            targetExe = args[++i];
                        }
                        break;

                    case "-F":
                    case "--FOLDER":
                        if (args.Length > i + 1)
                        {
                            sourcePath = args[++i];
                        }
                        break;

                    case "-C":
                    case "--CAB":
                        if (args.Length > i + 1)
                        {
                            sourcePath = args[++i];
                        }
                        break;

                    case "-G":
                    case "-GUID":
                        if (args.Length > i + 1)
                        {
                            guid = args[++i];
                        }
                        break;

                    case "--CLASSIC":
                        classicMode = true;
                        break;

                    default:
                        if (string.IsNullOrEmpty(sourcePath))
                        {
                            sourcePath = args[i];
                            break;
                        }
                        
                        if (string.IsNullOrEmpty(targetExe))
                        {
                            targetExe = args[i];
                            break;
                        }

                        Console.WriteLine("Usage: stuffer <directory> <output EXE>");
                        return 1;
                }
            }

            // verify program arguments
            if (string.IsNullOrEmpty(sourcePath) || (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
                || (File.Exists(sourcePath) && (Path.GetExtension(sourcePath) ?? "").ToLower() != ".cab"))
            {
                throw new ArgumentException("Source must either be a valid directory or .CAB file!");
            }

            if (string.IsNullOrEmpty(targetExe) || !File.Exists(targetExe)
                || (Path.GetExtension(targetExe) ?? "").ToLower() != ".exe")
            {
                throw new ArgumentException("Target must be a valid EXE!");
            }

            // backup the target exe to a temporary path
            targetExe = Path.GetFullPath(targetExe);
            var backup = Path.Combine(Directory.GetParent(targetExe).FullName, Path.GetFileName(targetExe) + ".orig");
            File.Copy(targetExe, backup, true);

            if (!string.IsNullOrEmpty(guid))
            {
                Console.WriteLine("Writing GUIDs is not supported yet.");
            }

            if (classicMode)
            {
                // create a cab file
                var path = Path.GetFullPath("Product.cab");
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                Console.WriteLine("Compressing {0} to {1}...", sourcePath, path);
                sourcePath = CompressCab(sourcePath, path);
            }

            // if argument 1 was a .cab file instead of a directory
            if (File.Exists(sourcePath))
            {
                EmbedCab(sourcePath, targetExe);
            }
            else
            {
                EmbedDirectory(sourcePath, targetExe);
            }

            return 0;
        }

        /// <summary>
        /// Recursively iterate over a directory's contents and pack each file into a Windows Cabinet.
        /// </summary>
        /// <param name="directory">The source directory to iterate</param>
        /// <param name="cabName">The filename of the Windows Cabinet</param>
        /// <returns>The filesystem path of the packed Windows Cabinet</returns>
        private static string CompressCab(string directory, string cabName = "Product.cab")
        {
            var path = Path.GetFullPath(cabName);
            var cab = new CabInfo(path);
            cab.Pack(directory, true, CompressionLevel.Max, (sender, args) =>
                {
                    switch (args.ProgressType)
                    {
                        case ArchiveProgressType.FinishFile:
                            Console.WriteLine("File: {0}", args.CurrentFileName);
                            break;
                        case ArchiveProgressType.FinishArchive:
                            Console.WriteLine("Finished packing {0:N1}kB.", args.FileBytesProcessed);
                            break;
                    }
                });
            return path;
        }

        /// <summary>
        /// Recursively iterate over a directory's contents and add each file as a <code>CabResource</code>
        /// in the target Win32 PE file.
        /// </summary>
        /// <param name="directory">The source directory to iterate</param>
        /// <param name="target">PE file to embed cab with</param>
        private static void EmbedDirectory(string directory, string target)
        {
            // load existing resources from original exe into memory
            var rc = new ResourceCollection();
            rc.Find(target);
            rc.Load(target);

            // generate list of all files in directory tree
            var root = Path.GetFullPath(directory).TrimEnd(new[] { '\\' });
            var query = from file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                        select Path.GetFullPath(file).Substring(root.Length + 1);
            var fileList = query.ToList();

            using (var mem = new MemoryStream())
            {
                // compress XML output to gzip
                using (var gzip = new GZipStream(mem, CompressionMode.Compress, true))
                {
                    // serialize file listing to XML
                    var serializer = new XmlSerializer(fileList.GetType());
                    serializer.Serialize(gzip, fileList);
                }

                // store file listing as a new resource
                var data = mem.GetBuffer();
                rc.Add(new CabResource(@"FileList.xml", ref data));
            }

            // read and compress files in parallel to be embedded
            long compressed = 0;
            long total = 0;
            Action<CabResource> addResource = rc.Add;
            Parallel.ForEach(
                fileList,
                file =>
                    {
                        using (var filestream = File.OpenRead(Path.Combine(root, file)))
                        using (var mem = new MemoryStream())
                        {
                            // compress files with Gzip in 8k blocks
                            using (var gzip = new GZipStream(mem, CompressionMode.Compress, true))
                            using (var buffered = new BufferedStream(gzip, 8192))
                            {
                                filestream.CopyTo(buffered);
                            }

                            // create new CabResource with compressed byte data
                            lock (fileList)
                            {
                                total += filestream.Length;
                                compressed += mem.Length;
                                Console.WriteLine(
                                    "Compressed {0} - {1:N1}kB to {2:N1}kB.",
                                    file,
                                    filestream.Length / 1024f,
                                    mem.Length / 1024f);
                                var data = mem.GetBuffer();
                                addResource(new CabResource(file, ref data));
                            }
                        }
                    });

            Console.WriteLine(
                "Total: {0:N2}MB Compressed: {1:N2}MB. Saving...",
                total / 1024f / 1024f,
                compressed / 1024f / 1024f);
            rc.Save(target);
        }

        /// <summary>
        /// Deprecated feature to embed cabinets in bootstrapper
        /// </summary>
        /// <param name="cabFile">Compressed Windows Cabinet file</param>
        /// <param name="target">PE file to embed cab with</param>
        private static void EmbedCab(string cabFile, string target)
        {
            // load existing resources from original exe into memory
            var r = new CabResource("#6699", cabFile);
            r.Save(target);
        }
    }
}