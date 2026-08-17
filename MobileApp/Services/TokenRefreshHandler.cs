using System.Net;
using System.Net.Http.Headers;

namespace MobileApp.Services
{
    /// <summary>
    /// Attaches bearer tokens and silently refreshes them.
    /// Does not prompt biometrics or navigate to login — that belongs on explicit UI login paths.
    /// Background sync and other HTTP callers fail quietly when the session cannot be refreshed.
    /// Transient (offline) refresh failures must not clear the stored session.
    /// </summary>
    public class TokenRefreshHandler : DelegatingHandler
    {
        private readonly IAuthService _authService;

        public TokenRefreshHandler(IAuthService authService)
        {
            _authService = authService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var (accessToken, _) = await _authService.GetStoredTokensAsync();

            if (string.IsNullOrEmpty(accessToken))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            if (await _authService.IsTokenExpiredAsync())
            {
                var refresh = await _authService.RefreshTokenAsync();

                if (refresh.Succeeded && refresh.Token != null)
                {
                    accessToken = refresh.Token.AccessToken;
                }
                else if (!refresh.IsTransientFailure)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    {
                        Content = new StringContent("Session expired. Please login again.")
                    };
                }
            }

            var buffered = await BufferedRequest.CreateAsync(request, cancellationToken);
            buffered.Message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await base.SendAsync(buffered.Message, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            var retryRefresh = await _authService.RefreshTokenAsync();
            if (!retryRefresh.Succeeded || retryRefresh.Token == null)
                return response;

            var retry = buffered.Clone();
            retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", retryRefresh.Token.AccessToken);
            return await base.SendAsync(retry, cancellationToken);
        }

        private sealed class BufferedRequest
        {
            private readonly HttpMethod _method;
            private readonly Uri? _requestUri;
            private readonly Version _version;
            private readonly HttpVersionPolicy _versionPolicy;
            private readonly List<KeyValuePair<string, IEnumerable<string>>> _headers;
            private readonly byte[]? _body;
            private readonly List<KeyValuePair<string, IEnumerable<string>>> _contentHeaders;

            public HttpRequestMessage Message { get; }

            private BufferedRequest(
                HttpRequestMessage message,
                HttpMethod method,
                Uri? requestUri,
                Version version,
                HttpVersionPolicy versionPolicy,
                List<KeyValuePair<string, IEnumerable<string>>> headers,
                byte[]? body,
                List<KeyValuePair<string, IEnumerable<string>>> contentHeaders)
            {
                Message = message;
                _method = method;
                _requestUri = requestUri;
                _version = version;
                _versionPolicy = versionPolicy;
                _headers = headers;
                _body = body;
                _contentHeaders = contentHeaders;
            }

            public static async Task<BufferedRequest> CreateAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                byte[]? body = null;
                var contentHeaders = new List<KeyValuePair<string, IEnumerable<string>>>();
                if (request.Content != null)
                {
                    body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                    contentHeaders.AddRange(request.Content.Headers);
                }

                var headers = request.Headers
                    .Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value))
                    .ToList();

                var message = BuildMessage(
                    request.Method,
                    request.RequestUri,
                    request.Version,
                    request.VersionPolicy,
                    headers,
                    body,
                    contentHeaders);

                foreach (var option in request.Options)
                    message.Options.TryAdd(option.Key, option.Value);

                return new BufferedRequest(
                    message,
                    request.Method,
                    request.RequestUri,
                    request.Version,
                    request.VersionPolicy,
                    headers,
                    body,
                    contentHeaders);
            }

            public HttpRequestMessage Clone()
            {
                return BuildMessage(
                    _method,
                    _requestUri,
                    _version,
                    _versionPolicy,
                    _headers,
                    _body,
                    _contentHeaders);
            }

            private static HttpRequestMessage BuildMessage(
                HttpMethod method,
                Uri? requestUri,
                Version version,
                HttpVersionPolicy versionPolicy,
                List<KeyValuePair<string, IEnumerable<string>>> headers,
                byte[]? body,
                List<KeyValuePair<string, IEnumerable<string>>> contentHeaders)
            {
                var message = new HttpRequestMessage(method, requestUri)
                {
                    Version = version,
                    VersionPolicy = versionPolicy
                };

                foreach (var header in headers)
                    message.Headers.TryAddWithoutValidation(header.Key, header.Value);

                if (body != null)
                {
                    message.Content = new ByteArrayContent(body);
                    foreach (var header in contentHeaders)
                        message.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                return message;
            }
        }
    }
}
