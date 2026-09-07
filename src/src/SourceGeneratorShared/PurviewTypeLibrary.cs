using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Purview.SourceGeneratorFramework;

/// <summary>
/// Provides common <see cref="TypeIdentity"/> instances for use by source generators.
/// </summary>
[SuppressMessage("Design", "CA1034:Nested types should not be visible")]
[SuppressMessage("Naming", "CA1724:Type names should not match namespaces")]
[SuppressMessage("Naming", "CA1720:Identifier contains type name")]
public static partial class PurviewTypeLibrary
{
	/// <summary>
	/// Common types from the <c>System</c> namespace.
	/// </summary>
	public static class System
	{
		/// <summary>
		/// The <c>System</c> namespace.
		/// </summary>
		public const string Namespace = "System";

		/// <summary>
		/// <see cref="Attribute"/>.
		/// </summary>
		public static readonly TypeIdentity Attribute = TypeIdentity.Create<Attribute>();

		/// <summary>
		/// <see cref="Type"/>.
		/// </summary>
		public static readonly TypeIdentity Type = TypeIdentity.Create<Type>();

		/// <summary>
		/// <see cref="bool"/>.
		/// </summary>
		public static readonly TypeIdentity Boolean = TypeIdentity.Create<bool>();

		/// <summary>
		/// <see cref="byte"/>.
		/// </summary>
		public static readonly TypeIdentity Byte = TypeIdentity.Create<byte>();

		/// <summary>
		/// <see cref="sbyte"/>.
		/// </summary>
		public static readonly TypeIdentity SByte = TypeIdentity.Create<sbyte>();

		/// <summary>
		/// <see cref="char"/>.
		/// </summary>
		public static readonly TypeIdentity Char = TypeIdentity.Create<char>();

		/// <summary>
		/// <see cref="decimal"/>.
		/// </summary>
		public static readonly TypeIdentity Decimal = TypeIdentity.Create<decimal>();

		/// <summary>
		/// <see cref="double"/>.
		/// </summary>
		public static readonly TypeIdentity Double = TypeIdentity.Create<double>();

		/// <summary>
		/// <see cref="float"/>.
		/// </summary>
		public static readonly TypeIdentity Float = TypeIdentity.Create<float>();

		/// <summary>
		/// <see cref="int"/>.
		/// </summary>
		public static readonly TypeIdentity Int32 = TypeIdentity.Create<int>();

		/// <summary>
		/// <see cref="uint"/>.
		/// </summary>
		public static readonly TypeIdentity UInt32 = TypeIdentity.Create<uint>();

		/// <summary>
		/// <see cref="long"/>.
		/// </summary>
		public static readonly TypeIdentity Int64 = TypeIdentity.Create<long>();

		/// <summary>
		/// <see cref="ulong"/>.
		/// </summary>
		public static readonly TypeIdentity UInt64 = TypeIdentity.Create<ulong>();

		/// <summary>
		/// <see cref="short"/>.
		/// </summary>
		public static readonly TypeIdentity Int16 = TypeIdentity.Create<short>();

		/// <summary>
		/// <see cref="ushort"/>.
		/// </summary>
		public static readonly TypeIdentity UInt16 = TypeIdentity.Create<ushort>();

		/// <summary>
		/// <see cref="string"/>.
		/// </summary>
		public static readonly TypeIdentity String = TypeIdentity.Create<string>();

		/// <summary>
		/// <see cref="object"/>.
		/// </summary>
		public static readonly TypeIdentity Object = TypeIdentity.Create<object>();

		/// <summary>
		/// <see cref="void"/>.
		/// </summary>
		public static readonly TypeIdentity Void = new("void", null);

		/// <summary>
		/// The C# <c>null</c> literal, presented as an identity for use in value positions.
		/// </summary>
		/// <remarks>
		/// Renders as <c>null</c> and converts to the <c>"null"</c> expression where a string value is
		/// expected, such as an initializer, default value, argument or return value. It is not a type and
		/// is rejected in type positions.
		/// </remarks>
		public static readonly TypeIdentity Null = TypeIdentity.Null;

