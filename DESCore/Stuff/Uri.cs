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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TecWare.DE.Stuff
{
	public static partial class Procs
	{
		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static IEnumerable<string> ParseMultiValueHeader(string value)
			=> from c in value.Split(',')
				let t = c.Trim()
				where t.Length > 0
				select t;

		/// <summary>Get filename from uri path.</summary>
		/// <param name="uri"></param>
		/// <returns></returns>
		public static string GetFileName(this Uri uri)
			=> Path.GetFileName(uri.AbsolutePath);

		/// <summary>Filter unwanted chars from status description.</summary>
		/// <param name="statusDescription"></param>
		/// <returns></returns>
		public static string FilterHttpStatusDescription(string statusDescription)
		{
			if (String.IsNullOrEmpty(statusDescription))
				return statusDescription;

			var sb = new StringBuilder(statusDescription.Length);
			for (var i = 0; i < statusDescription.Length; i++)
			{
				var c = statusDescription[i];
				if (c == '\n')
					sb.Append("<br/>");
				else if (c > (char)0x1F || c == '\t')
					sb.Append(c);
			}
			return sb.ToString();
		} // func FilterHttpStatusDescription
	} // class Procs
}
