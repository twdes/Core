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

namespace TecWare.DE.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Defines the quotation algorithmus.</summary>
	public enum CsvQuotation
	{
		/// <summary>No quotation allowed.</summary>
		None,
		/// <summary>Normal quotation if needed.</summary>
		Normal,
		/// <summary>Quote all text-columns.</summary>
		ForceText,
		/// <summary>Quote all columns.</summary>
		Forced
	} // enum CsvQuotation

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class TextCoreSettings
	{
		/// <summary>Leave Stream Open</summary>
		public bool LeaveStreamOpen { get; set; } = false;
		/// <summary>Row of the header information.</summary>
		public int HeaderRow { get; set; } = 0;
		/// <summary>First data row.</summary>
		public int StartRow { get; set; } = 1;
	} // class TextSCoreSettings

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class TextFixedSettings : TextCoreSettings
	{
		/// <summary></summary>
		public int[] Lengths { get; set; } = null;
		/// <summary>Padding</summary>
		public char Padding { get; set; } = ' ';
	} // class TextFixedSettings

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Description of the text file.</summary>
	public sealed class TextCsvSettings : TextCoreSettings
	{
		/// <summary>Delemitter for the columns (default: ';').</summary>
		public char Delemiter { get; set; } = ';';
		/// <summary>Quotation type</summary>
		public CsvQuotation Quotation { get; } = CsvQuotation.Normal;
		/// <summary>Quotation char (default: '"').</summary>
		public char Quote { get; set; } = '"';
	} // class TextCsvSettings
}
