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
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- enum CsvQuotation ------------------------------------------------------

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

	#endregion

	#region -- class TextCoreSettings -------------------------------------------------

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

	#endregion

	#region -- class TextFixedSettings ------------------------------------------------

	/// <summary></summary>
	public sealed class TextFixedSettings : TextCoreSettings
	{
		/// <summary></summary>
		public int[] Lengths { get; set; } = null;
		/// <summary>Padding</summary>
		public char Padding { get; set; } = ' ';

		internal int CreateOffsets(out int[] offsets)
		{
			if (Lengths == null || Lengths.Length == 0)
				throw new ArgumentNullException(nameof(Lengths));

			offsets = new int[Lengths.Length];

			var ofs = 0;
			for (var i = 0; i < Lengths.Length; i++)
			{
				offsets[i] = ofs;
				ofs += Lengths[i];
			}

			return ofs;
		} // func CreateOffsets
	} // class TextFixedSettings

	#endregion

	#region -- class TextCsvSettings --------------------------------------------------

	/// <summary>Description of the text file.</summary>
	public sealed class TextCsvSettings : TextCoreSettings
	{
		/// <summary>Delemitter for the columns (default: ';').</summary>
		public char Delemiter { get; set; } = ';';
		/// <summary>Quotation type</summary>
		public CsvQuotation Quotation { get; set; } = CsvQuotation.Normal;
		/// <summary>Quotation char (default: '"').</summary>
		public char Quote { get; set; } = '"';
	} // class TextCsvSettings

	#endregion

	#region -- class TextDataRowColumn ------------------------------------------------

	/// <summary>Column description for csv import/export.</summary>
	[Obsolete("Is only used in some lua extensions. Do not use in compiled languages.")]
	public sealed class TextDataRowColumn : IDataColumn, IDataConverterColumn
	{
		/// <summary></summary>
		public TextDataRowColumn()
		{
		} // ctor

		IStringConverter IDataConverterColumn.Converter => SimpleValueConverter.Create(Converter, null);

		/// <summary>Gets the name in this column.</summary>
		public string Name { get; set; }
		/// <summary>Gets the type of data in this column.</summary>
		public Type DataType { get; set; }
		/// <summary>Extented attributes for the column.</summary>
		public IPropertyEnumerableDictionary Attributes => PropertyDictionary.EmptyReadOnly;

		/// <summary>Optional, special provider for the format of the value.</summary>
		public IFormatProvider FormatProvider { get; set; } = null;
		/// <summary>Optional. Convert Type.</summary>
		public Func<string, object> Converter { get; set; } = null;
	} // class TextDataRowColumn

	#endregion

	#region -- class TextDataRowWriterColumn ------------------------------------------

	/// <summary>Column description</summary>
	[Obsolete("Is only used in some lua extensions. Do not use in compiled languages.")]
	public sealed class TextDataRowWriterColumn : IDataColumn, IDataConverterColumn
	{
		IStringConverter IDataConverterColumn.Converter => SimpleValueConverter.Create(null, Converter);
		Type IDataColumn.DataType => typeof(object);
		IPropertyEnumerableDictionary IDataColumn.Attributes => PropertyDictionary.EmptyReadOnly;

		/// <summary>Name of the column</summary>
		public string Name { get; set; }
		/// <summary>Optional special provider for the format of the value.</summary>
		public IFormatProvider FormatProvider { get; set; }
		/// <summary>Convert Type.</summary>
		public Func<object, string> Converter { get; set; }
	} // class TextDataRowWriterColumn

	#endregion
}
