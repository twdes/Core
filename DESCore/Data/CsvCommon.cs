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
	public abstract class TextSCoreSettings
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
	public sealed class TextFixedSettings : TextSCoreSettings
	{
		/// <summary></summary>
		public int[] Lengths { get; set; } = null;
		/// <summary>Padding</summary>
		public char Padding { get; set; } = ' ';
	} // class TextFixedSettings

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Description of the text file.</summary>
	public sealed class TextCsvSettings : TextSCoreSettings
	{
		/// <summary>Delemitter for the columns (default: ';').</summary>
		public char Delemiter { get; set; } = ';';
		/// <summary>Quotation type</summary>
		public CsvQuotation Quotation { get; } = CsvQuotation.Normal;
		/// <summary>Quotation char (default: '"').</summary>
		public char Quote { get; set; } = '"';
	} // class TextCsvSettings
}
