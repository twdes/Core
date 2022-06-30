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
using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- class TextDataRowEnumerator --------------------------------------------

	/// <summary></summary>
	public sealed class TextDataRowEnumerator : TextRowEnumerator<IDataRow>
	{
		#region -- class TextDataRow --------------------------------------------------

		private sealed class TextDataRow : DynamicDataRow
		{
			#region -- class CallConverter --------------------------------------------

			private sealed class CallConverter
			{
				private readonly IStringConverter converter;
				private readonly IFormatProvider formatProvider;

				public CallConverter(IStringConverter converter, IFormatProvider formatProvider)
				{
					this.converter = converter ?? throw new ArgumentNullException(nameof(converter));
					this.formatProvider = formatProvider;
				} // ctor

				public object TryParse(string value)
					=> converter.TryParse(value, formatProvider, out var r) ? r : null;

				public object Parse(string value)
					=> converter.Parse(value, formatProvider);
			} // class CallConverter

			#endregion

			#region -- class FormatConverter ------------------------------------------

			private sealed class FormatConverter
			{
				private readonly IFormatProvider formatProvider;

				public FormatConverter(IFormatProvider formatProvider)
				{
					this.formatProvider = formatProvider;
				} // ctor

				public object ParseGuid(string value)
					=> value == null ? Guid.Empty : Guid.Parse(value);

				public object TryParseGuid(string value)
					=> value != null && Guid.TryParse(value, out var r) ? r : Guid.Empty;

				public object ParseDecimal(string value)
					=> value == null ? 0.0m : Decimal.Parse(value, NumberStyles.Currency | NumberStyles.Float, formatProvider);

				public object TryParseDecimal(string value)
					=> value != null && Decimal.TryParse(value, NumberStyles.Currency | NumberStyles.Float, formatProvider, out var r) ? r : 0.0m;

				public object ParseDouble(string value)
					=> value == null ? 0.0 : Double.Parse(value, NumberStyles.Currency | NumberStyles.Float, formatProvider);

				public object TryParseDouble(string value)
					=> value != null && Double.TryParse(value, NumberStyles.Currency | NumberStyles.Float, formatProvider, out var r) ? r : 0.0;

				public object ParseSingle(string value)
					=> value == null ? 0.0f : (object)Single.Parse(value, NumberStyles.Currency | NumberStyles.Float, formatProvider);

				public object TryParseSingle(string value)
					=> value != null && Single.TryParse(value, NumberStyles.Currency | NumberStyles.Float, formatProvider, out var r) ? r : 0.0f;


				public object ParseByte(string value)
					=> value == null ? 0 : Byte.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseByte(string value)
					=> value != null && Byte.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;

				public object ParseSByte(string value)
					=> value == null ? 0 : SByte.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseSByte(string value)
					=> value != null && SByte.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;

				public object ParseUInt16(string value)
					=> value == null ? 0 : UInt16.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseUInt16(string value)
					=> value != null && UInt16.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;

				public object ParseInt16(string value)
					=> value == null ? 0 : Int16.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseInt16(string value)
					=> value != null && Int16.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;

				public object ParseUInt32(string value)
					=> value == null ? 0 : UInt32.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseUInt32(string value)
					=> value != null && UInt32.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;

				public object ParseInt32(string value)
					=> value == null ? 0 : Int32.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseInt32(string value)
					=> value != null && Int32.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;

				public object ParseUInt64(string value)
					=> value == null ? 0 : UInt64.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseUInt64(string value)
					=> value != null && UInt64.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;

				public object ParseInt64(string value)
					=> value == null ? 0 : Int64.Parse(value, NumberStyles.Integer, formatProvider);

				public object TryParseInt64(string value)
					=> value != null && Int64.TryParse(value, NumberStyles.Integer, formatProvider, out var r) ? r : 0;


				public object ParseDateTime(string value)
					=> value == null ? DateTime.MinValue : DateTime.Parse(value, formatProvider, DateTimeStyles.AssumeLocal);

				public object TryParseDateTime(string value)
					=> value != null && DateTime.TryParse(value, formatProvider, DateTimeStyles.AssumeLocal, out var r) ? r : DateTime.MinValue;

				public object ParseDateTimeOffset(string value)
					=> value == null ? DateTimeOffset.MinValue : DateTime.Parse(value, formatProvider, DateTimeStyles.AssumeLocal);

				public object TryParseDateTimeOffset(string value)
					=> value != null && DateTimeOffset.TryParse(value, formatProvider, DateTimeStyles.AssumeLocal, out var r) ? r : DateTimeOffset.MinValue;
			} // class FormatConverter

			#endregion

			#region -- class EnumConverter --------------------------------------------

			private sealed class EnumConverter
			{
				private readonly Type enumType;

				public EnumConverter(Type enumType)
				{
					this.enumType = enumType ?? throw new ArgumentNullException(nameof(enumType));
				} // ctor

				public object Parse(string value)
					=> Enum.Parse(enumType, value);

				public object TryParse(string value)
				{
					if (value == null)
						return Activator.CreateInstance(enumType);
					try
					{
						return Enum.Parse(enumType, value);
					}
					catch (FormatException)
					{
						return Activator.CreateInstance(enumType);
					}
				} // func TryParse
			} // class EnumConverter

			#endregion

			#region -- class GenericConverter -----------------------------------------

			private sealed class GenericConverter
			{
				private readonly Type type;

				public GenericConverter(Type type)
				{
					this.type = type ?? throw new ArgumentNullException(nameof(type));
				} // ctor

				public object ParseClass(string value)
				{
					if (value == null)
						return null;
					return Procs.ChangeType(value, type);
				} // func ParseClass

				public object TryParseClass(string value)
				{
					if (value == null)
						return null;

					try
					{
						return Procs.ChangeType(value, type);
					}
					catch (FormatException)
					{
						return null;
					}
				} // func TryParseClass

				public object ParseStruct(string value)
				{
					if (value == null)
						return Activator.CreateInstance(type);
					return Procs.ChangeType(value, type);
				} // func ParseStruct

				public object TryParseStruct(string value)
				{
					if (value == null)
						return Activator.CreateInstance(type);

					try
					{
						return Procs.ChangeType(value, type);
					}
					catch (FormatException)
					{
						return Activator.CreateInstance(type);
					}
				} // func TryParseStruct
			} // class GenericConverter

			#endregion

			#region -- class NullableConverter ----------------------------------------

			private sealed class NullableConverter
			{
				private readonly Func<string, object> func;

				public NullableConverter(Func<string, object> func)
				{
					this.func = func ?? throw new ArgumentNullException(nameof(func));
				} // ctor

				public object Parse(string value)
					=> value == null ? null : func(value); // (Nullable<?>)value -> is boxed to (object)value
			} // class NullableConverter

			#endregion


			private readonly TextDataRowEnumerator enumerator;
			private Func<string, object>[] getValues = null;

			public TextDataRow(TextDataRowEnumerator enumerator)
			{
				this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
			} // ctor

			private static object ConvertNone(string value)
				=> value;

			private Func<string, object> GetTypeConverter(Type dataType, IFormatProvider formatProvider)
			{
				if (dataType == null)
					throw new ArgumentNullException(nameof(IDataColumn.DataType));

				if (dataType.IsGenericType && !dataType.IsGenericTypeDefinition && dataType.GetGenericTypeDefinition() == typeof(Nullable<>))
					return new NullableConverter(GetTypeConverter(dataType.GetGenericArguments()[0], formatProvider)).Parse;
				else
				{
					var isStrict = enumerator.IsParsedStrict;

					if (dataType == typeof(string))
						return new Func<string, object>(ConvertNone);
					else if (dataType == typeof(Guid))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseGuid) : new FormatConverter(formatProvider).TryParseGuid;

					else if (dataType == typeof(decimal))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseDecimal) : new FormatConverter(formatProvider).TryParseDecimal;
					else if (dataType == typeof(double))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseDouble) : new FormatConverter(formatProvider).TryParseDouble;
					else if (dataType == typeof(float))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseSingle) : new FormatConverter(formatProvider).TryParseSingle;

					else if (dataType == typeof(byte))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseByte) : new FormatConverter(formatProvider).TryParseByte;
					else if (dataType == typeof(sbyte))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseSByte) : new FormatConverter(formatProvider).TryParseSByte;
					else if (dataType == typeof(ushort))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseUInt16) : new FormatConverter(formatProvider).TryParseUInt16;
					else if (dataType == typeof(short))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseInt16) : new FormatConverter(formatProvider).TryParseInt16;
					else if (dataType == typeof(uint))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseUInt32) : new FormatConverter(formatProvider).TryParseUInt32;
					else if (dataType == typeof(int))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseInt32) : new FormatConverter(formatProvider).TryParseInt32;
					else if (dataType == typeof(ulong))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseUInt64) : new FormatConverter(formatProvider).TryParseUInt64;
					else if (dataType == typeof(long))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseInt64) : new FormatConverter(formatProvider).TryParseInt64;

					else if (dataType == typeof(DateTime))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseDateTime) : new FormatConverter(formatProvider).TryParseDateTime;
					else if (dataType == typeof(DateTimeOffset))
						return isStrict ? new Func<string, object>(new FormatConverter(formatProvider).ParseDateTimeOffset) : new FormatConverter(formatProvider).TryParseDateTimeOffset;


					else if (dataType.IsEnum)
						return isStrict ? new Func<string, object>(new EnumConverter(dataType).Parse) : new Func<string, object>(new EnumConverter(dataType).TryParse);

					else if (dataType.IsValueType)
						return isStrict ? new Func<string, object>(new GenericConverter(dataType).ParseStruct) : new Func<string, object>(new GenericConverter(dataType).TryParseStruct);
					else
						return isStrict ? new Func<string, object>(new GenericConverter(dataType).ParseClass) : new Func<string, object>(new GenericConverter(dataType).TryParseClass);
				}
			} // func GetTypeConverter

			private Func<string, object> GetConverter(IDataColumn column)
			{
				// test for converter
				var conv = column.GetConverter();
				var formatProvider = column.GetFormatProvider();
				if (conv != null)
				{
					return enumerator.IsParsedStrict
						? new Func<string, object>(new CallConverter(conv, formatProvider).Parse)
						: new Func<string, object>(new CallConverter(conv, formatProvider).TryParse);
				}
				else
					return GetTypeConverter(column.DataType, formatProvider);
			} // func GetConverter

			public void UpdateConverter(IReadOnlyList<IDataColumn> columns)
			{
				if (columns == null)
					getValues = null;
				else
				{
					getValues = new Func<string, object>[columns.Count];
					for (var i = 0; i < getValues.Length; i++)
						getValues[i] = GetConverter(columns[i]);
				}
			} // proc UpdateConverter

			public override object this[int index]
			{
				get
				{
					if (getValues == null)
						throw new ArgumentNullException(nameof(Columns), "No columns defined.");

					return getValues[index](enumerator.CoreReader[index]);
				}
			} // func this

			public override bool IsDataOwner => false;

			public override IReadOnlyList<IDataColumn> Columns => enumerator.Columns;
		} // class TextDataRow

		#endregion

		private IDataColumn[] columns = null;
		private bool isParseStrict = false;
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
		public void UpdateColumns(params IDataColumn[] columns)
		{
			this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
			currentRow.UpdateConverter(columns);
		} // proc UpdateColumns

		/// <summary>The returned reference is reused.</summary>
		public override IDataRow Current => currentRow;
		/// <summary>Column definition.</summary>
		public IReadOnlyList<IDataColumn> Columns => columns;
		/// <summary></summary>
		public bool IsParsedStrict
		{
			get => isParseStrict; set
			{
				if (isParseStrict != value)
				{
					isParseStrict = value;
					currentRow.UpdateConverter(columns);
				}
			}
		} // func IsParsedStrict
	} // class TextDataRowEnumerator

	#endregion
}
