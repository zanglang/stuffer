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

    using Microsoft.Deployment.Compression;
    using Microsoft.Deployment.Compression.Cab;
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
            var target = Path.GetFullPath(args[1]);
            var target2 = Path.Combine(Directory.GetParent(target).FullName, "test.exe");
            File.Copy(target, target2, true);

            // if argument 1 was a .cab file instead of a directory
            string cabFile;
            if (File.Exists(args[0]))
            {
                var extension = Path.GetExtension(args[0]);
                Debug.Assert(extension != null);
                if (extension.ToLower() != ".cab")
                {
                    Console.WriteLine("Only cabs are supported: {0}", args[0]);
                    return 1;
                }

                cabFile = args[0];
            }
            else if (Directory.Exists(args[0]))
            {
                Console.WriteLine("Packaging cab... ");
                cabFile = Path.GetFullPath("test.cab");

                // compress directory into a cabinet
                var cab = new CabInfo(cabFile);
                cab.Pack(
                    args[0], 
                    true, 
                    CompressionLevel.Max, 
                    (source, e) =>
                    {
                        if (e.ProgressType == ArchiveProgressType.StartFile)
                        {
                            Console.WriteLine("    {0}", e.CurrentFileName);
                        }
                    });
            }
            else
            {
                Console.WriteLine("{0} must be directory or .cab file!", args[0]);
                return 1;
            }

            // read to bytes
            byte[] cabData = File.ReadAllBytes(Path.GetFullPath(cabFile));

            // load resources from exe
            var rc = new ResourceCollection();
            rc.Find(target);
            rc.Load(target);

            // save resources back to exe
            var r = new CabResource("6699", ref cabData);
            r.Save(target2);
            return 0;
        }
    }
}