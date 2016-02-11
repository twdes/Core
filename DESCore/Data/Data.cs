using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TecWare.DE.Stuff;

namespace TecWare.DES.Data
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataRow : IPropertyReadOnlyDictionary
	{
		/// <summary>Name of the columns</summary>
		string[] ColumnNames { get; }
		/// <summary></summary>
		Type[] ColumnTypes { get; }
		/// <summary>Number of columns</summary>
		int ColumnCount { get; }

		/// <summary>Get the value for the column by index</summary>
		/// <param name="index"></param>
		/// <returns></returns>
		object this[int index] { get; }
		/// <summary>Get the value for the column by name</summary>
		/// <param name="columnName"></param>
		/// <returns></returns>
		object this[string columnName] { get; }
	} // interface IDataRow
}