		/// <summary>
		/// <see cref="nint"/>.
		/// </summary>
		public static readonly TypeIdentity IntPtr = TypeIdentity.Create<nint>();

		/// <summary>
		/// <see cref="nuint"/>.
		/// </summary>
		public static readonly TypeIdentity UIntPtr = TypeIdentity.Create<nuint>();

		/// <summary>
		/// <see cref="global::System.Action"/>.
		/// </summary>
		public static readonly TypeIdentity Action = TypeIdentity.Create<Action>();

		/// <summary>
		/// The <c>Func&lt;TResult&gt;</c> open generic definition, the lowest arity in the
		/// <c>Func</c> family. Use <see cref="TypeIdentity.WithArity(int)"/> for higher arities, up to
		/// <c>Func&lt;T1…T16, TResult&gt;</c> (arity 17).
		/// </summary>
		public static readonly TypeIdentity Func = new(nameof(Func), Namespace, 1);

		/// <summary>
		/// <see cref="global::System.Exception"/>.
		/// </summary>
		public static readonly TypeIdentity Exception = TypeIdentity.Create<Exception>();

		/// <summary>
		/// <see cref="global::System.IDisposable"/>.
		/// </summary>
		public static readonly TypeIdentity IDisposable = TypeIdentity.Create<IDisposable>();

		/// <summary>
		/// <c>System.IAsyncDisposable</c>.
		/// </summary>
		public static readonly TypeIdentity IAsyncDisposable = new(nameof(IAsyncDisposable), "System");

		/// <summary>
		/// <see cref="DateTimeOffset"/>.
		/// </summary>
		public static readonly TypeIdentity DateTimeOffset = TypeIdentity.Create<DateTimeOffset>();

		/// <summary>
		/// <see cref="DateTime"/>.
		/// </summary>
		public static readonly TypeIdentity DateTime = TypeIdentity.Create<DateTime>();

		/// <summary>
		/// <see cref="TimeSpan"/>.
		/// </summary>
		public static readonly TypeIdentity TimeSpan = TypeIdentity.Create<TimeSpan>();

		/// <summary>
		/// <c>DateOnly</c>
		/// </summary>
		public static readonly TypeIdentity DateOnly = new(nameof(DateOnly), "System");

		/// <summary>
		/// <c>TimeOnly</c>
		/// </summary>
		public static readonly TypeIdentity TimeOnly = new(nameof(TimeOnly), "System");

		/// <summary>
		/// The <c>System.Collections</c> namespace.
		/// </summary>
		public static partial class Collections
		{
			/// <summary>
			/// The <c>System.Collections</c> namespace.
			/// </summary>
			public const string Namespace = "System.Collections";

			/// <summary>
			///  The <c>System.Collections.Generic</c> namespace.
			/// </summary>
			public static partial class Generic
			{
				public const string Namespace = "System.Collections.Generic";

				/// <summary>
				/// <see cref="IEnumerable{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IEnumerable = new(typeof(IEnumerable<>));

				/// <summary>
				/// <see cref="IList{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IList = new(typeof(IList<>));

				/// <summary>
				/// <see cref="ICollection{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ICollection = new(typeof(ICollection<>));

				/// <summary>
				/// <see cref="IReadOnlyCollection{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IReadOnlyCollection = new(typeof(IReadOnlyCollection<>));

				/// <summary>
				/// <see cref="IReadOnlyList{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IReadOnlyList = new(typeof(IReadOnlyList<>));

				/// <summary>
				/// The <c>IReadOnlySet{T}</c> interface. Declared by name because the type is not available to the
				/// <c>netstandard2.0</c> build of this assembly.
				/// </summary>
				public static readonly TypeIdentity IReadOnlySet = new(nameof(IReadOnlySet), Namespace, 1);

