using System;
using System.Collections.Specialized;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codice.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Handles base URL and common methods for all Edgegap APIs.
    /// </summary>
    public abstract class EdgegapApiBase
    {
        #region Vars
        private static string _baseUrlStaging => ApiEnvironment.Staging.GetDashboardUrl();
        private static string _baseUrlConsole => ApiEnvironment.Console.GetApiUrl();
        protected HttpClient _httpClient = new(); // Base address set

        protected ApiEnvironment SelectedApiEnvironment { get; }
        protected string ApiToken { get; }
        
        protected EdgegapWindowV2.LogLevel LogLevel { get; set; }
        protected bool IsLogLevelDebug => LogLevel == EdgegapWindowV2.LogLevel.Debug;
        
        /// <summary>Based on SelectedApiEnvironment.</summary>
        /// <returns></returns>
        protected string GetBaseUrl() =>
            SelectedApiEnvironment == ApiEnvironment.Staging
                ? _baseUrlStaging
                : _baseUrlConsole;
        #endregion // Vars

        
        /// <param name="apiEnvironment">"console" || "staging-console"?</param>
        /// <param name="apiToken">Without the "token " prefix, although we'll clear this if present</param>
        /// <param name="baseUrlSuffix">Extended base url path; eg: "v1/wizard/" (!) with lingering "/" slash</param>
        /// <param name="logLevel">You may want more-verbose logs other than errs</param>
        protected EdgegapApiBase(
            ApiEnvironment apiEnvironment,
            string apiToken,
            string baseUrlSuffix = "",
            EdgegapWindowV2.LogLevel logLevel = EdgegapWindowV2.LogLevel.Error)
        {
            this.SelectedApiEnvironment = apiEnvironment;

            string url = $"{GetBaseUrl()}/{baseUrlSuffix}";
            this._httpClient.BaseAddress = new Uri(url);
            this._httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            string cleanedApiToken = apiToken.Replace("token ", ""); // We already prefixed token below 
            this._httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("token", cleanedApiToken);

            this.LogLevel = logLevel;
        }

        
        #region HTTP Requests
        /// <summary>
        /// We already added "https://api.edgegap.com/" (or similar) BaseAddress via constructor.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="json">Serialize to your model via Newtonsoft</param>
        /// <returns>
        /// - Success => returns HttpResponseMessage result
        /// - Error => Catches errs => returns null (no rethrow)
        /// </returns>
        protected async Task<HttpResponseMessage> PostAsync(string relativePath, string json = "{}")
        {
            json = appendEdgegapDataToJSON(json);
            StringContent stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            if (IsLogLevelDebug)
                Debug.Log($"PostAsync to: `{_httpClient.BaseAddress}{relativePath}` with json: `{json}`");

            return await ExecuteRequestAsync(() => _httpClient.PostAsync(relativePath, stringContent));
        }
        
        /// <summary>
        /// We already added "https://api.edgegap.com/" (or similar) BaseAddress via constructor.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="customQuery">
        /// To append to the URL; eg: "foo=0&bar=1"
        /// (!) First query key should prefix nothing, as shown</param>
        /// <returns>
        /// - Success => returns HttpResponseMessage result
        /// - Error => Catches errs => returns null (no rethrow)
        /// </returns>
        protected async Task<HttpResponseMessage> GetAsync(string relativePath, string customQuery = "")
        {
            UriBuilder uriBuilder = prepareEdgegapUriWithQuery(relativePath, customQuery);
            if (IsLogLevelDebug) Debug.Log($"GetAsync to: `{uriBuilder.Uri}`");
            
            return await ExecuteRequestAsync(() => _httpClient.GetAsync(uriBuilder.Uri));
        }
        
        /// <summary>POST || GET</summary>
        /// <param name="requestFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected virtual async Task<HttpResponseMessage> ExecuteRequestAsync(
            Func<Task<HttpResponseMessage>> requestFunc, 
            CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = null;
            try
            {
                response = await requestFunc();

                // Check for a successful status code
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    return null;
                }
            }
            catch (HttpRequestException e)
            {
                Debug.LogError($"HttpRequestException: {e.Message}");
                return null;
            }
            catch (TaskCanceledException e)
            {
                if (cancellationToken.IsCancellationRequested)
                    Debug.LogError("Task was cancelled by caller.");
                else
                    Debug.LogError($"TaskCanceledException: Timeout - {e.Message}");
                return null;
            }
            catch (Exception e) // Generic exception handler
            {
                Debug.LogError($"Unexpected error occurred: {e.Message}");
                return null;
            }
    
            return response;
        }
        #endregion // HTTP Requests
        
        
        #region Utils
        /// <summary>Adds required body to json: source</summary>
        /// <param name="json">Serialized json string from requester</param>
        /// <returns></returns>
        private static string appendEdgegapDataToJSON(string json)
        {
            JObject jsonObj = JObject.Parse(json);
            jsonObj["source"] = "unity";
            
            return jsonObj.ToString();
        }
        
        /// <summary>
        /// Merges Edgegap-required query params (source) -> merges with custom query -> normalizes.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="customQuery"></param>
        /// <returns></returns>
        private UriBuilder prepareEdgegapUriWithQuery(string relativePath, string customQuery)
        {
            UriBuilder uriBuilder = new UriBuilder(relativePath);

            // Parse the existing query from the UriBuilder
            NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

            // Add default "source=unity" param
            query["source"] = "unity";

            // Parse and merge the custom query parameters
            NameValueCollection customParams = HttpUtility.ParseQueryString(customQuery);
            foreach (string key in customParams)
            {
                query[key] = customParams[key];
            }

            // Set the merged query back to the UriBuilder
            uriBuilder.Query = query.ToString();

            return uriBuilder;
        }
        #endregion // Utils
    }
}
