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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- class TextDataRowColumn ------------------------------------------------

	/// <summary>Column description</summary>
	public sealed class TextDataRowColumn : IDataColumn
	{
		/// <summary>Name of the column</summary>
		public string Name { get; set; }
		/// <summary>Data type</summary>
		public Type DataType { get; set; }
		/// <summary>Optional special provider for the format of the value.</summary>
		public IFormatProvider FormatProvider { get; set; }
		/// <summary>Convert Type.</summary>
		public Func<string, object> Converter { get; set; }
		/// <summary>Empty attributes</summary>
		public IPropertyEnumerableDictionary Attributes => PropertyDictionary.EmptyReadOnly;
	} // class TextDataRowColumn

	#endregion

	#region -- class TextDataRowEnumerator --------------------------------------------

	/// <summary></summary>
	public sealed class TextDataRowEnumerator : TextRowEnumerator<IDataRow>
	{
		#region -- class TextDataRow --------------------------------------------------

		private sealed class TextDataRow : DynamicDataRow
		{
			private readonly TextDataRowEnumerator enumerator;

			public TextDataRow(TextDataRowEnumerator enumerator)
			{
				this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
			} // ctor

			private object GetValueIntern(int index)
			{
				// get value
				var value = enumerator.CoreReader[index];
				var column = enumerator.Columns?[index];
				if (column == null)
					return null; // no column defined

				// convert
				if (column.Converter != null)
					return column.Converter(value);
				else if (column.DataType == typeof(decimal))
					return Decimal.Parse(value, NumberStyles.Currency | NumberStyles.Float, column.FormatProvider);
				else if (column.DataType == typeof(double))
					return Double.Parse(value, NumberStyles.Currency | NumberStyles.Float, column.FormatProvider);
				else if (column.DataType == typeof(float))
					return Single.Parse(value, NumberStyles.Currency | NumberStyles.Float, column.FormatProvider);

				else if (column.DataType == typeof(byte))
					return Byte.Parse(value, NumberStyles.Integer, column.FormatProvider);
				else if (column.DataType == typeof(sbyte))
					return SByte.Parse(value, NumberStyles.Integer, column.FormatProvider);
				else if (column.DataType == typeof(ushort))
					return UInt16.Parse(value, NumberStyles.Integer, column.FormatProvider);
				else if (column.DataType == typeof(short))
					return Int16.Parse(value, NumberStyles.Integer, column.FormatProvider);
				else if (column.DataType == typeof(uint))
					return UInt32.Parse(value, NumberStyles.Integer, column.FormatProvider);
				else if (column.DataType == typeof(int))
					return Int32.Parse(value, NumberStyles.Integer, column.FormatProvider);
				else if (column.DataType == typeof(ulong))
					return UInt64.Parse(value, NumberStyles.Integer, column.FormatProvider);
				else if (column.DataType == typeof(long))
					return Int64.Parse(value, NumberStyles.Integer, column.FormatProvider);

				else if (column.DataType == typeof(DateTime))
					return DateTime.Parse(value, column.FormatProvider, DateTimeStyles.AssumeLocal);

				else if (column.DataType == typeof(string))
					return value;

				else
					return Procs.ChangeType(value, column.DataType);
			} // func GetValueIntern

			public override object this[int index]
			{
				get
				{
					try
					{
						return GetValueIntern(index);
					}
					catch (Exception e)
					{
						Debug.WriteLine(String.Format("[{0}] {1}", e.GetType().Name, e.Message));
						if (enumerator.IsParsedStrict)
							throw;
						else
							return null;
					}
				}
			} // func this

			public override bool IsDataOwner => false;

			public override IReadOnlyList<IDataColumn> Columns => enumerator.Columns;
		} // class TextDataRow

		#endregion

		private TextDataRowColumn[] columns = null;
		private readonly TextDataRow currentRow;

		#region -- Ctor/Dtor --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="coreReader"></param>
		public TextDataRowEnumerator(ITextCoreReader coreReader)
			: base(coreReader)
		{
			this.currentRow = new TextDataRow(this);
		} // ctor

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="settings"></param>
		public TextDataRowEnumerator(TextReader tr, TextCsvSettings settings)
			: this(new TextCsvReader(tr, settings))
		{
		} // ctor

		/// <summary></summary>
		/// <param name="tr"></param>
		/// <param name="settings"></param>
		public TextDataRowEnumerator(TextReader tr, TextFixedSettings settings)
			: this(new TextFixedReader(tr, settings))
		{
		} // ctor

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				columns = null;

			base.Dispose(disposing);
		} // proc Dispose

		#endregion

		/// <summary>Sets the column description.</summary>
		/// <param name="columns"></param>
		public void UpdateColumns(params TextDataRowColumn[] columns)
		{
			this.columns = columns;
		} // proc UpdateColumns

		/// <summary>The returned reference is reused.</summary>
		public override IDataRow Current => currentRow;
		/// <summary>Column definition.</summary>
		public TextDataRowColumn[] Columns => columns;
		/// <summary></summary>
		public bool IsParsedStrict { get; set; } = false;
	} // class TextDataRowEnumerator

	#endregion
}
