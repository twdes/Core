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
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	public static partial class Procs
	{
		public static string EscapeSpecialChars(this string value)
			=> Regex.Replace(value, @"\r\n?|\n|\t", m => m.Value == "\t" ? "\\t" : "\\n");

		public static string UnescapeSpecialChars(this string value)
			=> Regex.Replace(value, @"\\n|\\t", m => m.Value == @"\t" ? "\t" : "\n");
	} // class Procs
}
