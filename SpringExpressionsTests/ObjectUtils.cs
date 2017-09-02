#region License

/*
 * Copyright  2002-2005 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#endregion

#region Imports

using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Proxies;
using SpringReflection.Dynamic;

#endregion

namespace SpringUtil
{
	/// <summary>
	/// Helper methods with regard to objects, types, properties, etc.
	/// </summary>
	/// <remarks>
	/// <p>
	/// Not intended to be used directly by applications.
	/// </p>
	/// </remarks>
	/// <author>Rod Johnson</author>
	/// <author>Juergen Hoeller</author>
	/// <author>Rick Evans (.NET)</author>
	sealed class ObjectUtils
	{
		/// <summary>
		/// An empty object array.
		/// </summary>
		public static readonly object[] EmptyObjects = new object[] { };

		private static MethodInfo GetHashCodeMethodInfo = null;
	}
}
