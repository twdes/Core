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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TecWare.DE.Data
{
	#region -- ITextCoreReader --------------------------------------------------------

	/// <summary>Reader interface for row and column based text-files.</summary>
	public interface ITextCoreReader : IReadOnlyList<string>, IDisposable
	{
		/// <summary>Skip rows, without reading it.</summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		bool SkipRows(int rows);
		/// <summary>Read next row.</summary>
		/// <returns></returns>
		bool ReadRow();

		/// <summary>Base reader.</summary>
		TextReader BaseReader { get; }
		/// <summary>Settings for the text-file.</summary>
		TextCoreSettings Settings { get; }
	} //	interface ITextCoreReader

	#endregion

	#region -- class TextCoreReader ---------------------------------------------------

	/// <summary>Base text reader implementation.</summary>
	public abstract class TextCoreReader<TTEXTSCORESETTINGS> : ITextCoreReader
		where TTEXTSCORESETTINGS : TextCoreSettings
	{
		private readonly TextReader tr;
		private readonly TTEXTSCORESETTINGS settings;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="settings"></param>
		protected TextCoreReader(TextReader tr, TTEXTSCORESETTINGS settings)
		{
			this.tr = tr ?? throw new ArgumentNullException(nameof(tr));
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
					tr.Dispose();
			}
		} // proc Dispose

		#endregion

		#region -- GetEnumerator ------------------------------------------------------

		/// <summary>Enumerate the columns</summary>
		/// <returns></returns>
		public IEnumerator<string> GetEnumerator()
		{
			for (var i = 0; i < Count; i++)
				yield return this[i];
		} // func GetEnumerator

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();

		#endregion

		/// <summary>Skips the number of rows, without reading any contents.</summary>
		/// <param name="rows">Number of rows to skip.</param>
		/// <returns>Skip was successful</returns>
		public abstract bool SkipRows(int rows);

		/// <summary>Reads the contents of the current row.</summary>
		/// <returns></returns>
		public abstract bool ReadRow();

		/// <summary>Number of columns</summary>
		public abstract int Count { get; }
		/// <summary>Content of a column</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public abstract string this[int index] { get; }

		/// <summary>Returns the current Text-Reader.</summary>
		public TextReader BaseReader => tr;
		/// <summary>Settings for the file.</summary>
		public TTEXTSCORESETTINGS Settings => settings;

		TextCoreSettings ITextCoreReader.Settings => settings;
	} // interface ITextCoreReader

	#endregion

	#region -- class TextFixedReader --------------------------------------------------

	/// <summary>Text reader, that is based on a fixed column length.</summary>
	public sealed class TextFixedReader : TextCoreReader<TextFixedSettings>
	{
		private readonly int recordLength;
		private readonly int[] recordOffsets;

		private char[] currentRecord;

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="settings"></param>
		public TextFixedReader(TextReader tr, TextFixedSettings settings)
			: base(tr, settings)
		{
			if (settings.Lengths == null || settings.Lengths.Length == 0)
				throw new ArgumentNullException("columnLengths");

			this.recordOffsets = new int[settings.Lengths.Length];

			var ofs = 0;
			for (var i = 0; i < settings.Lengths.Length; i++)
			{
				recordOffsets[i] = ofs;
				ofs += settings.Lengths[i];
			}

			this.recordLength = ofs;
			this.currentRecord = new char[recordLength];
		} // ctor

		private bool ReadRecord(bool throwInvalidBlockLength)
		{
			var r = BaseReader.ReadBlock(currentRecord, 0, recordLength);
			if (r == recordLength)
				return true;
			else if (r == 0 || !throwInvalidBlockLength)
				return false;
			else
				throw new ArgumentException("Invalid block length.");
		} // proc ReadRecord

		/// <summary></summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		public override bool SkipRows(int rows)
		{
			while (rows-- > 0)
			{
				if (ReadRecord(false))
					return false;
			}
			return true;
		} // func SkipRows

		/// <summary></summary>
		/// <returns></returns>
		public override bool ReadRow()
			=> ReadRecord(true);

		/// <summary></summary>
		public override int Count => recordOffsets.Length;

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public override string this[int index]
		{
			get
			{
				var ofs = recordOffsets[index];
				var end = ofs + Settings.Lengths[index] - 1;
				var padding = Settings.Padding;

				for (var i = end; i >= ofs; i--)
				{
					if (currentRecord[i] != padding)
						return new String(currentRecord, ofs, i - ofs + 1);
				}

				return String.Empty;
			}
		} // func this
	} // class TextFixedReader

	#endregion

	#region -- class TextCsvReader ----------------------------------------------------

	/// <summary>Csv reader</summary>
	public sealed class TextCsvReader : TextCoreReader<TextCsvSettings>
	{
		#region -- enum ReadColumnReturn ----------------------------------------------

		private enum ReadColumnReturn
		{
			/// <summary>End of Column</summary>
			EoC,
			/// <summary>End of Line</summary>
			EoL,
			/// <summary>End of File</summary>
			EoF,
		} // enum ReadColumnReturn

		#endregion

		private readonly List<string> columns = new List<string>();

		private int charOffset = 0;
		private int charBufferLength = 0;
		private readonly char[] charBuffer;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="settings"></param>
		public TextCsvReader(TextReader tr, TextCsvSettings settings)
			: base(tr, settings)
		{
			this.charBuffer = new char[1024];
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				columns.Clear();
			base.Dispose(disposing);
		} // proc Dispose

		#endregion

		#region -- Primitives ---------------------------------------------------------

		private void SetColumn(int index, string value)
		{
			if (index < columns.Count)
				columns[index] = value;
			else
				columns.Add(value);
		} // proc SetColumn

		private bool ReadBuffer()
		{
			charOffset = 0;
			charBufferLength = BaseReader.Read(charBuffer, charOffset, charBuffer.Length);

			return charBufferLength > 0;
		} // func ReadBuffer

		#endregion

		#region -- SkipRow ------------------------------------------------------------

		private bool SkipRow()
		{
			var state = 0;
			while (true)
			{
				// read next block
				if (charOffset >= charBufferLength)
				{
					if (!ReadBuffer())
						return false;
				}

				var c = charBuffer[charOffset++];
				switch (state)
				{
					#region -- 0 --
					case 0: // collect all until quote
						if (c == '\n')
							state = 10;
						else if (c == '\r')
							state = 11;
						break;
					#endregion

					#region -- 10, 11 NewLines --
					case 10: // \r
						if (c != '\r')
							charOffset--;
						return true;
					case 11: // \r
						if (c != '\n')
							charOffset--;
						return true;
						#endregion
				}
			}
		} // func SkipRow

		/// <summary></summary>
		/// <param name="rows"></param>
		/// <returns></returns>
		public override bool SkipRows(int rows)
		{
			while (rows-- > 0)
			{
				if (!SkipRow())
					return false;
			}
			return true;
		} // func SkipRows

		#endregion

		#region -- ReadRow ------------------------------------------------------------

		private ReadColumnReturn ReadColumn(int currentColumn)
		{
			var state = 0; // state of the parser
			var quote = Settings.Quote;
			var delemiter = Settings.Delemiter;
			var sbValue = new StringBuilder();
			var mode = Settings.Quotation;

			while (true)
			{
				// read next block
				if (charOffset >= charBufferLength)
				{
					if (!ReadBuffer())
					{
						if (sbValue.Length > 0 || currentColumn > 0)
						{
							SetColumn(currentColumn, sbValue.ToString());
							return ReadColumnReturn.EoL;
						}
						else
							return ReadColumnReturn.EoF;
					}
				}

				// parse logic
				var c = charBuffer[charOffset++];

				switch (state)
				{
					#region -- 0 --
					case 0: // collect all until quote
						if (c == delemiter) // end of column
						{
							SetColumn(currentColumn, sbValue.ToString());
							return ReadColumnReturn.EoC;
						}
						else if (c == '\n')
							state = 10;
						else if (c == '\r')
							state = 11;
						else if (c == quote && mode != CsvQuotation.None)
						{
							if (mode == CsvQuotation.Forced && sbValue.Length > 0) // parse error
								throw new ArgumentException("todo: error text for invalid forced quote format (double quote).");
							state = 5;
						}
						else if (mode != CsvQuotation.Forced)
							sbValue.Append(c);

						break;
					#endregion

					#region -- 5, 6 Quotes --
					case 5:
						if (c == quote) // check for escaped quote
							state = 6;
						else
							sbValue.Append(c);
						break;

					case 6:
						if (c == quote) // escaped
						{
							sbValue.Append(c);
							state = 5;
						}
						else
						{
							charOffset--;
							state = 0;
							mode = CsvQuotation.Forced; // set quote to force, to ignore chars afterwards
						}
						break;
					#endregion

					#region -- 10, 11 NewLines --
					case 10: // \r
						if (c != '\r')
							charOffset--;
						SetColumn(currentColumn, sbValue.ToString());
						return ReadColumnReturn.EoL;
					case 11: // \r
						if (c != '\n')
							charOffset--;
						SetColumn(currentColumn, sbValue.ToString());
						return ReadColumnReturn.EoL;
						#endregion
				}
			}
		} // func ReadColumn

		/// <summary></summary>
		/// <returns></returns>
		public override bool ReadRow()
		{
			// first clear current row
			for (var i = 0; i < columns.Count; i++)
				columns[i] = null;

			// fetch row
			var currentColumn = 0;
			while (true)
			{
				var s = ReadColumn(currentColumn);
				if (s == ReadColumnReturn.EoF)
					return false;
				else if (s == ReadColumnReturn.EoL)
					return true;
				else
					currentColumn++;
			}
		} // func ReadRow

		#endregion

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public override string this[int index] => index >= 0 && index < columns.Count ? columns[index] : null;

		/// <summary></summary>
		public override int Count => columns.Count;
	} // class TextCsvReader

	#endregion

	#region -- class TextRowEnumerator ------------------------------------------------

	/// <summary>Enumerator implementation for a text reader.</summary>
	public abstract class TextRowEnumerator<T> : IEnumerator<T>
	{
		private readonly ITextCoreReader coreReader;
		private int currentRow = -1;
		private int currentDataRow = -1;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="coreReader"></param>
		public TextRowEnumerator(ITextCoreReader coreReader)
		{
			this.coreReader = coreReader;
		} // ctor

		/// <summary></summary>
		public void Dispose()
			=> Dispose(true);

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
				coreReader.Dispose();
		} // proc Dispose

		#endregion

		#region -- Enumerator ---------------------------------------------------------

		private void SkipToRow(int targetRow)
		{
			// move before this row
			var rows = (targetRow - 1) - currentRow;
			if (rows > 0)
			{
				coreReader.SkipRows(rows);
				currentRow += rows;
			}
		} // void SkipToRow

		private bool CoreRead()
		{
			if (coreReader.ReadRow())
			{
				currentRow++;
				return true;
			}
			else
				return false;
		} // func CoreRead

		/// <summary></summary>
		protected virtual void OnReset() { }

		/// <summary></summary>
		public void Reset()
		{
			if (currentRow == -1)
				return;

			if (!(coreReader.BaseReader is StreamReader sr) || !sr.BaseStream.CanSeek)
				throw new NotSupportedException();

			// seek begin of file
			sr.BaseStream.Seek(0, SeekOrigin.Begin);
			sr.DiscardBufferedData();

			currentRow = -1;
			currentDataRow = -1;
			OnReset();
		} // proc Reset

		/// <summary></summary>
		/// <returns></returns>
		public virtual string[] MoveToHeader()
		{
			// move to header
			SkipToRow(coreReader.Settings.HeaderRow);

			if (currentRow == coreReader.Settings.HeaderRow - 1 && CoreRead())
			{
				// build header mapping
				var headerMapping = new string[coreReader.Count];
				for (var i = 0; i < headerMapping.Length; i++)
					headerMapping[i] = coreReader[i];

				return headerMapping;
			}
			else
				throw new InvalidOperationException("Could not read header row.");
		} // func MoveToHeader

		/// <summary></summary>
		protected virtual void UpdateCurrent()
		{
		} // proc UpdateCurrent

		/// <summary></summary>
		/// <returns></returns>
		public virtual bool MoveNext()
		{
			SkipToRow(coreReader.Settings.StartRow);

			var r = CoreRead();
			if (r)
			{
				UpdateCurrent();
				currentDataRow++;
			}

			return r;
		} // proc MoveNext

		#endregion

		object IEnumerator.Current => Current;

		/// <summary>Current row</summary>
		public abstract T Current { get; }
		/// <summary>Current row number.</summary>
		public int Row => currentDataRow;
		/// <summary>Access to the reader.</summary>
		public ITextCoreReader CoreReader => coreReader;
	} // class TextRowEnumerator

	#endregion
}
