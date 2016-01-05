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
