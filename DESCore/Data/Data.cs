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
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	#region -- interface IDataColumn --------------------------------------------------

	/// <summary></summary>
	public interface IDataColumn
	{
		/// <summary>Gets the name in this column.</summary>
		string Name { get; }
		/// <summary>Gets the type of data in this column.</summary>
		Type DataType { get; }
		/// <summary>Extented attributes for the column.</summary>
		IPropertyEnumerableDictionary Attributes { get; }
	} // interface IDataColumn

	#endregion

	#region -- interface IDataColumns -------------------------------------------------

	/// <summary></summary>
	public interface IDataColumns
	{
		/// <summary>Gets the columns.</summary>
		IReadOnlyList<IDataColumn> Columns { get; }
	} // interface IDataColumns

	#endregion

	#region -- interface IDataValues --------------------------------------------------

	/// <summary></summary>
	public interface IDataValues
	{
		/// <summary>Gets the value for the column at the specified index.</summary>
		/// <param name="index">The zero-based index of the column to get the value.</param>
		/// <returns></returns>
		object this[int index] { get; }
	} // interface IDataValues

	#endregion

	#region -- interface IDataRow -----------------------------------------------------

	/// <summary></summary>
	public interface IDataRow : IDataColumns, IDataValues, IPropertyReadOnlyDictionary
	{
		/// <summary><c>true</c>, if this objects holds the data. <c>false</c>, for a data window (the data changes on a move next).</summary>
		bool IsDataOwner { get; }
		/// <summary>Gets the value for the column with the specified name.</summary>
		/// <param name="columnName">The name of the column to get the value.</param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		object this[string columnName, bool throwException = true] { get; }
	} // interface IDataRow

	#endregion

	#region -- class DynamicDataRow ---------------------------------------------------

	/// <summary>Implements the dynamic access of the IDataRow members.</summary>
	public abstract class DynamicDataRow : IDataRow, IDynamicMetaObjectProvider
	{
		#region -- class DynamicDataRowMetaObjectProvider -----------------------------

		private sealed class DynamicDataRowMetaObjectProvider : DynamicMetaObject
		{
			public DynamicDataRowMetaObjectProvider(Expression expression, DynamicDataRow row)
				: base(expression, BindingRestrictions.Empty, row)
			{
			} // ctor

			private DynamicMetaObject BindMember(string name, bool throwException)
			{
				var row = (DynamicDataRow)Value;
				var restriction = row.GetRowBindingRestriction(Expression);
				if (restriction != null) // there is a row restriction, use the this[int]
				{
					var column = row.FindColumnIndex(name);
					if (column == -1) // column not found, return a null
					{
						if (throwException)
						{
							return new DynamicMetaObject(
								Expression.Throw(
									Expression.New(Procs.ArgumentOutOfRangeConstructorInfo2,
										new Expression[]
										{
											Expression.Constant(name),
											Expression.Constant(String.Format("Could not resolve {0}.",name))
										}
									), typeof(object)
								), restriction
							);
						}
						else
							return new DynamicMetaObject(Expression.Constant(null, typeof(object)), restriction);
					}
					else // the index of the column
					{
						return new DynamicMetaObject(
							Expression.MakeIndex(
								Expression.Convert(Expression, typeof(DynamicDataRow)),
								thisIntPropertyInfo,
								new Expression[] { Expression.Constant(column) }
							),
							restriction
						);
					}
				}
				else // use the this[string]
				{
					return new DynamicMetaObject(
						Expression.MakeIndex(
							Expression.Convert(Expression, typeof(DynamicDataRow)),
							thisStringPropertyInfo,
							new Expression[] { Expression.Constant(name), Expression.Constant(throwException) }
						),
						BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, typeof(DynamicDataRow)))
					);
				}
			} // func BindMember

			public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
			{
				if (binder.Name == nameof(Columns))
					return base.BindGetMember(binder);
				else
					return BindMember(binder.Name, false);
			} // func BindGetMember

			private static bool IsPublicMember(Type type, string name)
			{
				return (
					from mi in type.GetRuntimeMethods()
					where mi.IsPublic && !mi.IsStatic
					select mi.Name
				).Union(
					from pi in type.GetRuntimeProperties()
					where pi.GetMethod != null && pi.GetMethod.IsPublic && !pi.GetMethod.IsStatic
					select pi.Name
				).Contains(name);
			} // proc IsPublicMember

			public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
				=> args.Length > 0  // optimization: arguments should be a method call, we only add properties dynamic
				|| binder.Name == nameof(Columns) // optimization: columns is a known public properties
				|| IsPublicMember(LimitType, binder.Name) // test for non-dynamic members (methods, properties)
					? base.BindInvokeMember(binder, args)
					: BindMember(binder.Name, true);
		} // class DynamicDataRowMetaObjectProvider

		#endregion

		#region -- IDynamicMetaObjectProvider -----------------------------------------

		DynamicMetaObject IDynamicMetaObjectProvider.GetMetaObject(Expression parameter)
			=> new DynamicDataRowMetaObjectProvider(parameter, this);

		#endregion

		#region -- IDataRow -----------------------------------------------------------

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public virtual bool TryGetProperty(string columnName, out object value)
		{
			if (String.IsNullOrEmpty(columnName)
				|| Columns == null || Columns.Count < 1)
			{
				value = null;
				return false;
			}

			var index = this.FindColumnIndex(columnName);
			if (index == -1)
			{
				value = null;
				return false;
			}

			value = this[index];
			return true;
		} // func TryGetProperty

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <returns></returns>
		public object this[string columnName]
			=> this[columnName, false];

		/// <summary></summary>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public object this[string columnName, bool throwException]
		{
			get
			{
				if (TryGetProperty(columnName, out var value))
					return value;
				else if (throwException)
					throw new ArgumentException(String.Format("Column with name \"{0}\" not found.", columnName ?? "null"));
				else
					return null;
			}
		} // prop this

		/// <summary>Create a restriction to identify this datarow type.</summary>
		/// <param name="expression">Expression to the DataRow (uncasted).</param>
		/// <returns>Binding restriction, to check this DataRow-type.</returns>
		protected virtual BindingRestrictions GetRowBindingRestriction(Expression expression)
			=> null;

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public abstract object this[int index] { get; }
		/// <summary></summary>
		public abstract bool IsDataOwner { get; }

		/// <summary></summary>
		public abstract IReadOnlyList<IDataColumn> Columns { get; }

		#endregion

		// -- Static --------------------------------------------------------------

		private readonly static PropertyInfo thisStringPropertyInfo;
		private readonly static PropertyInfo thisIntPropertyInfo;

		static DynamicDataRow()
		{
			var ti = typeof(DynamicDataRow);
			thisStringPropertyInfo = ti.GetProperty("Item", new Type[] { typeof(string), typeof(bool) });
			thisIntPropertyInfo = ti.GetProperty("Item", new Type[] { typeof(int) });
		} // sctor
	} // class DynamicDataRow

	#endregion

	#region -- class SimpleDataRow ----------------------------------------------------

	/// <summary>Simple implementation for the DynamicDataRow</summary>
	public sealed class SimpleDataRow : DynamicDataRow
	{
		private readonly object[] values;
		private readonly SimpleDataColumn[] columns;

		/// <summary></summary>
		/// <param name="values"></param>
		/// <param name="columns"></param>
		public SimpleDataRow(object[] values, SimpleDataColumn[] columns)
		{
			this.values = values ?? throw new ArgumentNullException(nameof(values));
			this.columns = columns ?? throw new ArgumentNullException(nameof(values));
		} // ctor

		/// <summary></summary>
		/// <param name="row"></param>
		public SimpleDataRow(IDataRow row)
		{
			var length = row.Columns.Count;

			this.values = new object[length];
			this.columns = new SimpleDataColumn[length];

			for (var i = 0; i < length; i++)
			{
				values[i] = row[i];
				columns[i] = new SimpleDataColumn(row.Columns[i]);
			}
		} // ctor

		/// <summary></summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public override object this[int index] => values[index];
		/// <summary></summary>
		public override bool IsDataOwner => true;
		/// <summary></summary>
		public override IReadOnlyList<IDataColumn> Columns => columns;
	} // class SimpleDataRow

	#endregion

	#region -- class GenericDataRowEnumerator<T> --------------------------------------

	/// <summary>IDataRow implementation for an type.</summary>
	public sealed class GenericDataRowEnumerator<T> : IEnumerator<IDataRow>, IDataColumns
	{
		#region -- class PropertyColumnInfo -------------------------------------------

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

		#region -- class PropertyDataRow ----------------------------------------------

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

			public object this[string columnName, bool throwException]
				=> owner.GetProperty(columnName, throwException).GetValue(current);

			public bool IsDataOwner => false;
			public IReadOnlyList<IDataColumn> Columns => owner.properties;
		} // class PropertyDataRow

		#endregion

		private readonly IEnumerator<T> enumerator;
		private readonly PropertyColumnInfo[] properties;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="enumerator"></param>
		public GenericDataRowEnumerator(IEnumerator<T> enumerator)
		{
			this.enumerator = enumerator ?? throw new ArgumentNullException();
			this.properties = GetColumnInfoCore();
		} // ctor

		/// <summary></summary>
		public void Dispose()
			=> enumerator.Dispose();
		
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

		/// <summary></summary>
		public void Reset()
			=> enumerator.Reset();

		/// <summary></summary>
		/// <returns></returns>
		public bool MoveNext()
			=> enumerator.MoveNext();

		/// <summary></summary>
		public IReadOnlyList<IDataColumn> Columns => properties;

		/// <summary></summary>
		public IDataRow Current => new PropertyDataRow(this, BaseCurrent);
		/// <summary></summary>
		public T BaseCurrent => enumerator.Current;

		object IEnumerator.Current => Current;

		private static PropertyColumnInfo[] GetColumnInfoCore()
			=> typeof(T).GetRuntimeProperties()
				.Where(pi => pi.CanRead && pi.GetMethod.IsPublic && !pi.GetMethod.IsStatic)
				.Select(pi => new PropertyColumnInfo(pi)).ToArray();

		/// <summary></summary>
		/// <returns></returns>
		public static IDataColumn[] GetColumnInfo()
			=> GetColumnInfoCore();
	} // class GenericDataRowEnumerator<T>

	#endregion

	#region -- class SimpleDataColumn -------------------------------------------------

	/// <summary>Simple column implementation.</summary>
	public class SimpleDataColumn : IDataColumn
	{
		private readonly string name;
		private readonly Type dataType;
		private readonly IPropertyEnumerableDictionary attributes;

		/// <summary></summary>
		/// <param name="column"></param>
		public SimpleDataColumn(IDataColumn column)
			: this(column.Name, column.DataType, column.Attributes)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="dataType"></param>
		/// <param name="attributes"></param>
		public SimpleDataColumn(string name, Type dataType, IPropertyEnumerableDictionary attributes = null)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			this.name = name;
			this.dataType = dataType ?? throw new ArgumentNullException("dataType");
			this.attributes = attributes ?? PropertyDictionary.EmptyReadOnly;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> $"Column: {name} ({dataType.Name})";

		/// <summary></summary>
		public string Name => name;
		/// <summary></summary>
		public Type DataType => dataType;
		/// <summary></summary>
		public IPropertyEnumerableDictionary Attributes => attributes;
	} // class SimpleDataColumn

	#endregion

	#region -- class SimpleDataColumns ------------------------------------------------

	/// <summary></summary>
	public class SimpleDataColumns : IDataColumns
	{
		private readonly IDataColumn[] columns;

		/// <summary></summary>
		/// <param name="columns"></param>
		public SimpleDataColumns(params IDataColumn[] columns)
		{
			this.columns = columns ?? throw new ArgumentNullException(nameof(columns));
		} // ctor

		/// <summary></summary>
		public IReadOnlyList<IDataColumn> Columns => columns;
	} // class SimpleDataColumns

	#endregion

	#region -- class DataRowHelper-----------------------------------------------------

	/// <summary></summary>
	public static class DataRowHelper
	{
		/// <summary></summary>
		/// <param name="columns"></param>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static int FindColumnIndex(this IEnumerator<IDataRow> columns, string columnName, bool throwException = false)
			=> FindColumnIndex((IDataColumns)columns, columnName, throwException);

		/// <summary></summary>
		/// <param name="columns"></param>
		/// <param name="columnName"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
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
		  /// <summary></summary>
		  /// <param name="columns"></param>
		  /// <param name="throwException"></param>
		  /// <param name="columnNames"></param>
		  /// <returns></returns>
		public static int[] FindColumnIndices(this IDataColumns columns, bool throwException, params string[] columnNames)
		{
			// init result
			var idx = new int[columnNames.Length];
			for (var i = 0; i < idx.Length; i++)
				idx[i] = -1;

			// match columns
			for (var i = 0; i < columns.Columns.Count; i++)
			{
				var n = columns.Columns[i].Name;
				var j = Array.FindIndex(columnNames, c => String.Compare(n, c, StringComparison.OrdinalIgnoreCase) == 0);
				if (j != -1)
					idx[j] = i;
			}

			// return values
			for (var i = 0; i < idx.Length; i++)
			{
				if (idx[i] == -1)
					throw new ArgumentOutOfRangeException(nameof(columnNames), columnNames[i], $"Column '{columnNames[i]}' not found.");
			}

			return idx;
		} // func FindColumnIndices

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="row"></param>
		/// <param name="index"></param>
		/// <param name="default"></param>
		/// <param name="raiseCondition"></param>
		/// <returns></returns>
		public static T GetValue<T>(this IDataValues row, int index, T @default, Action<T> raiseCondition = null)
		{
			if (index == -1)
				return @default;

			var value = row[index];
			if (value == null)
				return @default;

			var r = value.ChangeType<T>();
			raiseCondition?.Invoke(r);
			return r;
		} // func GetGetValue

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="items"></param>
		/// <param name="index"></param>
		/// <param name="default"></param>
		/// <param name="raiseCondition"></param>
		/// <returns></returns>
		public static T GetValue<T>(this IEnumerator<IDataRow> items, int index, T @default, Action<T> raiseCondition = null)
			=> GetValue<T>(items.Current, index, @default, raiseCondition);

		#region -- ToMyData -----------------------------------------------------------

		/// <summary>If the row is not owner of the data, the row data is copied in a SimpleDataRow.</summary>
		/// <param name="row"></param>
		/// <returns></returns>
		public static IDataRow ToMyData(this IDataRow row)
			=> row.IsDataOwner ? row : new SimpleDataRow(row);

		#endregion
	} // class DataRowHelper

	#endregion
}
