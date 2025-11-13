namespace HttpLibrary
{
	public interface INamedPooledHttpClientProvider
	{
		IPooledHttpClient GetClient(string name);
	}
}