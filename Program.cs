// --------------------------------------------------------------------------------
// <copyright file="Program.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
// --------------------------------------------------------------------------------

namespace stuffer
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Xml.Serialization;

    using Microsoft.Deployment.Resources;

    /// <summary>
    /// Main program
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main program entry
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        /// <returns>0 if successful, 1 for errors</returns>
        public static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: stuffer <directory> <output EXE>");
                return 1;
            }

            // backup the target exe to a temporary path
            var original = Path.GetFullPath(args[1]);
            var target = Path.Combine(Directory.GetParent(original).FullName, "test.exe");
            File.Copy(original, target, true);

            // load existing resources from original exe into memory
            var rc = new ResourceCollection();
            rc.Find(original);
            rc.Load(original);

            // if argument 1 was a .cab file instead of a directory
            if (File.Exists(args[0]))
            {
                var extension = Path.GetExtension(args[0]);
                Debug.Assert(extension != null);
                if (extension.ToLower() != ".cab")
                {
                    Console.WriteLine("Only cabs are supported: {0}", args[0]);
                    return 1;
                }

                EmbedCab(args[0], target);
            }
            else if (!Directory.Exists(args[0]))
            {
                Console.WriteLine("{0} must be directory or .cab file!", args[0]);
                return 1;
            }

            //////////////////////////////////

            // generate list of all files in directory tree
            var root = Path.GetFullPath(args[0]).TrimEnd(new[] { '\\' });
            var query = from file in Directory.EnumerateFiles(args[0], "*.*", SearchOption.AllDirectories)
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

            long compressed = 0;
            long total = 0;
            foreach (var file in fileList)
            {
                using (var filestream = File.OpenRead(Path.Combine(root, file)))
                using (var mem = new MemoryStream())
                {
                    // read and compress files
                    using (var gzip = new GZipStream(mem, CompressionMode.Compress, true))
                    {
                        filestream.CopyTo(gzip);
                    }

                    total += filestream.Length;
                    compressed += mem.Length;
                    Console.WriteLine(
                        "Compressed {0} - {1:N1}kB to {2:N1}kB.",
                        file,
                        filestream.Length / 1024f,
                        mem.Length / 1024f);
                    var data = mem.GetBuffer();
                    rc.Add(new CabResource(file, ref data));
                }
            }

            Console.WriteLine(
                "Total: {0:N2}MB Compressed: {1:N2}MB. Saving...",
                total / 1024f / 1024f,
                compressed / 1024f / 1024f);
            rc.Save(target);
            return 0;
        }

        /// <summary>
        /// Deprecated feature to embed cabinets in bootstrapper
        /// </summary>
        /// <param name="cabFile">Compressed Windows Cabinet file</param>
        /// <param name="target">PE file to embed cab with</param>
        private static void EmbedCab(string cabFile, string target)
        {
            // read to bytes
            byte[] cabData = File.ReadAllBytes(Path.GetFullPath(cabFile));

            // save resources back to exe
            var r = new CabResource("6699", ref cabData);
            r.Save(target);
        }
    }
}