				/// <summary>
				/// <see cref="IEnumerator{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IEnumerator = new(typeof(IEnumerator<>));

				/// <summary>
				/// <see cref="List{T}"/>.
				/// </summary>
				public static readonly TypeIdentity List = new(typeof(List<>));

				/// <summary>
				/// <see cref="IDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity IDictionary = new(typeof(IDictionary<,>));

				/// <summary>
				/// <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity IReadOnlyDictionary = new(typeof(IReadOnlyDictionary<,>));

				/// <summary>
				/// <see cref="Dictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity Dictionary = new(typeof(Dictionary<,>));

				/// <summary>
				/// <see cref="ISet{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ISet = new(typeof(ISet<>));

				/// <summary>
				/// <see cref="HashSet{T}"/>.
				/// </summary>
				public static readonly TypeIdentity HashSet = new(typeof(HashSet<>));

				/// <summary>
				/// <see cref="SortedDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity SortedDictionary = new(typeof(SortedDictionary<,>));

				/// <summary>
				/// <see cref="SortedSet{T}"/>.
				/// </summary>
				public static readonly TypeIdentity SortedSet = new(typeof(SortedSet<>));

				/// <summary>
				/// <see cref="SortedList{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity SortedList = new(typeof(SortedList<,>));

				/// <summary>
				/// <see cref="LinkedList{T}"/>.
				/// </summary>
				public static readonly TypeIdentity LinkedList = new(typeof(LinkedList<>));

				/// <summary>
				/// <see cref="Queue{T}"/>.
				/// </summary>
				public static readonly TypeIdentity Queue = new(typeof(Queue<>));

				/// <summary>
				/// <see cref="Stack{T}"/>.
				/// </summary>
				public static readonly TypeIdentity Stack = new(typeof(Stack<>));

				/// <summary>
				/// <see cref="KeyValuePair{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity KeyValuePair = new(typeof(KeyValuePair<,>));
			}

			/// <summary>
			/// The <c>System.Collections.Concurrent</c> namespace.
			/// </summary>
			public static partial class Concurrent
			{
				public const string Namespace = "System.Collections.Concurrent";

				/// <summary>
				/// <see cref="ConcurrentDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity ConcurrentDictionary = new(typeof(ConcurrentDictionary<,>));

				/// <summary>
				/// <see cref="ConcurrentQueue{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ConcurrentQueue = new(typeof(ConcurrentQueue<>));

				/// <summary>
				/// <see cref="ConcurrentStack{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ConcurrentStack = new(typeof(ConcurrentStack<>));

				/// <summary>
				/// <see cref="ConcurrentBag{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ConcurrentBag = new(typeof(ConcurrentBag<>));

				/// <summary>
				/// <see cref="BlockingCollection{T}"/>.
				/// </summary>
				public static readonly TypeIdentity BlockingCollection = new(typeof(BlockingCollection<>));

				/// <summary>
				/// <see cref="IProducerConsumerCollection{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IProducerConsumerCollection = new(
					typeof(IProducerConsumerCollection<>)
				);
			}

			/// <summary>
			/// The <c>System.Collections.ObjectModel</c> namespace.
			/// </summary>
			public static partial class ObjectModel
			{
				public const string Namespace = "System.Collections.ObjectModel";

				/// <summary>
				/// <see cref="Collection{T}"/>.
				/// </summary>
				public static readonly TypeIdentity Collection = new(typeof(Collection<>));

				/// <summary>
				/// <see cref="ReadOnlyCollection{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ReadOnlyCollection = new(typeof(ReadOnlyCollection<>));

				/// <summary>
				/// <see cref="ReadOnlyDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity ReadOnlyDictionary = new(typeof(ReadOnlyDictionary<,>));

				/// <summary>
				/// <see cref="KeyedCollection{TKey, TItem}"/>.
				/// </summary>
				public static readonly TypeIdentity KeyedCollection = new(typeof(KeyedCollection<,>));

