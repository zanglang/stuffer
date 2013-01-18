// --------------------------------------------------------------------------------
// <copyright file="Program.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
// --------------------------------------------------------------------------------

namespace stuffer
{
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
    }
}