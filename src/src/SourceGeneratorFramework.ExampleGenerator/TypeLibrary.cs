namespace Purview.SourceGeneratorFramework.ExampleGenerator;

/// <summary>
/// Provides <see cref="TypeIdentity"/> instances used by the service registration generator.
/// </summary>
static class PurviewTypeLibrary
{
	const string ExpamplesNamespace = "Purview.SourceGeneratorFramework.Examples";

	/// <summary>
	/// Common types from the <c>Microsoft.Extensions.DependencyInjection</c> namespace.
	/// </summary>
	public static class Microsoft
	{
		/// <summary>
		/// Common types from the <c>Microsoft.Extensions.DependencyInjection</c> namespace.
		/// </summary>
		public static class Extensions
		{
			/// <summary>
			/// Common types from the <c>Microsoft.Extensions.DependencyInjection</c> namespace.
			/// </summary>
			public static class DependencyInjection
			{
				const string DINamespace = "Microsoft.Extensions.DependencyInjection";

				/// <summary>
				/// <c>Microsoft.Extensions.DependencyInjection.IServiceCollection</c>.
				/// </summary>
				public static readonly TypeIdentity IServiceCollection = new(nameof(IServiceCollection), DINamespace);

				/// <summary>
				/// <c>Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions</c>.
				/// </summary>
				public static readonly TypeIdentity ServiceCollectionServiceExtensions = new(
					nameof(ServiceCollectionServiceExtensions),
					DINamespace
				);
			}
		}
	}

	/// <summary>
	/// The <c>[GenerateService]</c> attribute type.
	/// </summary>
	public static readonly TypeIdentity GenerateServiceAttribute = new(
		nameof(GenerateServiceAttribute),
		ExpamplesNamespace
	);

	/// <summary>
	/// The <c>ServiceLifetime</c> enum type.
	/// </summary>
	public static readonly TypeIdentity ServiceLifetime = new(nameof(ServiceLifetime), ExpamplesNamespace);

	/// <summary>
	/// The static <c>ServiceCollectionExtensions</c> class.
	/// </summary>
	public static readonly TypeIdentity ServiceCollectionExtensions = new(
		nameof(ServiceCollectionExtensions),
		ExpamplesNamespace
	);

	/// <summary>
	/// The static <c>ServiceInfo</c> class.
	/// </summary>
	public static readonly TypeIdentity ServiceInfo = new(nameof(ServiceInfo), ExpamplesNamespace);

	/// <summary>
	/// The <c>[GenerateCodeWriterSample]</c> attribute type.
	/// </summary>
	public static readonly TypeIdentity GenerateCodeWriterSampleAttribute = new(
		nameof(GenerateCodeWriterSampleAttribute),
		ExpamplesNamespace
	);
}
