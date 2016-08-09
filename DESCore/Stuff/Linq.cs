using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- class Procs --------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		private static Lazy<MethodInfo> objectGetTypeMethodInfo = new Lazy<MethodInfo>(() => GetMethod(typeof(object), "GetType"));

		public static MethodInfo GetMethod(this Type type, string methodName, params Type[] parameters)
		{
			var mi = type.GetRuntimeMethod(methodName, parameters);
			if (mi == null)
				throw new ArgumentException($"Method {type.Name}.{methodName} not resolved.");
			return mi;
		} // func FindMethod

		public static PropertyInfo GetProperty(this Type type, string propertyName, params Type[] parameters)
		{
			var pi = parameters.Length == 0 ?
				type.GetRuntimeProperty(propertyName) :
				type.GetRuntimeProperties().Where(c => c.Name == propertyName && CompareParameter(c.GetIndexParameters(), parameters)).FirstOrDefault();

			if (pi == null)
				throw new ArgumentException($"Property {type.Name}.{propertyName} not resolved.");

			return pi;
		} // func GetProperty

		private static bool CompareParameter(ParameterInfo[] parameterInfo, Type[] parameters)
		{
			if (parameterInfo.Length != parameters.Length)
				return false;

			for (var i = 0; i < parameters.Length; i++)
				if (parameterInfo[i].ParameterType != parameters[i])
					return false;

			return true;
		} // func CompareParameter

		public static MethodInfo ObjectGetTypeMethodInfo => objectGetTypeMethodInfo.Value;
	} // class Procs

	#endregion
}
