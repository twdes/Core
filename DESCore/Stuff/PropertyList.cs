using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DES.Stuff
{
	public partial class Procs
	{
		#region -- PropertyList -----------------------------------------------------------

		private static string ReadStr(string cur, char terminator, ref int index)
		{
			StringBuilder sb = new StringBuilder();
			bool lInQuote = false;
			while (index < cur.Length && (lInQuote || cur[index] != terminator))
			{
				char cCur = cur[index];
				if (cCur == '"')
					if (lInQuote)
					{
						cCur = cur[++index];
						if (cCur == terminator)
							break;
						else if (cCur == '"')
							sb.Append('"');
						else
						{
							sb.Append(cCur);
							lInQuote = false;
						}
					}
					else
						lInQuote = true;
				else
					sb.Append(cCur);
				index++;
			}
			index++;
			return sb.ToString();
		} // func ReadStr

		/// <overloads>Splits a list of properties.</overloads>
		/// <summary>Splits a list of properties with the format <c>Param1=Value1;Param2=Value2</c>.</summary>
		/// <param name="items"></param>
		/// <returns></returns>
		public static IEnumerable<KeyValuePair<string, string>> SplitPropertyList(string items)
		{
			return SplitPropertyList(items, ';');
		} // func SplitPropertyList

		/// <summary></summary>
		/// <param name="items"></param>
		/// <param name="sepChar"></param>
		/// <returns></returns>
		public static IEnumerable<KeyValuePair<string, string>> SplitPropertyList(string items, char sepChar)
		{
			var index = 0;
			if (items != null)
			{
				items = items.Replace("\\n", Environment.NewLine);
				while (index < items.Length)
					yield return new KeyValuePair<string, string>(ReadStr(items, '=', ref index), ReadStr(items, sepChar, ref index));
			}
		} // func SplitPropertyList

		private static string GetValue(string value)
		{
			value = value.Replace("\n", "\\n").Replace("\r", "");
			if (value.IndexOf('"') >= 0)
				value = '"' + value.Replace("\"", "\"\"") + '"';
			return value;
		} // func GetValue

		/// <summary></summary>
		/// <param name="items"></param>
		/// <returns></returns>
		public static string JoinPropertyList(IEnumerable<KeyValuePair<string, string>> items)
		{
			StringBuilder sb = new StringBuilder();
			foreach (KeyValuePair<string, string> oCur in items)
				sb.Append(GetValue(oCur.Key)).Append('=').Append(GetValue(oCur.Value)).Append(';');
			return sb.ToString();
		} // func JoinPropertyList

		#endregion
	}
}
