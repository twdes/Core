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
using System.Security;
using TecWare.DE.Stuff;

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
		private readonly SecureString password;

		/// <summary>Create a basic user credential.</summary>
		/// <param name="userName">User name</param>
		/// <param name="password">Password of the user.</param>
		public UserCredential(string userName, SecureString password)
			: this("Basic", null, userName, password)
		{
		} // ctor

		/// <summary>Create a ntlm user credential.</summary>
		/// <param name="domain"></param>
		/// <param name="userName">User name</param>
		/// <param name="password">Password of the user.</param>
		public UserCredential(string domain, string userName, SecureString password)
			: this("NTLM", domain, userName, password)
		{
		} // ctor

		/// <summary>Create a user credential with authentification type.</summary>
		/// <param name="authType"></param>
		/// <param name="domain"></param>
		/// <param name="userName">User name</param>
		/// <param name="password">Password of the user.</param>
		public UserCredential(string authType, string domain, string userName, SecureString password)
		{
			this.authType = authType ?? throw new ArgumentNullException(nameof(authType));
			this.domain = domain;
			this.userName = userName ?? String.Empty;
			this.password = password != null ? password.Copy() : new SecureString();
		} // ctor

		/// <summary>GetCredential implementation, that compares the authentification type, too.</summary>
		/// <param name="uri"></param>
		/// <param name="authType"></param>
		/// <returns></returns>
		public NetworkCredential GetCredential(Uri uri, string authType)
			=> String.Compare(authType, this.authType, StringComparison.OrdinalIgnoreCase) == 0
				? new NetworkCredential(userName, password, domain)
				: null;

		/// <summary>Authentification type</summary>
		public string AuthType => authType;
		/// <summary>Domain</summary>
		public string Domain => domain;
		/// <summary>User name</summary>
		public string UserName => userName;
		/// <summary>Password</summary>
		public SecureString Password => password;

		/// <summary>Wrap network credentials to compare also the authentification type.</summary>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		public static ICredentials Wrap(NetworkCredential userInfo)
			=> new CredentialWrapper(userInfo);

		/// <summary>Wrap network credentials to compare also the authentification type.</summary>
		/// <param name="userInfo"></param>
		/// <returns></returns>
		public static ICredentials Wrap(ICredentials userInfo)
		{
			switch (userInfo)
			{
				case NetworkCredential nc:
					return new CredentialWrapper(nc);
				case UserCredential uc:
					return uc;
				default:
					return userInfo;
			}
		} // func Wrap

		/// <summary>Create UserCredential from password and username.</summary>
		/// <param name="userName"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		public static UserCredential Create(string userName, string password)
		{
			using (var p = CreateSecureString(password))
				return Create(userName, p);
		} // func Create

#if NET47
		private static unsafe SecureString CreateSecureString(string password)
		{
			if (String.IsNullOrEmpty(password))
				return null;
			fixed (char* c = password)
				return new SecureString(c, password.Length);
		} // func CreateSecureString
#else
		private static SecureString CreateSecureString(string password)
		{
			if (String.IsNullOrEmpty(password))
				return null;

			var s = new SecureString();
			foreach (var c in password)
				s.AppendChar(c);
			s.MakeReadOnly();
			return s;
		} // func CreateSecureString
#endif

		/// <summary>Create UserCredential from password and username.</summary>
		/// <param name="userName"></param>
		/// <param name="password"></param>
		/// <returns></returns>
		public static UserCredential Create(string userName, SecureString password)
		{
			if (userName == null)
				throw new ArgumentNullException(nameof(userName));

			var p = userName.IndexOf('\\');
			if(p >= 0)
				return new UserCredential(userName.Substring(0, p), userName.Substring(p + 1), password);
			else
			{
				p = userName.IndexOf('@');
				if (p >= 0)
					return new UserCredential(userName.Substring(p + 1), userName.Substring(0, p), password);
				else
					return new UserCredential(userName, password);
			}
		} // func Create
	} // class UserCredential
}