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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- interface ITextCoreWriter ----------------------------------------------

	/// <summary>Core interface to write a csv-file.</summary>
	public interface ITextCoreWriter : IDisposable
	{
		/// <summary>Write a value line</summary>
		/// <param name="values">Values as text</param>
		void WriteRow(IEnumerable<string> values);

		/// <summary>Base output stream.</summary>
		TextWriter BaseWriter { get; }
		/// <summary>CSV-configuration</summary>
		TextCoreSettings Settings { get; }
	} // interface ITextCoreWriter

	#endregion

	#region -- interface ITextCoreWriter2 ---------------------------------------------

	/// <summary>Extented WriteRow implementation</summary>
	public interface ITextCoreWriter2 : ITextCoreWriter
	{
		/// <summary>Write a value line</summary>
		/// <param name="values">Values as text</param>
		/// <param name="isText">Is this a text column.</param>
		void WriteRow(IEnumerable<string> values, bool[] isText);
	} // interface ITextCoreWriter2

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
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		} // proc Dispose

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
	public sealed class TextCsvWriter : TextCoreWriter<TextCsvSettings>, ITextCoreWriter2
	{
		private readonly Func<string, bool?, bool> quoteValue;

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		public TextCsvWriter(TextWriter sw, TextCsvSettings settings)
			: base(sw, settings)
		{
			switch (Settings.Quotation)
			{
				case CsvQuotation.Forced:
					quoteValue = new Func<string, bool?, bool>((v, isText) => true);
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
			var delemiter = Settings.Delemiter;
			var i = 0;
			foreach (var v in values)
			{
				if (i > 0)
					BaseWriter.Write(delemiter);
				WriteRowValue(v, isText == null || i >= isText.Length ? (bool?)null : isText[i]);
				i++;
			}
			BaseWriter.WriteLine();
		} // proc WriteRow
	} // class TextCsvWriter

	#endregion

	#region -- class TextObjectWriter -------------------------------------------------

	/// <summary></summary>
	public abstract class TextObjectWriter : IDisposable
	{
		private readonly ITextCoreWriter coreWriter;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="coreWriter"></param>
		protected TextObjectWriter(ITextCoreWriter coreWriter)
		{
			this.coreWriter = coreWriter ?? throw new ArgumentNullException(nameof(coreWriter));
		} // ctor

		/// <summary></summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		} // proc Dispose

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

	#region -- class TextDataRowWriter ------------------------------------------------

	/// <summary></summary>
	public sealed class TextDataRowWriter : TextObjectWriter
	{
		#region -- class ValueGet -----------------------------------------------------

		private sealed class ValueGet
		{
			private readonly IStringConverter converter;
			private readonly IFormatProvider formatProvider;

			public ValueGet(string name, IStringConverter converter, IFormatProvider formatProvider)
			{
				Index = -1;
				Name = name ?? throw new ArgumentNullException(nameof(name));
				this.converter = converter;
				this.formatProvider = formatProvider ?? CultureInfo.InvariantCulture;
			} // ctor

			public string Get(IDataRow row)
			{
				if (converter != null)
					return converter.Format(Index >= 0 ? row[Index] : null);
				else
				{
					var v = Index >= 0 ? row[Index] : null;
					return Convert.ToString(v, formatProvider);
				}
			} // func Get

			public string Name { get; }
			public int Index { get; set; }
		} // class ValueGet

		#endregion

		private bool[] isText = null;
		private ValueGet[] columnsInfo = null;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="coreWriter"></param>
		/// <param name="columns"></param>
		public TextDataRowWriter(ITextCoreWriter coreWriter, params IDataColumn[] columns)
			: base(coreWriter)
		{
			if (columns != null && columns.Length >= 0)
				GenerateColumnInfo(columns, CultureInfo.InvariantCulture);
		} // ctor

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		/// <param name="columns"></param>
		public TextDataRowWriter(TextWriter sw, TextCsvSettings settings, params IDataColumn[] columns)
			: this(new TextCsvWriter(sw, settings), columns)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="sw"></param>
		/// <param name="settings"></param>
		/// <param name="columns"></param>
		public TextDataRowWriter(TextWriter sw, TextFixedSettings settings, params IDataColumn[] columns)
			: this(new TextFixedWriter(sw, settings), columns)
		{
		} // ctor

		private void GenerateColumnInfo(IReadOnlyList<IDataColumn> columns, CultureInfo defaultCulture)
		{
			isText = CoreWriter is ITextCoreWriter2 ? new bool[columns.Count] : null;
			columnsInfo = new ValueGet[columns.Count];
			for (var i = 0; i < columnsInfo.Length; i++)
			{
				var col = columns[i];
				columnsInfo[i] = new ValueGet(
					col.Name,
					col.GetConverter(),
					col.GetFormatProvider() ?? defaultCulture
				)
				{ Index = i };

				if (isText != null)
					isText[i] = col.DataType == typeof(string);
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

		private IEnumerable<string> GetRowValues(IDataRow row)
			=> columnsInfo.Select(c => c.Get(row));

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

				GenerateColumnInfo(columns.Columns, CultureInfo.InvariantCulture);
				doAttachIndex = false;
			}

			var writeRow = isText != null
				? new Action<IDataRow>(r => ((ITextCoreWriter2)CoreWriter).WriteRow(GetRowValues(r), isText))
				: new Action<IDataRow>(r => CoreWriter.WriteRow(GetRowValues(r)));

			// write header
			if (CoreWriter.Settings.HeaderRow >= 0)
				WriteHeader();

			// emit first row
			if (emitCurrent)
			{
				if (doAttachIndex)
					AttachIndex(rowEnumerator.Current);
				writeRow(rowEnumerator.Current);
			}
			else
			{
				if (rowEnumerator.MoveNext())
				{
					if (doAttachIndex)
						AttachIndex(rowEnumerator.Current);
					writeRow(rowEnumerator.Current);
				}
				else
					return;
			}

			// emit all other rows
			while (rowEnumerator.MoveNext())
				writeRow(rowEnumerator.Current);
		} // proc Write
	} // class TextDataRowEnumerator

	#endregion
}
