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
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataColumn
	{
		/// <summary>Gets the name in this column.</summary>
		string Name { get; }
		/// <summary>Gets the type of data in this column.</summary>
		Type DataType { get; }
		/// <summary>Gets the attributes in this column.</summary>
		IPropertyEnumerableDictionary Attributes { get; }
	} // interface IDataColumn

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataColumns
	{
		/// <summary>Gets the columns.</summary>
		IReadOnlyList<IDataColumn> Columns { get; }
	} // interface IDataColumns

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataValues
	{
		/// <summary>Gets the value for the column at the specified index.</summary>
		/// <param name="index">The zero-based index of the column to get the value.</param>
		/// <returns></returns>
		object this[int index] { get; }
	} // interface IDataValues

	#region -- interface IDataRow -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataRow : IDataColumns, IDataValues, IPropertyReadOnlyDictionary
	{
		/// <summary>Gets the value for the column with the specified name.</summary>
		/// <param name="columnName">The name of the column to get the value.</param>
		/// <returns></returns>
		object this[string columnName] { get; }
	} // interface IDataRow

	#endregion
	
	#region -- class DynamicDataRow -----------------------------------------------------

	public abstract class DynamicDataRow : IDataRow, IDynamicMetaObjectProvider
	{
		#region -- IDynamicMetaObjectProvider ---------------------------------------------

		public DynamicMetaObject GetMetaObject(Expression parameter)
		{
			// todo: Missing functionality!
			throw new NotImplementedException();
		} // func GetMetaObject

		#endregion

		#region -- IDataRow ---------------------------------------------------------------

		public virtual bool TryGetProperty(string columnName, out object value)
		{
			value = null;

			try
			{
				if (String.IsNullOrEmpty(columnName))
					return false;

				if (Columns == null || Columns.Count < 1)
					return false;

				var index = this.FindColumnIndex(columnName);
				if (index == -1)
					return false;

				value = this[index];
				return true;
			} // try
			catch
			{
				return false;
			} // catch
		} // func TryGetProperty
		
		public virtual object this[string columnName]
		{
			get
			{
				var index = this.FindColumnIndex(columnName);
				if (index == -1)
					throw new ArgumentException(String.Format("Column with name \"{0}\" not found.", columnName ?? "null"));
				return this[index];
			}
		} // prop this

		public abstract object this[int index] { get; }

		public abstract IReadOnlyList<IDataColumn> Columns { get; }

		#endregion
	} // class DynamicDataRow

	#endregion

	#region -- class GenericDataRowEnumerator<T> ----------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class GenericDataRowEnumerator<T> : IEnumerator<IDataRow>, IDataColumns
	{
		#region -- class PropertyColumnInfo -----------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class PropertyColumnInfo : IDataColumn
		{
			private readonly PropertyInfo property;

			public PropertyColumnInfo(PropertyInfo property)
			{
				this.property = property;
			} // ctor

			internal object GetValue(object current)
				=> property.GetValue(current);

			public string Name => property.Name;
			public Type DataType => property.PropertyType;

			public IPropertyEnumerableDictionary Attributes => PropertyDictionary.EmptyReadOnly;
		} // class PropertyColumnInfo

		#endregion

		#region -- class PropertyDataRow --------------------------------------------------

		private sealed class PropertyDataRow : IDataRow
		{
			private readonly GenericDataRowEnumerator<T> owner;
			private readonly object current;

			public PropertyDataRow(GenericDataRowEnumerator<T> owner, object current)
			{
				this.owner = owner;
				this.current = current;
			} // ctor

			public bool TryGetProperty(string name, out object value)
			{
				var p = owner.GetProperty(name, false);
				if (p != null)
				{
					value = p.GetValue(this.current);
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			} // func TryGetProperty
			
			public object this[int index]
				=> owner.properties[index].GetValue(current);

			public object this[string columnName]
				=> owner.GetProperty(columnName, true).GetValue(current);
			
			public IReadOnlyList<IDataColumn> Columns => owner.properties;
		} // class PropertyDataRow

		#endregion

		private readonly IEnumerator<T> enumerator;
		private readonly PropertyColumnInfo[] properties;

		#region -- Ctor/Dtor --------------------------------------------------------------

		public GenericDataRowEnumerator(IEnumerator<T> enumerator)
		{
			if (enumerator == null)
				throw new ArgumentNullException();

			this.enumerator = enumerator;
			this.properties = typeof(T).GetRuntimeProperties()
				.Where(pi => pi.CanRead && pi.GetMethod.IsPublic && !pi.GetMethod.IsStatic)
				.Select(pi => new PropertyColumnInfo(pi)).ToArray();
		} // ctor

		public void Dispose()
		{
			enumerator.Dispose();
		} // proc Dispose

		#endregion

		private PropertyColumnInfo GetProperty(string propertyName, bool throwException)
		{
			var property = Array.Find(properties, c => String.Compare(c.Name, propertyName, StringComparison.OrdinalIgnoreCase) == 0);
			if (property == null)
			{
				if (throwException)
					throw new ArgumentNullException("propertyName", $"Property '{propertyName}' not found.");
			}
			return property;
		} // proc GetProperty

		public void Reset()
			=> enumerator.Reset();

		public bool MoveNext()
			=> enumerator.MoveNext();

		public IReadOnlyList<IDataColumn> Columns => properties;

		public IDataRow Current => new PropertyDataRow(this, enumerator.Current);

		object IEnumerator.Current => Current;
	} // class GenericDataRowEnumerator<T>

	#endregion

	#region -- class SimpleDataColumn ---------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class SimpleDataColumn : IDataColumn
	{
		private readonly string name;
		private readonly Type dataType;
		private readonly IPropertyEnumerableDictionary attributes;

		public SimpleDataColumn(string name, Type dataType, IPropertyEnumerableDictionary attributes = null)
		{
			this.name = name;
			this.dataType = dataType;
			this.attributes = attributes ?? PropertyDictionary.EmptyReadOnly;
		} // ctor

		public string Name => name;
		public Type DataType => dataType;
		public IPropertyEnumerableDictionary Attributes => attributes;
	} // class SimpleDataColumn

	#endregion

	#region -- class DataRowHelper-------------------------------------------------------

	public static class DataRowHelper
	{
		public static int FindColumnIndex(this IEnumerator<IDataRow> columns, string columnName, bool throwException = false)
			=> FindColumnIndex((IDataColumns)columns, columnName, throwException);

		public static int FindColumnIndex(this IDataColumns columns, string columnName, bool throwException = false)
		{
			for (var i = 0; i < columns.Columns.Count; i++)
			{
				if (String.Compare(columns.Columns[i].Name, columnName, StringComparison.OrdinalIgnoreCase) == 0)
					return i;
			}
			if (throwException)
				throw new ArgumentOutOfRangeException(String.Format("Column '{0}' not found", columnName));
			return -1;
		} // func FindColumnIndex

		public static T GetValue<T>(this IEnumerator<IDataRow> items, int index, T @default, Action<T> raiseCondition = null)
		{
			if (index == -1)
				return @default;

			var value = items.Current[index];
			if (value == null)
				return @default;

			var r = value.ChangeType<T>();
			if (raiseCondition != null)
				raiseCondition(r);
			return r;
		} // func GetValue

	} // class DataRowHelper

	#endregion
}
