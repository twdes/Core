using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;

namespace TecWare.DE.Stuff
{
	#region -- class LuaPropertiesTable --------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public class LuaPropertiesTable : LuaTable
	{
		private PropertyDictionary properties;

		public LuaPropertiesTable(PropertyDictionary properties)
		{
			if (properties == null)
				throw new ArgumentNullException("properties");

			this.properties = properties;
		} // ctor
		
		protected override object OnIndex(object key) => base.OnIndex(key) ?? properties?.GetProperty(key?.ToString(), null);
	} // class LuaPropertiesTable

	#endregion

	public static partial class Procs
	{
	} // class Procs
}
