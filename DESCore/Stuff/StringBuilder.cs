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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		public static StringBuilder WriteFreshLine(this StringBuilder sb)
		{
			if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
				sb.AppendLine();

			return sb;
		} // func WriteFreshLine

		public static StringBuilder WriteSeperator(this StringBuilder sb, string sHeader = null)
		{
			sb.WriteFreshLine();
			if (sHeader == null)
				sb.AppendLine(new string('-', 80));
			else
			{
				sb.Append("-- ");
				sb.Append(sHeader);
				int iRest = 76 - sHeader.Length;
				if (iRest > 0)
					sb.Append(' ').AppendLine(new string('-', iRest));
			}
			return sb;
		} // proc WriteSeperator

		public static StringBuilder WriteException(this StringBuilder sb, Exception e)
		{
			ExceptionFormatter.FormatPlainText(sb, e);
			return sb;
		} // func WriteException
	} // class Procs
}
