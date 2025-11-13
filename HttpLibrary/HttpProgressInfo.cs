using System;

namespace HttpLibrary
{
	/// <summary>
	/// Represents progress information for HTTP operations
	/// </summary>
	public sealed class HttpProgressInfo
	{
		/// <summary>
		/// The client name that is reporting progress
		/// </summary>
		public string ClientName { get; init; }

		/// <summary>
		/// The URL being requested
		/// </summary>
		public string Url { get; init; }

		/// <summary>
		/// Current stage of the HTTP operation
		/// </summary>
		public HttpProgressStage Stage { get; init; }

		/// <summary>
		/// Number of bytes transferred (download or upload)
		/// </summary>
		public long BytesTransferred { get; init; }

		/// <summary>
		/// Total bytes expected (if known, otherwise null)
		/// </summary>
		public long? TotalBytes { get; init; }

		/// <summary>
		/// Progress percentage (0-100) if total is known, otherwise null
		/// </summary>
		public double? ProgressPercentage => TotalBytes.HasValue && TotalBytes.Value > 0
			? (double)BytesTransferred / TotalBytes.Value * 100.0
			: null;

		/// <summary>
		/// Additional message or information about the current progress
		/// </summary>
		public string? Message { get; init; }

		/// <summary>
		/// Timestamp when this progress was reported
		/// </summary>
		public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

		public HttpProgressInfo(string clientName, string url)
		{
			ClientName = clientName ?? throw new ArgumentNullException(nameof(clientName));
			Url = url ?? throw new ArgumentNullException(nameof(url));
		}
	}
}