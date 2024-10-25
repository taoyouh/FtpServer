// <copyright file="HybridAuthenticator.cs" company="Zhaoquan Huang">
// Copyright (c) Zhaoquan Huang. All rights reserved
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Zhaobang.FtpServer.Authenticate
{
    /// <summary>
    /// The authenticator that accepts both userName/password and anonymous.
    /// </summary>
    public class HybridAuthenticator : IAuthenticator
    {
        private readonly List<FtpUser> users;
        private readonly bool enableAnonymous;

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAuthenticator"/> class.
        /// </summary>
        /// <param name="users">The users to accept.</param>
        /// <param name="enableAnonymous">Enable anonymous mode or not.</param>
        public HybridAuthenticator(List<FtpUser> users, bool enableAnonymous)
        {
            this.users = users;
            this.enableAnonymous = enableAnonymous;
        }

        /// <summary>
        /// Verifies if the username-password pair is correct.
        /// </summary>
        /// <param name="userName">The user name user inputted.</param>
        /// <param name="password">The password user inputted.</param>
        /// <returns>Whether the pair is correct.</returns>
        public bool Authenticate(string userName, string password)
        {
            if (this.enableAnonymous && userName.ToUpper() == "ANONYMOUS") return true;
            return users.Any(u => u.Name.ToUpper() == userName.ToUpper() && u.Password.ToUpper() == password.ToUpper());
        }
    }
}
