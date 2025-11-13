namespace HttpLibrary
{
	/// <summary>
	/// Represents a User-Agent token (product/version or comment).
	/// Internal to avoid exposing small utility on public surface.
	/// </summary>
	internal sealed class UserAgentToken
	{
		public string? Product { get; set; }
		public string? Version { get; set; }
		public string? Comment { get; set; }

		public bool IsComment => Comment is not null;

		public override string ToString()
		{
			if(IsComment)
			{
				return Comment!;
			}
			if(Version is not null)
			{
				return $"{Product}/{Version}";
			}
			return Product ?? string.Empty;
		}
	}
}