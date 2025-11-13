namespace HttpLibrary
{
	/// <summary>
	/// Decision on how to handle a redirect
	/// </summary>
	public enum RedirectAction
	{
		/// <summary>
		/// Follow the redirect automatically
		/// </summary>
		Follow,

		/// <summary>
		/// Stop and return the redirect response without following
		/// </summary>
		Stop,

		/// <summary>
		/// Follow the redirect but change the method to GET (useful for POST -> 303 -> GET)
		/// </summary>
		FollowWithGet,

		/// <summary>
		/// Cancel the entire operation. This will throw an OperationCanceledException.
		/// </summary>
		Cancel
	}
}