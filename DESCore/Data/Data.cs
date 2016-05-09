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
	public interface IDataColumns
	{
		/// <summary>Name of the columns</summary>
		string[] ColumnNames { get; }
		/// <summary></summary>
		Type[] ColumnTypes { get; }
		/// <summary>Number of columns</summary>
		int ColumnCount { get; }
	} // interface IDataColumns

	public interface IDataValues
	{
		/// <summary>Get the value for the column by index</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		object this[int index] { get; }
	} // interface IDataValues

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataRow : IDataColumns, IDataValues, IPropertyReadOnlyDictionary
	{
		/// <summary>Get the value for the column by name</summary>
		/// <param name="columnName"></param>
		/// <returns></returns>
		object this[string columnName] { get; }
	} // interface IDataRow
}
