using System;

namespace SpringExpressions
{
	/// <summary>
	/// Factory which delegates object creation to external factory/container
	/// </summary>
	public static class ReferenceObjectFactory
	{
		/// <summary>
		/// Factory method
		/// </summary>
		/// <param name="type">Type of an object</param>
		/// <param name="name">Optional nama of an object - can be null!</param>
		/// <returns>Created object or exception if none found</returns>
		public delegate object CreateObjectCallback(Type type, string name);

		/// <summary>
		/// Delegates object creation to external factory
		/// </summary>
		public static event CreateObjectCallback CreateObject;

		internal static object InvokeCreateObject(Type type, string name)
		{
			var createObjectCallback = CreateObject;
			if (createObjectCallback != null)
				return createObjectCallback(type, name);

			throw new InvalidOperationException(
				"ReferenceObjectFactory was not initialized!");
		}
	}
}
