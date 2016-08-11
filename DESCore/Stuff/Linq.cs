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
		private static Lazy<ConstructorInfo> argumentOutOfRangeConstructorInfo =
			new Lazy<ConstructorInfo>(
				() =>
					(
						from ci in typeof(ArgumentOutOfRangeException).GetTypeInfo().DeclaredConstructors
						let p = ci.GetParameters()
						where p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(string)
						select ci
				).First()
		);

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

		public static FieldInfo GetField(this Type type, string fieldName)
		{
			var fi =  type.GetRuntimeField(fieldName);

			if (fi == null)
				throw new ArgumentException($"Property {type.Name}.{fieldName} not resolved.");

			return fi;
		} // func GetField

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
		public static ConstructorInfo ArgumentOutOfRangeConstructorInfo2 => argumentOutOfRangeConstructorInfo.Value;
	} // class Procs

	#endregion
}
