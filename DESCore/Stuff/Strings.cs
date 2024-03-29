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
using System.Text;
using System.Text.RegularExpressions;

namespace TecWare.DE.Stuff
{
	public static partial class Procs
	{
		#region -- EscapeSpecialChars -------------------------------------------------

		/// <summary>Escape special chars</summary>
		/// <param name="sb"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static StringBuilder EscapeSpecialChars(this StringBuilder sb, string value)
		{
			if (value != null)
			{
				foreach (var c in value)
				{
					switch (c)
					{
						case '\n':
							sb.Append("\\n");
							break;
						case '\r':
							break;
						case '\t':
							sb.Append("\\t");
							break;
						case '\\':
							sb.Append(@"\\");
							break;
						case '\0':
							sb.Append("\\0");
							break;
						default:
							sb.Append(c);
							break;
					}
				}
			}
			return sb;
		} // func EscapeSpecialChars

		/// <summary>Escape special chars</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string EscapeSpecialChars(this string value)
		{
			if (String.IsNullOrEmpty(value))
				return value;

			return EscapeSpecialChars(new StringBuilder(value.Length + 32), value).ToString();
		} // func EscapeSpecialChars

		/// <summary>unbespecial chars</summary>
		/// /// <param name="sb"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static StringBuilder UnescapeSpecialChars(this StringBuilder sb, string value)
		{
			if (value != null)
			{
				var isEscape = false;
				foreach (var c in value)
				{
					if (isEscape)
					{
						switch (c)
						{
							case 'n':
								sb.Append('\n');
								break;
							case 't':
								sb.Append('\t');
								break;
							case '\\':
								sb.Append('\\');
								break;
							case '0':
								sb.Append('\0');
								break;
							default:
								sb.Append('\\').Append(c);
								break;
						}
						isEscape = false;
					}
					else if (c == '\\')
						isEscape = true;
					else
						sb.Append(c);
				}
			}

			return sb;
		} // func UnescapeSpecialChars

		/// <summary>Escape special chars</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string UnescapeSpecialChars(this string value)
		{
			if (String.IsNullOrEmpty(value))
				return value;

			return UnescapeSpecialChars(new StringBuilder(value.Length), value).ToString();
		} // func EscapeSpecialChars

		#endregion

		/// <summary></summary>
		/// <param name="language"></param>
		/// <param name="languagePart"></param>
		/// <param name="countryPart"></param>
		/// <returns></returns>
		public static bool TrySplitLanguage(string language, out string languagePart, out string countryPart)
		{
			if (String.IsNullOrEmpty(language))
				goto failed;

			var p = language.IndexOf('-');
			if (p == -1 && language.Length == 2)
			{
				languagePart = language;
				countryPart = null;
				return true;
			}
			else if (p == 2 && language.Length == 5)
			{
				languagePart = language.Substring(0, 2);
				countryPart = language.Substring(3, 2);
				return true;
			}

			failed:
			languagePart = null;
			countryPart = null;
			return false;
		} // func SplitLanguage

		#region -- Filter -------------------------------------------------------------

		private static bool StaticTrueFilter(string value)
			=> true;

		private static bool StaticFalseFilter(string value)
			=> false;

		private static Predicate<string> GetFilterFunctionEx(string filterExpression)
		{
			var r = new StringBuilder();

			for(var i = 0;i<filterExpression.Length;i++)
			{
				var c = filterExpression[i];
				switch (c)
				{
					case '?':
					case '(':
					case ')':
					case '[':
					case ']':
					case '<':
					case '>':
					case '.':
					case '+':
					case '$':
					case '^':
					case '#':
					case '\\':
						if (i == 0)
							r.Append('^');

						r.Append('\\').Append(c);
						break;
					case '*':
						r.Append(".*");
						break;
					default:
						if (i == 0)
							r.Append('^');

						r.Append(c);
						break;
				}
			}

			if (r.Length <= 2 || r[r.Length - 2] != '.' || r[r.Length - 1] != '*')
				r.Append('$');

			var regEx = new Regex(r.ToString(), RegexOptions.Singleline | RegexOptions.IgnoreCase);
			return value => regEx.Match(value).Success;
		} // func GetFilterFunctionEx
		
