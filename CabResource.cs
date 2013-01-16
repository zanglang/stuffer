// --------------------------------------------------------------------------------
// <copyright file="Program.cs" company="muvee Technologies Pte Ltd">
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
        #region Kernel32 interops

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateResource(
            IntPtr updateHandle, string type, IntPtr name, ushort lcid, IntPtr data, uint dataSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr BeginUpdateResource(
            string fileName, [MarshalAs(UnmanagedType.Bool)] bool deleteExistingResources);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EndUpdateResource(
            IntPtr updateHandle, [MarshalAs(UnmanagedType.Bool)] bool discardChanges);

        #endregion

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
        /// Writes the cabinet resource to a PE binary
        /// </summary>
        /// <param name="file">Filesystem path to write to</param>
        public new void Save(string file)
        {
            // require permissions to use file IO
            new FileIOPermission(FileIOPermissionAccess.AllAccess, file).Demand();

            var updateHandle = IntPtr.Zero;
            try
            {
                // get a handle to begin
                updateHandle = BeginUpdateResource(file, false);
                this.Save(updateHandle);
                if (!EndUpdateResource(updateHandle, false))
                {
                    // Win32 API error
                    int err = Marshal.GetLastWin32Error();
                    throw new IOException(
                        string.Format(CultureInfo.InvariantCulture, "Failed to save resource. Error code: {0}", err));
                }

                updateHandle = IntPtr.Zero;
            }
            finally
            {
                // free the handle
                if (updateHandle != IntPtr.Zero)
                {
                    EndUpdateResource(updateHandle, true);
                }
            }
        }

        /// <summary>
        /// Write byte data to a resource handle
        /// </summary>
        /// <param name="updateHandle">Handle obtained from BeginUpdateResource</param>
        private void Save(IntPtr updateHandle)
        {
            var dataPtr = IntPtr.Zero;
            try
            {
                int dataLength = 0;
                if (this.Data != null)
                {
                    //dataLength = this.Data.LongLength;
                    //dataPtr = Marshal.AllocHGlobal(new IntPtr(dataLength));
                    dataLength = this.Data.Length;
                    dataPtr = Marshal.AllocHGlobal(dataLength);
                    Marshal.Copy(this.Data, 0, dataPtr, dataLength);
                }

                // initiate updating
                if (!UpdateResource(
                        updateHandle,
                        this.ResourceType.ToString(),
                        (IntPtr)6699,
                        (ushort)this.Locale,
                        dataPtr,
                        (uint)dataLength))
                {
                    throw new IOException("Failed to save resource. Error: " + Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                if (dataPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(dataPtr);
                }
            }
        }
    }
}