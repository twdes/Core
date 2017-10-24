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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	public static partial class Procs
	{
		public static string EscapeSpecialChars(this string value)
			=> Regex.Replace(value, @"\r\n?|\n|\t", m => m.Value == "\t" ? "\\t" : "\\n");

		public static string UnescapeSpecialChars(this string value)
			=> Regex.Replace(value, @"\\n|\\t", m => m.Value == @"\t" ? "\t" : "\n");

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

		#region -- Filter -----------------------------------------------------------------

		private static bool StaticTrueFilter(string value)
			=> true;

		private static bool StaticFalseFilter(string value)
			=> false;

		private static Func<string, bool> GetFilterFunctionEx(string filterExpression)
		{
			throw new NotImplementedException();
		} // func GetFilterFunctionEx
		
		public static Func<string, bool> GetFilerFunction(string filterExpression, bool? defaultFilter = null)
		{
			if (String.IsNullOrEmpty(filterExpression))
				return  defaultFilter.HasValue 
					?  (defaultFilter.Value ? new Func<string, bool>(StaticTrueFilter) : new Func<string, bool>(StaticFalseFilter))
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
				else
					return GetFilterFunctionEx(filterExpression);
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
			=> GetFilerFunction(filterExpression, true)(value);

		#endregion
	} // class Procs
}
