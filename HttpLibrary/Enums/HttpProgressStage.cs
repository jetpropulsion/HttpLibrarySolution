namespace HttpLibrary
{
	/// <summary>
	/// Stages of HTTP operation progress
	/// </summary>
	public enum HttpProgressStage
	{
		/// <summary>
		/// Preparing the request
		/// </summary>
		PreparingRequest,

		/// <summary>
		/// Sending request headers
		/// </summary>
		SendingRequest,

		/// <summary>
		/// Waiting for response
		/// </summary>
		WaitingForResponse,

		/// <summary>
		/// Receiving response headers
		/// </summary>
		ReceivingHeaders,

		/// <summary>
		/// Downloading response body
		/// </summary>
		DownloadingContent,

		/// <summary>
		/// Upload in progress
		/// </summary>
		UploadingContent,

		/// <summary>
		/// Request completed successfully
		/// </summary>
		Completed,

		/// <summary>
		/// Request failed
		/// </summary>
		Failed
	}
}