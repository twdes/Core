﻿#region -- copyright --
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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Networking
{
	public class UserCredential : ICredentials
	{
		#region -- class CredentialWrapper ----------------------------------------------

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

		public UserCredential(string authType, string domain, string userName, string password)
		{
			this.authType = authType;
			this.domain = domain;
			this.userName = userName;
			this.password = password;
		} // ctor

		public NetworkCredential GetCredential(Uri uri, string authType)
		{
			if (String.Compare(authType, this.authType, StringComparison.OrdinalIgnoreCase) == 0)
				return new NetworkCredential(userName, password, domain);
			else
				return null;
		} // func GetCredential

		public string AuthType => authType;
		public string Domain => domain;
		public string UserName => userName;
		public string Password => password;

		public static ICredentials Wrap(NetworkCredential userInfo)
			=> new CredentialWrapper(userInfo);
	} // class UserCredential
}