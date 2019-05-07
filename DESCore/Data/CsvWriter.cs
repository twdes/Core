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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- interface ITextCoreWriter ----------------------------------------------

	/// <summary>Core interface to write a csv-file.</summary>
	public interface ITextCoreWriter : IDisposable
	{
		/// <summary>Write a value line</summary>
		/// <param name="values"></param>
		void WriteRow(IEnumerable<string> values);

		/// <summary>Base output stream.</summary>
		TextWriter BaseWriter { get; }
		/// <summary>CSV-configuration</summary>
		TextCoreSettings Settings { get; }
	} // interface ITextCoreWriter

	#endregion

	#region -- class TextCoreWriter ---------------------------------------------------

	/// <summary>Base text reader implementation.</summary>
	public abstract class TextCoreWriter<TTEXTSCORESETTINGS> : ITextCoreWriter
		where TTEXTSCORESETTINGS : TextCoreSettings
	{
		private readonly TextWriter sw;
		private readonly TTEXTSCORESETTINGS settings;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		protected TextCoreWriter(TextWriter sw, TTEXTSCORESETTINGS settings)
		{
			this.sw = sw ?? throw new ArgumentNullException(nameof(sw));
			this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
		} // ctor

		/// <summary></summary>
		public void Dispose()
			=> Dispose(true);

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (!settings.LeaveStreamOpen)
					sw.Dispose();
			}
		} // proc Dispose

		#endregion

		/// <summary></summary>
		/// <param name="values"></param>
		public abstract void WriteRow(IEnumerable<string> values);

		/// <summary>Returns the current output stream.</summary>
		public TextWriter BaseWriter => sw;
		/// <summary>Settings for the file.</summary>
		public TTEXTSCORESETTINGS Settings => settings;

		TextCoreSettings ITextCoreWriter.Settings => settings;
	} // interface ITextCoreReader

	#endregion

	#region -- class TextFixedWriter --------------------------------------------------

	/// <summary>Write a fixed format file.</summary>
	public sealed class TextFixedWriter : TextCoreWriter<TextFixedSettings>
	{
		private readonly int[] lineOffsets;
		private readonly char[] lineBuffer;

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		public TextFixedWriter(TextWriter sw, TextFixedSettings settings) 
			: base(sw, settings)
		{
			lineBuffer = new char[settings.CreateOffsets(out lineOffsets)];
		} // ctor

		private void WriteRowValue(int index, string v)
		{
			// start end of value
			var ofs = lineOffsets[index];
			var endAt = ofs + Settings.Lengths[index];

			// fil in value
			if (!String.IsNullOrEmpty(v))
			{
				for (var i = 0; i < v.Length; i++)
				{
					if (ofs >= endAt)
						break;

					lineBuffer[ofs++] = v[i];
				}
			}

			// padding
			while (ofs < endAt)
				lineBuffer[ofs++] = Settings.Padding;
		} // proc WriteRowValue

		/// <summary></summary>
		/// <param name="values"></param>
		public override void WriteRow(IEnumerable<string> values)
		{
			using (var e = values.GetEnumerator())
			{
				for (var i = 0; i < Settings.Lengths.Length; i++)
					WriteRowValue(i, e.MoveNext() ? e.Current : null);
			}

			BaseWriter.WriteLine(lineBuffer);
		} // proc WriteRow
	} // class TextFixedWriter

	#endregion

	#region -- class TextCsvWriter ----------------------------------------------------

	/// <summary>Write a csv file.</summary>
	public sealed class TextCsvWriter : TextCoreWriter<TextCsvSettings>
	{
		private Func<string, bool?, bool> quoteValue;

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		public TextCsvWriter(TextWriter sw, TextCsvSettings settings) 
			: base(sw, settings)
		{
			switch (Settings.Quotation)
			{
				case CsvQuotation.Forced:
					quoteValue= new Func<string, bool?, bool>((v, isText) => true);
					break;
				case CsvQuotation.ForceText:
					quoteValue = new Func<string, bool?, bool>((v, isText) => isText.HasValue && isText.Value);
					break;
				case CsvQuotation.Normal:
					quoteValue = new Func<string, bool?, bool>((v, isText) => GetQuotedNormal(v, Settings.Quote));
					break;
				default:
					quoteValue = new Func<string, bool?, bool>((v, isText) => false);
					break;
			}


		} // ctor

		private static bool GetQuotedNormal(string v, char quote)
		{
			if (String.IsNullOrEmpty(v))
				return false;

			for (var i = 0; i < v.Length; i++)
			{
				if (v[i] == quote || Char.IsControl(v[i]))
					return true;
			}

			return false;
		} // func GetQuotedNormal

		private void WriteRowValue(string v, bool? isText)
		{
			var quoted = quoteValue(v, isText);

			if (!quoted)
			{
				BaseWriter.Write(v);
			}
			else if (v == null)
				BaseWriter.Write("\"\"");
			else
			{
				var len = v.Length;
				BaseWriter.Write("\"");
				for (var i = 0; i < len; i++)
				{
					if (v[i] == '"')
						BaseWriter.Write("\"\"");
					else
						BaseWriter.Write(v[i]);
				}
				BaseWriter.Write("\"");
			}
		} // proc WriteRowValue

		/// <summary></summary>
		/// <param name="values"></param>
		public override void WriteRow(IEnumerable<string> values)
			=> WriteRow(values, null);

		/// <summary></summary>
		/// <param name="values"></param>
		/// <param name="isText"></param>
		public void WriteRow(IEnumerable<string> values, bool[] isText = null)
		{
			var quote = Settings.Quote;
			var i = 0;
			foreach (var v in values)
			{
				WriteRowValue(v, isText == null || i >= isText.Length ? (bool?)null : isText[i]);
				BaseWriter.Write(quote);
				i++;
			}
			BaseWriter.WriteLine();
		} // proc WriteRow
	} // class TextFixedWriter

	#endregion

	#region -- class TextObjectWriter -------------------------------------------------

	/// <summary></summary>
	public abstract class TextObjectWriter : IDisposable
	{
		private readonly ITextCoreWriter coreWriter;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="coreWriter"></param>
		public TextObjectWriter(ITextCoreWriter coreWriter)
		{
			this.coreWriter = coreWriter ?? throw new ArgumentNullException(nameof(coreWriter));
		} // ctor

		/// <summary></summary>
		public void Dispose()
			=> Dispose(true);

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				coreWriter.Dispose();
		} // proc Dispose

		#endregion

		/// <summary>Core text writer</summary>
		public ITextCoreWriter CoreWriter => coreWriter;
	} // class TextObjectWriter

	#endregion

	#region -- class TextDataRowWriterColumn ------------------------------------------

	/// <summary>Column description</summary>
	public sealed class TextDataRowWriterColumn
	{
		/// <summary>Name of the column</summary>
		public string Name { get; set; }
		/// <summary>Optional special provider for the format of the value.</summary>
		public IFormatProvider FormatProvider { get; set; }
		/// <summary>Convert Type.</summary>
		public Func<object, string> Converter { get; set; }
	} // class TextDataRowWriterColumn

	#endregion

	#region -- class TextDataRowWriter ------------------------------------------------

	/// <summary></summary>
	public sealed class TextDataRowWriter : TextObjectWriter
	{
		//#region -- class TextDataRow --------------------------------------------------

		//private sealed class TextDataRow : DynamicDataRow
		//{
		//	private readonly TextDataRowEnumerator enumerator;

		//	public TextDataRow(TextDataRowEnumerator enumerator)
		//	{
		//		this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
		//	} // ctor

		//	private object GetValueIntern(int index)
		//	{
		//		// get value
		//		var value = enumerator.CoreReader[index];
		//		var column = enumerator.Columns?[index];
		//		if (column == null)
		//			return null; // no column defined

		//		// convert
		//		if (column.Converter != null)
		//			return column.Converter(value);
		//		else if (column.DataType == typeof(decimal))
		//			return Decimal.Parse(value, NumberStyles.Currency | NumberStyles.Float, column.FormatProvider);
		//		else if (column.DataType == typeof(double))
		//			return Double.Parse(value, NumberStyles.Currency | NumberStyles.Float, column.FormatProvider);
		//		else if (column.DataType == typeof(float))
		//			return Single.Parse(value, NumberStyles.Currency | NumberStyles.Float, column.FormatProvider);

		//		else if (column.DataType == typeof(byte))
		//			return Byte.Parse(value, NumberStyles.Integer, column.FormatProvider);
		//		else if (column.DataType == typeof(sbyte))
		//			return SByte.Parse(value, NumberStyles.Integer, column.FormatProvider);
		//		else if (column.DataType == typeof(ushort))
		//			return UInt16.Parse(value, NumberStyles.Integer, column.FormatProvider);
		//		else if (column.DataType == typeof(short))
		//			return Int16.Parse(value, NumberStyles.Integer, column.FormatProvider);
		//		else if (column.DataType == typeof(uint))
		//			return UInt32.Parse(value, NumberStyles.Integer, column.FormatProvider);
		//		else if (column.DataType == typeof(int))
		//			return Int32.Parse(value, NumberStyles.Integer, column.FormatProvider);
		//		else if (column.DataType == typeof(ulong))
		//			return UInt64.Parse(value, NumberStyles.Integer, column.FormatProvider);
		//		else if (column.DataType == typeof(long))
		//			return Int64.Parse(value, NumberStyles.Integer, column.FormatProvider);

		//		else if (column.DataType == typeof(DateTime))
		//			return DateTime.Parse(value, column.FormatProvider, DateTimeStyles.AssumeLocal);

		//		else if (column.DataType == typeof(string))
		//			return value;

		//		else
		//			return Procs.ChangeType(value, column.DataType);
		//	} // func GetValueIntern

		//	public override object this[int index]
		//	{
		//		get
		//		{
		//			try
		//			{
		//				return GetValueIntern(index);
		//			}
		//			catch (Exception e)
		//			{
		//				Debug.WriteLine(String.Format("[{0}] {1}", e.GetType().Name, e.Message));
		//				if (enumerator.IsParsedStrict)
		//					throw;
		//				else
		//					return null;
		//			}
		//		}
		//	} // func this

		//	public override bool IsDataOwner => false;

		//	public override IReadOnlyList<IDataColumn> Columns => enumerator.Columns;
		//} // class TextDataRow

		//#endregion

		#region -- class ValueGet -----------------------------------------------------

		private abstract class ValueGet
		{
			public ValueGet(string name)
			{
				Index = -1;
				Name = name ?? throw new ArgumentNullException(nameof(name));
			} // ctor

			protected abstract string GetValueCore(object value);

			public string Get(IDataRow row)
				=> GetValueCore(Index >= 0 ? row[Index] : null);

			public string Name { get; }
			public int Index { get; set; }
		} // class ValueGet

		#endregion

		#region -- class ValueConverterGet --------------------------------------------

		private sealed class ValueConverterGet : ValueGet
		{
			private readonly Func<object, string> converter;

			public ValueConverterGet(string name, Func<object, string> converter)
				: base(name)
			{
				this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
			}

			protected override string GetValueCore(object value)
				=> converter(value);
		} // class ValueConverterGet

		#endregion

		#region -- class ValueStringGet -----------------------------------------------

		private sealed class ValueStringGet : ValueGet
		{
			private readonly IFormatProvider formatProvider;

			public ValueStringGet(string name, IFormatProvider formatProvider = null)
				: base(name)
			{
				this.formatProvider = formatProvider;
			}

			protected override string GetValueCore(object value)
				=> Convert.ToString(value, formatProvider);
		} // class ValueStringGet

		#endregion

		private ValueGet[] columnsInfo = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="coreWriter"></param>
		/// <param name="columns"></param>
		public TextDataRowWriter(ITextCoreWriter coreWriter, params TextDataRowWriterColumn[] columns)
			: base(coreWriter)
		{
			if (columns != null && columns.Length >= 0)
				GenerateColumnInfo(columns);
		} // ctor

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		/// <param name="columns"></param>
		public TextDataRowWriter(TextWriter sw, TextCsvSettings settings, params TextDataRowWriterColumn[] columns)
			: this(new TextCsvWriter(sw, settings), columns)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		/// <param name="columns"></param>
		public TextDataRowWriter(TextWriter sw, TextFixedSettings settings, params TextDataRowWriterColumn[] columns)
			: this(new TextFixedWriter(sw, settings), columns)
		{
		} // ctor

		private void GenerateColumnInfo(IReadOnlyList<TextDataRowWriterColumn> columns)
		{
			columnsInfo = new ValueGet[columns.Count];
			for (var i = 0; i < columnsInfo.Length; i++)
			{
				var col = columns[i];

				columnsInfo[i] = col.Converter != null
					? (ValueGet)new ValueConverterGet(col.Name, col.Converter)
					: new ValueStringGet(col.Name, col.FormatProvider);
			}
		} // proc GenerateColumnInfo

		private void GenerateColumnInfo(IReadOnlyList<IDataColumn> columns)
		{
			columnsInfo = new ValueGet[columns.Count];
			for (var i = 0; i < columnsInfo.Length; i++)
			{
				var col = columns[i];
				columnsInfo[i] = new ValueStringGet(col.Name, CultureInfo.InvariantCulture) { Index = i };
			}
		} // proc GenerateColumnInfo

		private void AttachIndex(IDataRow current)
		{
			for (var i = 0; i < columnsInfo.Length; i++)
				columnsInfo[i].Index = current.FindColumnIndex(columnsInfo[i].Name);
		} // proc AttachIndex

		#endregion

		private void WriteHeader()
			=> CoreWriter.WriteRow(columnsInfo.Select(c => c.Name));

		private void WriteRow(IDataRow row)
			=> CoreWriter.WriteRow(columnsInfo.Select(c => c.Get(row)));

		/// <summary>Write rows</summary>
		/// <param name="rows"></param>
		public void Write(IEnumerable<IDataRow> rows)
		{
			using (var e = rows.GetEnumerator())
				Write(e, false, rows as IDataColumns);
		} // func Write

		/// <summary>Write rows</summary>
		/// <param name="rowEnumerator"></param>
		/// <param name="emitCurrent"><c>true</c>, the method starts with a MoveNext().</param>
		/// <param name="columns"></param>
		public void Write(IEnumerator<IDataRow> rowEnumerator, bool emitCurrent = false, IDataColumns columns = null)
		{
			var doAttachIndex = true;

			if (columnsInfo == null)
			{
				// get column information from rows
				if (!emitCurrent)
				{
					if (columns == null)
					{
						columns = rowEnumerator as IDataColumns;
						if (columns == null)
						{
							if (!rowEnumerator.MoveNext())
								return;
							columns = rowEnumerator.Current;
						}
					}
				}
				else
					columns = rowEnumerator.Current;

				GenerateColumnInfo(columns.Columns);
				doAttachIndex = false;
			}
			else
			{
				// write header
				if (CoreWriter.Settings.HeaderRow >= 0)
					WriteHeader();
			}

			// emit first row
			if (emitCurrent)
			{
				if (doAttachIndex)
					AttachIndex(rowEnumerator.Current);
				WriteRow(rowEnumerator.Current);
			}
			else
			{
				if (rowEnumerator.MoveNext())
				{
					if (doAttachIndex)
						AttachIndex(rowEnumerator.Current);
					WriteRow(rowEnumerator.Current);
				}
				else
					return;
			}

			// emit all other rows
			while (rowEnumerator.MoveNext())
				WriteRow(rowEnumerator.Current);
		} // proc Write
	} // class TextDataRowEnumerator

	#endregion

}
