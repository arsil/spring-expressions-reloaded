#region License

/*
 * Copyright © 2002-2011 the original author or authors.
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

using System.Collections;

namespace SpringExpressions.Processors
{
    /// <summary>
    /// Defines an interface that should be implemented
    /// by all collection processors and aggregators.
    /// </summary>
    /// <remarks>
    /// <p>
    /// <b><paramref name="source"/> is an <see cref="IEnumerable"/>, not an
    /// <see cref="ICollection"/> - a breaking change from upstream Spring.NET, made deliberately.</b>
    /// The non-generic <see cref="ICollection"/> is not implemented by <c>HashSet&lt;T&gt;</c>, by a
    /// declared <c>ISet&lt;T&gt;</c>, or by a bare <c>IEnumerable&lt;T&gt;</c>, so every processor
    /// refused those sources with an <see cref="System.ArgumentException"/> while the compiled path -
    /// whose first tier asks <c>IsGenericEnumerable</c> - answered. One backend answering while the
    /// other throws, decided by the caller's declared context type.
    /// </p>
    /// <p>
    /// The same split, on a different source type, as the one upstream shipped between
    /// <c>ProjectionNode</c> (<see cref="IEnumerable"/>) and this interface: two interface names typed
    /// in two files, never a decision. The interpreter now asks what the compiled path asks.
    /// </p>
    /// <p>
    /// A processor that wants a count without walking calls
    /// <c>CollectionOperandUtils.TryGetCount</c>, which tests the runtime type for the non-generic
    /// <see cref="ICollection"/> and for <c>ICollection&lt;T&gt;</c> - neither alone is enough.
    /// </p>
    /// </remarks>
    public interface ICollectionProcessor
    {
        /// <summary>
        /// Processes a sequence of source items and returns a result.
        /// </summary>
        /// <param name="source">
        /// The source sequence to process.
        /// </param>
        /// <param name="args">
        /// An optional processor arguments array.
        /// </param>
        /// <returns>
        /// The processing result.
        /// </returns>
        object Process(IEnumerable source, object[] args);
    }
}
