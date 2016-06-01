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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.DE.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataColumnAttributes : IPropertyReadOnlyDictionary, IEnumerable<PropertyValue>
	{
	} // interface IDataColumnAttributes

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataColumn
	{
		/// <summary>Gets the name in this column.</summary>
		string Name { get; }
		/// <summary>Gets the type of data in this column.</summary>
		Type DataType { get; }
		/// <summary>Gets the attributes in this column.</summary>
		IDataColumnAttributes Attributes { get; }
	} // interface IDataColumn

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataColumns
	{
		/// <summary>Gets the columns.</summary>
		IDataColumn[] Columns { get; }
		/// <summary>Gets the number of columns.</summary>
		int ColumnCount { get; }
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

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataRow : IDataColumns, IDataValues, IPropertyReadOnlyDictionary
	{
		/// <summary>Gets the value for the column with the specified name.</summary>
		/// <param name="columnName">The name of the column to get the value.</param>
		/// <returns></returns>
		object this[string columnName] { get; }
	} // interface IDataRow
}
