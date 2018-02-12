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
using System.Linq;
using System.Reflection;

namespace TecWare.DE.Stuff
{
	#region -- class Procs ------------------------------------------------------------

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

		/// <summary></summary>
		/// <param name="type"></param>
		/// <param name="methodName"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static MethodInfo GetMethod(this Type type, string methodName, params Type[] parameters)
		{
			var mi = type.GetRuntimeMethod(methodName, parameters);
			if (mi == null)
				throw new ArgumentException($"Method {type.Name}.{methodName} not resolved.");
			return mi;
		} // func GetMethod

		/// <summary></summary>
		/// <param name="type"></param>
		/// <param name="propertyName"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static PropertyInfo GetProperty(this Type type, string propertyName, params Type[] parameters)
		{
			var pi = parameters.Length == 0 ?
				type.GetRuntimeProperty(propertyName) :
				type.GetRuntimeProperties().Where(c => c.Name == propertyName && CompareParameter(c.GetIndexParameters(), parameters)).FirstOrDefault();

			if (pi == null)
				throw new ArgumentException($"Property {type.Name}.{propertyName} not resolved.");

			return pi;
		} // func GetProperty

		/// <summary></summary>
		/// <param name="type"></param>
		/// <param name="fieldName"></param>
		/// <returns></returns>
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
		
		/// <summary></summary>
		public static MethodInfo ObjectGetTypeMethodInfo => objectGetTypeMethodInfo.Value;
		/// <summary></summary>
		public static ConstructorInfo ArgumentOutOfRangeConstructorInfo2 => argumentOutOfRangeConstructorInfo.Value;
	} // class Procs

	#endregion
}
