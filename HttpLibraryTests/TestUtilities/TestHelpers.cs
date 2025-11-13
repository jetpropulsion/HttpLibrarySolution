using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpLibraryTests.TestUtilities
{
	internal sealed class InMemoryResponseHandler : HttpMessageHandler
	{
		private readonly HttpStatusCode _status;
		private readonly string _headerName;
		private readonly string _headerValue;
		private readonly HttpContent? _content;
		private int _requestCount;

		public int RequestCount => _requestCount;

		public InMemoryResponseHandler(HttpStatusCode status = HttpStatusCode.OK, string headerName = "X-Test-Resp", string headerValue = "respval", HttpContent? content = null)
		{
			_status = status;
			_headerName = headerName;
			_headerValue = headerValue;
			_content = content;
			_requestCount = 0;
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
		{
			_requestCount++;
			HttpResponseMessage r = new HttpResponseMessage(_status);
			r.RequestMessage = request;
			if(!string.IsNullOrEmpty(_headerName))
			{
				r.Headers.Add(_headerName, _headerValue);
			}
			if(_content != null)
			{
				r.Content = _content;
			}
			return Task.FromResult(r);
		}
	}
}