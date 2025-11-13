using System;

namespace HttpLibrary.Models
{
	/// <summary>
	/// Result of an OPTIONS preflight request parsing Access-Control-Allow-* headers.
	/// </summary>
	public sealed class PreflightResult
	{
		/// <summary>
		/// Value of Access-Control-Allow-Origin header returned by the server (or null if absent).
		/// </summary>
		public string? AllowOrigin { get; set; }

		/// <summary>
		/// Value of Access-Control-Allow-Methods header returned by the server (or null if absent).
		/// </summary>
		public string? AllowMethods { get; set; }

		/// <summary>
		/// Value of Access-Control-Allow-Headers header returned by the server (or null if absent).
		/// </summary>
		public string? AllowHeaders { get; set; }

		/// <summary>
		/// Value of Access-Control-Allow-Credentials header parsed as boolean if present.
		/// </summary>
		public bool? AllowCredentials { get; set; }
	}
}