		/// <summary>Create for a simple star filter, a predicate</summary>
		/// <param name="filterExpression"></param>
		/// <param name="defaultFilter"></param>
		/// <returns></returns>
		public static Predicate<string> GetFilterFunction(string filterExpression, bool? defaultFilter = null)
		{
			if (String.IsNullOrEmpty(filterExpression))
				return  defaultFilter.HasValue 
					?  (defaultFilter.Value ? new Predicate<string>(StaticTrueFilter) : new Predicate<string>(StaticFalseFilter))
					: null;

			var p1 = filterExpression.IndexOf('*');
			var p2 = filterExpression.LastIndexOf('*');
			if (p1 == p2) // only one start
			{
				if (p1 == -1) // compare
					return value => String.Compare(value, filterExpression, StringComparison.OrdinalIgnoreCase) == 0;
				else if (p1 == 0) // => endswith
				{
					if (filterExpression.Length == 1)
						return StaticTrueFilter;
					else
					{
						var testValue = filterExpression.Substring(1);
						return value => value.EndsWith(testValue, StringComparison.OrdinalIgnoreCase);
					}
				}
				else if (p1 == filterExpression.Length - 1)// => startwith
				{
					var testValue = filterExpression.Substring(0, p1);
					return value => value.StartsWith(testValue, StringComparison.OrdinalIgnoreCase);
				}
				else // startswith, endswith
				{
					var testValue1 = filterExpression.Substring(0, p1);
					var testValue2 = filterExpression.Substring(p1 + 1);
					return value => value.StartsWith(testValue1, StringComparison.OrdinalIgnoreCase) && value.EndsWith(testValue2, StringComparison.OrdinalIgnoreCase);
				}
			}
			else
			{
				var p3 = filterExpression.IndexOf('*', p1 + 1);
				if (p3 == p2) // two stars
				{
					if (p1 == 0 && p2 == filterExpression.Length - 1) // => contains
					{
						var testValue = filterExpression.Substring(1, p2 - 1);
						return value => value.IndexOf(testValue, StringComparison.OrdinalIgnoreCase) >= 0;
					}
					else
						return GetFilterFunctionEx(filterExpression);
				}
				else
					return GetFilterFunctionEx(filterExpression);
			}
		} // func GetFilterFunction

		/// <summary>Simple "Star"-Filter rule, for single-strings</summary>
		/// <param name="value"></param>
		/// <param name="filterExpression"></param>
		/// <returns></returns>
		public static bool IsFilterEqual(string value, string filterExpression)
			=> GetFilterFunction(filterExpression, true)(value);

		#endregion

		#region -- SplitNewLines ------------------------------------------------------

		/// <summary>Split a string to new lines.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static IEnumerable<string> SplitNewLines(this string value)
		{
			foreach (var (startAt, len) in SplitNewLinesTokens(value))
				yield return value.Substring(startAt, len);
		} // func SplitNewLines

		/// <summary>Split a string to new lines.</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static IEnumerable<(int startAt, int len)> SplitNewLinesTokens(this string value)
		{
			var startAt = 0;
			var state = 0;

			var i = 0;
			var l = value?.Length ?? 0;
			while (i < l)
			{
				var c = value[i];
				switch (state)
				{
					case 0:
						if (c == '\n')
							state = 1;
						else if (c == '\r')
							state = 2;
						break;
					case 1:
						yield return (startAt, i - startAt - 1);
						state = 0;
						if (c == '\r') // \n\r
						{
							startAt = i + 1;
							break;
						}
						else
						{
							startAt = i;
							goto case 0;
						}
					case 2:
						yield return (startAt, i - startAt - 1);
						state = 0;
						if (c == '\n') // \r\n
						{
							startAt = i + 1;
							break;
						}
						else
						{
							startAt = i;
							goto case 0;
						}
				}
				i++;
			}

			if (state > 0) // ends with newline
				yield return (startAt, l - startAt - 1);
			else if (startAt < l)
				yield return (startAt, l - startAt);
		} // func SplitNewLinesTokens

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string GetFirstLine(this string value)
		{
			var (startAt, len) = SplitNewLinesTokens(value).FirstOrDefault(c => c.len > 0);
			return len > 0 ? value.Substring(startAt, len) : String.Empty;
		} // func GetFirstLine

		#endregion
	} // class Procs
}
