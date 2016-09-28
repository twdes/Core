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
using System.Xml.Linq;
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

	#region -- class Procs --------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		private static void AddValue(object value, XElement c)
		{
			var t = value as LuaTable;
			if (t != null)
			{
				c.Add(new XAttribute("t", "table"));
				ToXml(t, c);
			}
			else if (value != null)
			{
				var type = value.GetType();
				c.Add(new XAttribute("t", LuaType.GetType(type).AliasOrFullName));
				c.Add(new XText(value.ChangeType<string>()));
			}
		} // proc AddValue

		public static void ToXml(this LuaTable table, XElement xTarget)
		{
			// emit the members
			foreach (var o in table.Members)
			{
				var c = new XElement("m", new XAttribute("n", o.Key));
				AddValue(o.Value, c);
				xTarget.Add(c);
			}

			// emit the the array elements
			foreach (var o in table.ArrayList)
			{
				var c = new XElement("i");
				AddValue(o, c);
				xTarget.Add(c);
			}
		} // proc ToXml

		public static XElement ToXml(this LuaTable table)
		{
			var x = new XElement("table");
			ToXml(table, x);
			return x;
		} // func ToXml

		private static object GetValue(XElement x)
		{
			var type = LuaType.GetType(x.Attribute("t")?.Value ?? "string", lateAllowed: false).Type;

			if (type == typeof(LuaTable))
				return CreateLuaTable(x);
			else
				return Procs.ChangeType(x.Value, type);
		} // func GetValue
		
		public static LuaTable CreateLuaTable(XElement x)
		{
			var t = new LuaTable();

			// ignore root element
			foreach (var c in x.Elements())
			{
				if (c.Name == "m") // member
				{
					var name = c.Attribute("n")?.Value;
					if (name != null)
						t.SetMemberValue(name, GetValue(c), lRawSet: true);
				}
				else if (c.Name == "i")
				{
					t.ArrayList.Add(GetValue(c));
				}
			}

			return t;
		} // func CreateLuaTable

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

		public static T ReturnOptionalValue<T>(this LuaTable table, string name, T @default, bool ignoreCase = false, bool rawGet = false)
		{
			var value = table.GetMemberValue(name, ignoreCase, rawGet);
			if (value == null)
				return @default;
			else
			{
				if (Lua.RtInvokeable(value))
					value = new LuaResult(Lua.RtInvoke(value))[0];

				if (value == null)
					return @default;
				try
				{
					return value.ChangeType<T>();
				}
				catch
				{
					return @default;
				}
			}
		} // func ReturnOptionalValue
	} // class Procs

	#endregion
}
