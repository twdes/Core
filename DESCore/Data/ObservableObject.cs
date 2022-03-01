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
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TecWare.DE.Data
{
	#region -- class ObservableObject -------------------------------------------------

	/// <summary>Simple base class <see cref="INotifyPropertyChanged"/> implementation.</summary>
	public class ObservableObject : INotifyPropertyChanged
	{
		/// <summary>A property value is changed.</summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>Invokes PropertyChanged.</summary>
		/// <param name="propertyName">Name of the property.</param>
		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (String.IsNullOrEmpty(propertyName))
				throw new ArgumentNullException();

			InvokePropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		} // proc OnPropertyChanged

		/// <summary>Invokes PropertyChanged.</summary>
		/// <param name="_">Sender of the property change event. The <c>sender</c> is changed <c>this</c>.</param>
		/// <param name="args">Property change event arguments.</param>
		protected void InvokePropertyChanged(object _, PropertyChangedEventArgs args)
			=> PropertyChanged?.Invoke(this, args);

		/// <summary>Set a property and invoke property changed event.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">Reference to the member or variable, that holds the value.</param>
		/// <param name="newValue">Value to set.</param>
		/// <param name="getPropertyName">Function that returns the propery name.</param>
		/// <returns><c>true</c>, if the values is changed.</returns>
		protected bool Set<T>(ref T value, T newValue, Func<string> getPropertyName)
		{
			if (EqualityComparer<T>.Default.Equals(value, newValue))
				return false; // Keine Änderung

			value = newValue;
			OnPropertyChanged(getPropertyName());
			return true;
		} // func Set

		/// <summary>Set a property and invoke property changed event.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">Reference to the member or variable, that holds the value.</param>
		/// <param name="newValue">Value to set.</param>
		/// <param name="propertyName">Name of the property.</param>
		/// <returns><c>true</c>, if the values is changed.</returns>
		protected bool Set<T>(ref T value, T newValue, [CallerMemberName] string propertyName = null)
			=> Set(ref value, newValue, () => propertyName);

		/// <summary>Set a property and invoke property changed event.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="propertyExpression"></param>
		/// <param name="value">Reference to the member or variable, that holds the value.</param>
		/// <param name="newValue">Value to set.</param>
		/// <returns><c>true</c>, if the values is changed.</returns>
		[Obsolete("use nameof instead.")]
		protected bool Set<T>(Expression<Func<T>> propertyExpression, ref T value, T newValue)
		{
			if (propertyExpression == null)
				throw new ArgumentNullException(nameof(propertyExpression));

			return Set(ref value, newValue, () => ((PropertyInfo)((MemberExpression)(propertyExpression.Body)).Member).Name);
		} // func Set
	} // class ObservableObject

	#endregion
}
