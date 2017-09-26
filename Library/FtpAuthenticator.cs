// <copyright file="FtpAuthenticator.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Zhaobang.FtpServer
{
    /// <summary>
    /// Used to authenticate FTP login
    /// </summary>
    public class FtpAuthenticator
    {
        /// <summary>
        /// Verifies if the username-password pair is correct
        /// </summary>
        /// <param name="userName">The user name user inputted</param>
        /// <param name="password">The password user inputted</param>
        /// <returns>Whether the pair is correct</returns>
        public virtual bool Authenticate(string userName, string password)
        {
            return true;
        }
    }
}
