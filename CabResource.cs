// --------------------------------------------------------------------------------
// <copyright file="CabResource.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
// --------------------------------------------------------------------------------

namespace stuffer
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using Microsoft.Deployment.Resources;

    /// <summary>
    /// Implementation of a Microsoft Cabinet resource
    /// </summary>
    internal class CabResource : Resource
    {
        /// <summary>
        /// Defines a new ResourceType for Microsoft Cabinets
        /// </summary>
        private static ResourceType Cab
        {
            get
            {
                return "CABFILE";
            }
        }

        private string Source { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Resource ID to use</param>
        /// <param name="source">Source file to read resource data from</param>
        public CabResource(string name, string source)
            : base(Cab, name, 0)
        {
            this.Source = source;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Resource ID to use</param>
        /// <param name="data">Byte array containing the Microsoft Cabinet</param>
        public CabResource(string name, ref byte[] data)
            : base(Cab, name, 0, data)
        {
            // nothing extra
        }

        /// <summary>
        /// Saves the resource to a file.  Any existing resource data with matching type, name, and locale is overwritten.
        /// </summary>
        /// <param name="file">Win32 PE file to contain the resource</param>
        [SecurityPermission(SecurityAction.Assert, UnmanagedCode = true)]
        public new void Save(string file)
        {
            new FileIOPermission(FileIOPermissionAccess.AllAccess, file).Demand();

            Stream source = null;
            var handle = IntPtr.Zero;
            var ptr = IntPtr.Zero;
            try
            {
                Console.WriteLine("Beginning to update resources...");
                handle = NativeMethods.BeginUpdateResource(file, false);

                int length;
                if (this.Data != null)
                {
                    // using byte array as source
                    source = new MemoryStream(this.Data);
                    length = this.Data.Length;
                    Console.WriteLine("Copying {0:N1}kB...", length / 1024f);
                }
                else if (!string.IsNullOrEmpty(this.Source))
                {
                    // using another file as source
                    source = File.OpenRead(this.Source);
                    length = (int)source.Length;
                    Console.WriteLine("Copying {0:N1}kB from {1}...", length / 1024f, Path.GetFullPath(this.Source));
                }
                else
                {
                    throw new ArgumentException("No data source available?");
                }

                // allocate memory
                ptr = Marshal.AllocHGlobal(length);
                unsafe
                {
                    // do memcpy from source stream to allocated memory
                    var memPtr = (byte*)ptr.ToPointer();
                    using (var stream = new UnmanagedMemoryStream(memPtr, length, length, FileAccess.Write))
                    {
                        source.CopyTo(stream);
                    }
                }

                bool ret;
                Console.WriteLine("Updating resources...");
                if (this.Name.StartsWith("#", StringComparison.Ordinal))
                {
                    // A numeric-named resource must be saved via the integer version of UpdateResource.
                    var name = (IntPtr)Int32.Parse(this.Name.Substring(1), CultureInfo.InvariantCulture);
                    ret = NativeMethods.UpdateResource(
                        handle, (string)this.ResourceType, name, (ushort)this.Locale, ptr, (uint)length);
                }
                else
                {
                    // string name
                    ret = NativeMethods.UpdateResource(
                        handle, (string)this.ResourceType, this.Name, (ushort)this.Locale, ptr, (uint)length);
                }

                // actually write changes to file
                Console.WriteLine("Finalizing update...");
                if (ret && NativeMethods.EndUpdateResource(handle, false))
                {
                    handle = IntPtr.Zero;
                    Console.WriteLine("Done.");
                }
                else
                {
                    throw new IOException("Failed to save resource. Error: " + Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                // cleanup
                if (source != null)
                {
                    source.Dispose();
                }

                if (handle != IntPtr.Zero)
                {
                    NativeMethods.EndUpdateResource(handle, true);
                }

                if (ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
    }
}