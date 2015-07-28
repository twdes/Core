using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo.IronLua;

namespace TecWare.DES.Stuff
{
	public static partial class Procs
	{
		public static LuaTable CreateLuaTable(params KeyValuePair<string, object>[] args)
		{
			var t = new LuaTable();

			foreach (var c in args)
				t[c.Key] = c.Value;

			return t;
		} // func CreateLuaTable
	} // class Procs
}