				/// <summary>
				/// <see cref="ObservableCollection{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ObservableCollection = new(typeof(ObservableCollection<>));
			}

			/// <summary>
			/// The <c>System.Collections.Immutable</c> namespace.
			/// </summary>
			public static partial class Immutable
			{
				public const string Namespace = "System.Collections.Immutable";

				/// <summary>
				/// <see cref="ImmutableArray{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableArray = new(typeof(ImmutableArray<>));

				/// <summary>
				/// <see cref="ImmutableList{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableList = new(typeof(ImmutableList<>));

				/// <summary>
				/// <see cref="ImmutableDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableDictionary = new(typeof(ImmutableDictionary<,>));

				/// <summary>
				/// <see cref="ImmutableHashSet{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableHashSet = new(typeof(ImmutableHashSet<>));

				/// <summary>
				/// <see cref="ImmutableSortedDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableSortedDictionary = new(
					typeof(ImmutableSortedDictionary<,>)
				);

				/// <summary>
				/// <see cref="ImmutableSortedSet{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableSortedSet = new(typeof(ImmutableSortedSet<>));

				/// <summary>
				/// <see cref="ImmutableStack{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableStack = new(typeof(ImmutableStack<>));

				/// <summary>
				/// <see cref="ImmutableQueue{T}"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableQueue = new(typeof(ImmutableQueue<>));

				/// <summary>
				/// <see cref="IImmutableList{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IImmutableList = new(typeof(IImmutableList<>));

				/// <summary>
				/// <see cref="IImmutableDictionary{TKey, TValue}"/>.
				/// </summary>
				public static readonly TypeIdentity IImmutableDictionary = new(typeof(IImmutableDictionary<,>));

				/// <summary>
				/// <see cref="IImmutableSet{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IImmutableSet = new(typeof(IImmutableSet<>));

				/// <summary>
				/// <see cref="IImmutableStack{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IImmutableStack = new(typeof(IImmutableStack<>));

				/// <summary>
				/// <see cref="IImmutableQueue{T}"/>.
				/// </summary>
				public static readonly TypeIdentity IImmutableQueue = new(typeof(IImmutableQueue<>));

				/// <summary>
				/// <see cref="ImmutableArray{T}.Builder"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableArrayBuilder = new(typeof(ImmutableArray<>.Builder));

				/// <summary>
				/// <see cref="ImmutableList{T}.Builder"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableListBuilder = new(typeof(ImmutableList<>.Builder));

				/// <summary>
				/// <see cref="ImmutableDictionary{TKey, TValue}.Builder"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableDictionaryBuilder = new(
					typeof(ImmutableDictionary<,>.Builder)
				);

				/// <summary>
				/// <see cref="ImmutableHashSet{T}.Builder"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableHashSetBuilder = new(typeof(ImmutableHashSet<>.Builder));

