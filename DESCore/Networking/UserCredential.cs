#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Net;

namespace TecWare.DE.Networking
{
	/// <summary>Credential implementation to support different authentfication types</summary>
	public class UserCredential : ICredentials
	{
		#region -- class CredentialWrapper --------------------------------------------

		private sealed class CredentialWrapper : ICredentials
		{
			private readonly NetworkCredential userInfo;

			public CredentialWrapper(NetworkCredential userInfo)
			{
				this.userInfo = userInfo;
			} // ctor

			public NetworkCredential GetCredential(Uri uri, string authType)
			{
				if (userInfo == CredentialCache.DefaultCredentials
					|| userInfo == CredentialCache.DefaultNetworkCredentials)
					return userInfo;
				else if (String.IsNullOrEmpty(userInfo.Domain))
				{
					// force basic, if we have no domain
					return String.Compare(authType, "basic", StringComparison.OrdinalIgnoreCase) == 0 ? userInfo : null;
				}
				else
					return userInfo;
			} // func GetCredential
		} // class CredentialWrapper

		#endregion

		private readonly string authType;
		private readonly string domain;
		private readonly string userName;
		private readonly string password;

		/// <summary></summary>
		/// <param name="authType"></param>
		/// <param name="domain"></param>
		/// <param name="userName"></param>
		/// <param name="password"></param>
		public UserCredential(string authType, string domain, string userName, string password)
		{
			this.authType = authType;
			this.domain = domain;
			this.userName = userName;
			this.password = password;
		} // ctor

		/// <summary>GetCredential implementation, that compares the authentification type, too.</summary>
		/// <param name="uri"></param>
		/// <param name="authType"></param>
		/// <returns></returns>
		public NetworkCredential GetCredential(Uri uri, string authType)
		{
			if (String.Compare(authType, this.authType, StringComparison.OrdinalIgnoreCase) == 0)
				return new NetworkCredential(userName, password, domain);
			else
				return null;
		} // func GetCredential

		/// <summary>Authentification type</summary>
		public string AuthType => authType;
		/// <summary>Domain</summary>
		public string Domain => domain;
		/// <summary>User name</summary>
		public string UserName => userName;
		/// <summary>Password</summary>
		public string Password => password;

		/// <summary>Wrap network credentials to compare also the authentification type.</summary>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		public static ICredentials Wrap(NetworkCredential userInfo)
			=> new CredentialWrapper(userInfo);
	} // class UserCredential
}
