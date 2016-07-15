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
using Neo.IronLua;
using TecWare.DE.Data;

namespace TecWare.DE.Stuff
{
	#region -- class LuaPropertiesTable --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaPropertiesTable : LuaTable
	{
		private IPropertyReadOnlyDictionary properties;

		public LuaPropertiesTable(IPropertyReadOnlyDictionary properties)
		{
			if (properties == null)
				throw new ArgumentNullException("properties");

			this.properties = properties;
		} // ctor
		
		protected override object OnIndex(object key)
			=> base.OnIndex(key) ?? properties?.GetProperty(key?.ToString(), null);
	} // class LuaPropertiesTable

	#endregion

	public static partial class Procs
	{
		public static LuaTable CreateLuaTable(params PropertyValue[] values)
		{
			var t = new LuaTable();
			foreach (var c in values)
			{
				if (c.Value != null)
					t[c.Name] = c.Value;
			}
			return t;
		} // func CreateTable

		public static LuaTable CreateLuaTable(IDataRow values)
		{
			var t = new LuaTable();
			for(var i=0;i<values.Columns.Count;i++)
			{
				var v = values[i];
				if (v != null)
					t[values.Columns[i].Name] = v;
			}
			return t;
		} // func CreateTable
		
		public static int CompareStringKey(object key, string other)
		{
			if (key == null && other == null)
				return 0;
			else if (key is string)
			{
				if (other == null)
					return 1;
				else
					return String.Compare((string)key, other, StringComparison.OrdinalIgnoreCase);
			}
			else
				return -1;
		} // CompareStringKey
	} // class Procs
}
