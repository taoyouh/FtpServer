using System;
using System.Collections.Generic;
using System.Text;

namespace Zhaobang.FtpServer.Authenticate
{
    /// <summary>
    /// The authenticator that accepts a single pair of user name and password
    /// </summary>
    public class SimpleAuthenticator : IAuthenticator
    {
        string userName;
        string password;

        /// <summary>
        /// Initiates an instance of <see cref="SimpleAuthenticator"/>
        /// </summary>
        /// <param name="userName">The user name to accept</param>
        /// <param name="password">The password to accept</param>
        public SimpleAuthenticator(string userName, string password)
        {
            this.userName = userName;
            this.password = password;
        }

        /// <summary>
        /// Verifies if the username-password pair is correct
        /// </summary>
        /// <param name="userName">The user name user inputted</param>
        /// <param name="password">The password user inputted</param>
        /// <returns>Whether the pair is correct</returns>
        public bool Authenticate(string userName, string password)
        {
            return (this.userName == userName && this.password == password);
        }
    }
}
