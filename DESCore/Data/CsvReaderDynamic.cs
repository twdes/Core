using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- class TextDynamicColumn --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Column description</summary>
	public sealed class TextDynamicColumn
	{
		/// <summary>Name of the column</summary>
		public string ColumnName { get; set; }
		/// <summary>Data type</summary>
		public Type DataType { get; set; }
		/// <summary>Optional special provider for the format of the value.</summary>
		public IFormatProvider FormatProvider { get; set; }
		/// <summary>Convert Type.</summary>
		public Func<string, object> Converter { get; set; }
	} // class TextDynamicColumn

	#endregion

	#region -- class TextDynamicRow -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class TextDynamicRow
	{
		private TextDynamicRowEnumerator owner;
		private int version = 0;

		internal TextDynamicRow(TextDynamicRowEnumerator owner)
		{
			this.owner = owner;
		} // ctor

		internal void Reset()
		{
			version++;
		} // proc Reset

		private object GetValueIntern(int columnIndex)
		{
			// get value
			var value = owner.CoreReader[columnIndex];
			var column = owner.Columns?[columnIndex];
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
		} // func GetValue

		public object GetValue(int columnIndex)
		{
			try
			{
				return GetValueIntern(columnIndex);
			}
			catch (Exception e)
			{
				Debug.WriteLine(String.Format("[{0}] {1}", e.GetType().Name, e.Message));
					if (owner.IsParsedStrict)
						throw;
					else
						return null;
			}
		} // func GetValue

		public T GetValue<T>(int columnIndex)
		{
			try
			{
				var obj = GetValueIntern(columnIndex);
				return obj == null ? default(T) :(T)obj;
			}
			catch (Exception e)
			{
				Debug.WriteLine(String.Format("[{0}] {1}", e.GetType().Name, e.Message));
				if (owner.IsParsedStrict)
					throw;
				else
					return default(T);
			}
		} // func GetValue

		public T GetValue<T>(string columnName)
			=> GetValue<T>(IndexOfColumn(columnName));

		public int IndexOfColumn(string columnName)
			=> Array.FindIndex(owner.Columns, c => String.Compare(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase) == 0);

		public object this[int columnIndex] => GetValue(columnIndex);
		public object this[string columnName] => GetValue(IndexOfColumn(columnName));
	} // class TextDynamicRow

	#endregion

	#region -- class TextDynamicRowEnumerator -------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class TextDynamicRowEnumerator : TextRowEnumerator<TextDynamicRow>
	{
		private TextDynamicColumn[] columns = null;
		private readonly TextDynamicRow currentRow;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public TextDynamicRowEnumerator(ITextCoreReader coreReader)
			: base(coreReader)
		{
			this.currentRow = new TextDynamicRow(this);
		} // ctor

		public TextDynamicRowEnumerator(TextReader tr, TextCsvSettings settings)
			: this(new TextCsvReader(tr, settings))
		{
		} // ctor

		protected override void Dispose(bool disposing)
		{
			if (disposing)
				columns = null;

			base.Dispose(disposing);
		} // proc Dispose

		#endregion

		/// <summary>Sets the column description.</summary>
		/// <param name="columns"></param>
		public void UpdateColumns(params TextDynamicColumn[] columns)
		{
			this.columns = columns;
			currentRow.Reset();
		} // proc UpdateColumns

		/// <summary>The returned reference is reused.</summary>
		public override TextDynamicRow Current => currentRow;
		/// <summary>Column definition.</summary>
		public TextDynamicColumn[] Columns => columns;
		/// <summary></summary>
		public bool IsParsedStrict { get; set; } = false;
	} // class TextDynamicRowEnumerator

	#endregion
}
