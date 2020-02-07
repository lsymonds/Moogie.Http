﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace Moogie.Http
{
    /// <summary>
    /// Request container that is eventually translated into a HttpWebRequest instance. All extension methods hang off
    /// of this class.
    /// </summary>
    public class HttpRequest
    {
        internal HttpClient HttpClient { get; }
        internal string Uri { get; }
        internal HttpMethod HttpMethod { get; set; } = HttpMethod.Get;
        internal Dictionary<string, string> Headers { get; set; }
        internal List<string> PathSegments { get; set; }
        internal List<(string Name, string Value)> QueryParameters { get; set; }
        internal Func<Task<HttpContent>> GetBodyContent { get; set; }

        /// <summary>
        /// Initialises a new instance of the <see cref="HttpRequest"/> struct with a base URI.
        /// </summary>
        /// <param name="uri">The base URI to make the request against.</param>
        public HttpRequest(string uri) => Uri = uri;

        /// <summary>
        /// Initialises a new instance of the <see cref="HttpRequest"/> struct with a base URI and an underlying
        /// <see cref="HttpClient"/> instance to use.
        /// </summary>
        /// <param name="uri">The base URI to make the request against.</param>
        /// <param name="httpClient">The underlying HttpClient to use to make the request.</param>
        public HttpRequest(string uri, HttpClient httpClient) : this(uri) => HttpClient = httpClient;
    }

    /// <summary>
    /// Contains any and all extensions related to request headers.
    /// </summary>
    public static class HeaderExtensions
    {
        /// <summary>
        /// Sets a header for a <see cref="HttpRequest"/> instance.
        /// </summary>
        /// <param name="request">The http request to set a header against.</param>
        /// <param name="headerName">The name of the header to set.</param>
        /// <param name="headerValue">The header value to set.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest WithRequestHeader(this HttpRequest request, string headerName, string headerValue)
        {
            if (string.IsNullOrWhiteSpace(headerName)) throw new ArgumentNullException(nameof(headerName));

            if (request.Headers == null)
                request.Headers = new Dictionary<string, string>();

            request.Headers[headerName] = headerValue;

            return request;
        }

        /// <summary>
        /// Sets the Authorization header to contain a bearer token. This method does not care if your token has the
        /// "Bearer " prefix.
        /// </summary>
        /// <param name="request">The http request to set the Authorization header against.</param>
        /// <param name="token">The bearer token.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest WithBearerTokenAuth(this HttpRequest request, string token)
            => request.WithRequestHeader("Authorization", $"Bearer {token.Replace("Bearer ", "")}");

        /// <summary>
        /// Sets the user agent for a <see cref="HttpRequest"/>.
        /// </summary>
        /// <param name="request">The http request to set the UserAgent against.</param>
        /// <param name="userAgent">The user agent to set.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest WithUserAgent(this HttpRequest request, string userAgent)
            => request.WithRequestHeader("User-Agent", userAgent);

        /// <summary>
        /// Sets the content type that the requester can interpret. By default, this method appends multiple calls of
        /// <see cref="AcceptingResponseContentType"/> to one another. If you wish to change this behavior, set the
        /// <see cref="replace"/> parameter to true.
        /// </summary>
        /// <param name="request">The http request to set the Accept header against.</param>
        /// <param name="contentType">The content type that the requester can interpret.</param>
        /// <param name="replace">Whether to replace the header instead of adding to it.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AcceptingResponseContentType(this HttpRequest request,
            string contentType,
            bool replace = false)
        {
            string type = request.Headers != null && request.Headers.ContainsKey("Accept") && !replace
                ? $"{request.Headers["Accept"]},{contentType}"
                : contentType;

            return request.WithRequestHeader("Accept", type);
        }

        /// <summary>
        /// Sets the content type that the requester can interpret to be application/json. By default, this method
        /// appends multiple AcceptingResponseContentType calls to one another. If you wish to change this behavior,
        /// set the <see cref="replace"/> parameter to true.
        /// </summary>
        /// <param name="request">The http request to set the Accept header against.</param>
        /// <param name="replace">Whether to replace the header instead of adding to it.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AcceptingJsonResponseContentType(this HttpRequest request, bool replace = false) =>
            request.AcceptingResponseContentType("application/json", replace);

        /// <summary>
        /// Sets the content type that the requester can interpret to be application/xml. By default, this method
        /// appends multiple AcceptingResponseContentType calls to one another. If you wish to change this behavior,
        /// set the <see cref="replace"/> parameter to true.
        /// </summary>
        /// <param name="request">The http request to set the Accept header against.</param>
        /// <param name="replace">Whether to replace the header instead of adding to it.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AcceptingXmlResponseContentType(this HttpRequest request, bool replace = false)
            => request.AcceptingResponseContentType("application/xml", replace);

        /// <summary>
        /// Sets the content type that the requester can interpret to be text/plain. By default, this method
        /// appends multiple AcceptingResponseContentType calls to one another. If you wish to change this behavior,
        /// set the <see cref="replace"/> parameter to true.
        /// </summary>
        /// <param name="request">The http request to set the Accept header against.</param>
        /// <param name="replace">Whether to replace the header instead of adding to it.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AcceptingPlainResponseContentType(this HttpRequest request, bool replace = false)
            => request.AcceptingResponseContentType("text/plain", replace);

        /// <summary>
        /// Sets the content type that the requester can interpret to be text/html. By default, this method
        /// appends multiple AcceptingResponseContentType calls to one another. If you wish to change this behavior,
        /// set the <see cref="replace"/> parameter to true.
        /// </summary>
        /// <param name="request">The http request to set the Accept header against.</param>
        /// <param name="replace">Whether to replace the header instead of adding to it.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AcceptingHtmlResponseContentType(this HttpRequest request, bool replace = false)
            => request.AcceptingResponseContentType("text/html", replace);
    }

    /// <summary>
    /// Contains any and all extensions related to URLs.
    /// </summary>
    public static class UrlExtensions
    {
        /// <summary>
        /// Adds an additional path segment to the url specified when instantiating a <see cref="HttpRequest"/>.
        /// </summary>
        /// <param name="request">The http request to set the additional path segment against.</param>
        /// <param name="pathSegment">The actual path segment.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest WithPathSegment(this HttpRequest request, string pathSegment)
        {
            if (request.PathSegments == null)
                request.PathSegments = new List<string>();

            request.PathSegments.Add(pathSegment);

            return request;
        }

        /// <summary>
        /// Adds a query parameter to a <see cref="HttpRequest"/>. If the query parameter is already present,
        /// it is overwritten.
        /// </summary>
        /// <param name="request">The http request to set the query parameter against.</param>
        /// <param name="parameterName">The query parameter's name.</param>
        /// <param name="value">The query parameter's value.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest WithQueryParameter(this HttpRequest request, string parameterName, string value)
        {
            if (request.QueryParameters == null)
                request.QueryParameters = new List<(string, string)>();

            request.QueryParameters.Add((parameterName, value));

            return request;
        }
    }

    /// <summary>
    /// Contains any and all extensions related to actions performed on remote endpoints.
    /// </summary>
    public static class RequestActionExtensions
    {
        private static HttpRequest SetRequestMethod(this HttpRequest request, HttpMethod method)
        {
            request.HttpMethod = method;
            return request;
        }

        /// <summary>
        /// Sets the request method to GET.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsAGetRequest(this HttpRequest request) => request.SetRequestMethod(HttpMethod.Get);

        /// <summary>
        /// Sets the request method to POST.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsAPostRequest(this HttpRequest request) => request.SetRequestMethod(HttpMethod.Post);

        /// <summary>
        /// Sets the request method to PUT.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsAPutRequest(this HttpRequest request) => request.SetRequestMethod(HttpMethod.Put);

        /// <summary>
        /// Sets the request method to PATCH.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsAPatchRequest(this HttpRequest request) =>
            request.SetRequestMethod(new HttpMethod("PATCH"));

        /// <summary>
        /// Sets the request method to DELETE.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsADeleteRequest(this HttpRequest request) =>
            request.SetRequestMethod(HttpMethod.Delete);

        /// <summary>
        /// Sets the request method to TRACE.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsATraceRequest(this HttpRequest request) =>
            request.SetRequestMethod(HttpMethod.Trace);

        /// <summary>
        /// Sets the request method to HEAD.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsAHeadRequest(this HttpRequest request) => request.SetRequestMethod(HttpMethod.Head);

        /// <summary>
        /// Sets the request method to OPTION.
        /// </summary>
        /// <param name="request">The http request to set the method against.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest AsAnOptionsRequest(this HttpRequest request) =>
            request.SetRequestMethod(HttpMethod.Options);
    }

    /// <summary>
    /// Contains any and all extensions that set a request's body content.
    /// </summary>
    public static class BodyContentExtensions
    {
        /// <summary>
        /// Sets the request's body to be that of an object with a request body content type of application/json.
        /// This method, like all request body method, evaluates when the request is actually performed.
        /// </summary>
        /// <param name="request">The http request to set the body against.</param>
        /// <param name="body">The object to serialize and send as JSON.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest WithJsonBody<T>(this HttpRequest request, T body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            request.GetBodyContent = async () =>
            {
                // No, I don't need to have a using statement. StreamContent will automatically dispose of it when
                // .Dispose() is called on it.
                var stream = new MemoryStream();

                await JsonSerializer.SerializeAsync(stream, body);
                stream.Seek(0, SeekOrigin.Begin);

                return new StreamContent(stream);
            };

            return request;
        }

        /// <summary>
        /// Sets the request's body to be that of a string. This method, like all request body method, evaluates
        /// when the request is actually performed.
        /// </summary>
        /// <param name="request">The http request to set the body against.</param>
        /// <param name="body">The string content to set as the request's body.</param>
        /// <param name="contentType">The content type to be set.</param>
        /// <returns>The current <see cref="HttpRequest"/>.</returns>
        public static HttpRequest WithTextBody(this HttpRequest request, string body, string contentType = "text/plain")
        {
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentNullException(nameof(contentType));

            request.GetBodyContent = () =>
                Task.FromResult((HttpContent) new StringContent(body, Encoding.UTF8, contentType));

            return request;
        }
    }

    /// <summary>
    /// Contains any and all extensions that physically send a request to a remote endpoint.
    /// </summary>
    public static class SendTriggeringExtensions
    {
        private static async Task<HttpResponseMessage> MakeRequest(this HttpRequest request)
        {
            // Build Uri from Uri and query string parameters.
            var uri = new UriBuilder(request.Uri);
            if (request.QueryParameters != null)
            {
                var queryStringParameters = HttpUtility.ParseQueryString(uri.Query);
                foreach (var (name, value) in request.QueryParameters)
                    queryStringParameters.Add(name, value);
                uri.Query = queryStringParameters.ToString();
            }

            var actualRequest = new HttpRequestMessage(request.HttpMethod, uri.Uri);

            // Build headers.
            if (request.Headers != null)
                foreach (var header in request.Headers)
                    actualRequest.Headers.Add(header.Key, header.Value);

            // Set body.
            if (request.GetBodyContent != null)
                actualRequest.Content = await request.GetBodyContent();

            return await request.HttpClient.SendAsync(actualRequest);
        }

        /// <summary>
        /// Makes the request and ensures that the response is successful. If the response is not successful, an error
        /// will be thrown.
        /// </summary>
        /// <param name="request">The configured request to make.</param>
        /// <returns>An awaitable task.</returns>
        public static async Task EnsureSuccessStatusCode(this HttpRequest request)
        {
            using (var response = await request.MakeRequest())
                response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Makes the request and reads the response as a string. This method first ensures that the status code
        /// returned is a successful one.
        /// </summary>
        /// <param name="request">The configured request to make.</param>
        /// <returns>An awaitable task yielding the response as a string.</returns>
        public static async Task<string> ReadResponseAsString(this HttpRequest request)
        {
            using (var response = await request.MakeRequest())
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }

        /// <summary>
        /// Makes the request and deserializes the JSON response into an object. This method first ensures that the
        /// status code returned is a successful one.
        /// </summary>
        /// <param name="request">The configured request to make.</param>
        /// <typeparam name="T">The type to deserialize the JSON into.</typeparam>
        /// <returns>An awaitable task yielding the deserialized object.</returns>
        public static async Task<T> ReadJsonResponseAs<T>(this HttpRequest request)
        {
            using (var response = await request.MakeRequest())
            {
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();
                stream.Seek(0, SeekOrigin.Begin);

                return await JsonSerializer.DeserializeAsync<T>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
        }
    }
}