				/// <summary>
				/// <see cref="ImmutableSortedDictionary{TKey, TValue}.Builder"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableSortedDictionaryBuilder = new(
					typeof(ImmutableSortedDictionary<,>.Builder)
				);

				/// <summary>
				/// <see cref="ImmutableSortedSet{T}.Builder"/>.
				/// </summary>
				public static readonly TypeIdentity ImmutableSortedSetBuilder = new(
					typeof(ImmutableSortedSet<>.Builder)
				);
			}
		}

		/// <summary>
		/// The <c>System.Diagnostics</c> namespace.
		/// </summary>
		public static partial class Diagnostics
		{
			/// <summary>
			/// The <c>System.Diagnostics</c> namespace.
			/// </summary>
			public const string Namespace = "System.Diagnostics";

			/// <summary>
			/// The <c>System.Diagnostics.CodeAnalysis</c> namespace.
			/// </summary>
			public static partial class CodeAnalysis
			{
				public const string Namespace = "System.Diagnostics.CodeAnalysis";

				/// <summary>
				/// <see cref="SuppressMessageAttribute"/>.
				/// </summary>
				public static readonly TypeIdentity SuppressMessageAttribute = new(
					nameof(SuppressMessageAttribute),
					Namespace
				);

				/// <summary>
				/// The <c>System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute</c> type.
				/// </summary>
				public static readonly TypeIdentity ExcludeFromCodeCoverageAttribute = new(
					nameof(ExcludeFromCodeCoverageAttribute),
					Namespace
				);
			}
		}

		/// <summary>
		/// The <c>System.Runtime</c> namespace.
		/// </summary>
		public static partial class Runtime
		{
			/// <summary>
			///	The <c>System.Runtime</c> namespace.
			/// </summary>
			public const string Namespace = "System.Runtime";

			/// <summary>
			/// The <c>System.Runtime.CompilerServices</c> namespace.
			/// </summary>
			public static partial class CompilerServices
			{
				public const string Namespace = "System.Runtime.CompilerServices";

				/// <summary>
				/// <see cref="CompilerGeneratedAttribute"/>.
				/// </summary>
				public static readonly TypeIdentity CompilerGeneratedAttribute = new(
					nameof(CompilerGeneratedAttribute),
					Namespace
				);
			}
		}

		/// <summary>
		/// The <c>System.CodeDom</c> namespace.
		/// </summary>
		public static partial class CodeDom
		{
			/// <summary>
			/// The <c>System.CodeDom</c> namespace.
			/// </summary>
			public const string Namespace = "System.CodeDom";

			/// <summary>
			/// The <c>System.CodeDom.Compiler</c> namespace.
			/// </summary>
			public static partial class Compiler
			{
				public const string Namespace = "System.CodeDom.Compiler";

				/// <summary>
				/// <see cref="GeneratedCodeAttribute"/>.
				/// </summary>
				public static readonly TypeIdentity GeneratedCodeAttribute = new(
					nameof(GeneratedCodeAttribute),
					Namespace
				);
			}
		}
	}

	/// <summary>
	/// Common types from the <c>Microsoft</c> namespace hierarchy.
	/// </summary>
	public static class Microsoft
	{
		/// <summary>
		/// The <c>Microsoft</c> namespace.
		/// </summary>
		public const string Namespace = "Microsoft";

		/// <summary>
		/// Common types from the <c>Microsoft.CodeAnalysis</c> namespace.
		/// </summary>
		public static class CodeAnalysis
		{
			/// <summary>
			/// The <c>Microsoft.CodeAnalysis</c> namespace.
			/// </summary>
			public const string Namespace = "Microsoft.CodeAnalysis";

			/// <summary>
			/// <see cref="EmbeddedAttribute"/>.
			/// </summary>
			public static readonly TypeIdentity EmbeddedAttribute = new(nameof(EmbeddedAttribute), Namespace);
		}

		/// <summary>
		/// Common types from the <c>Microsoft.Extensions</c> namespace.
		/// </summary>
		public static partial class Extensions
		{
			/// <summary>
			/// The <c>Microsoft.Extensions</c> namespace.
			/// </summary>
			public const string Namespace = "Microsoft.Extensions";

			/// <summary>
			/// Common types from the <c>Microsoft.Extensions.DependencyInjection</c> namespace.
			/// </summary>
			public static class DependencyInjection
			{
				public const string DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";

				public static readonly TypeIdentity IServiceCollection = new(
					nameof(IServiceCollection),
					DependencyInjectionNamespace
				);
				public static readonly TypeIdentity ServiceDescriptor = new(
					nameof(ServiceDescriptor),
					DependencyInjectionNamespace
				);
				public static readonly TypeIdentity ServiceLifetime = new(
					nameof(ServiceLifetime),
					DependencyInjectionNamespace
				);
			}
		}
	}
}
