using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Handles base URL and common methods for all Edgegap APIs.
    /// </summary>
    public abstract class EdgegapApiBase
    {
        private static string _baseUrlStaging => ApiEnvironment.Staging.GetDashboardUrl();
        private static string _baseUrlConsole => ApiEnvironment.Console.GetApiUrl();
        private readonly HttpClient _httpClient = new(); // Base address set

        protected ApiEnvironment SelectedApiEnvironment { get; }
        protected string ApiToken { get; }

        protected EdgegapApiBase(ApiEnvironment apiEnvironment, string apiToken)
        {
            SelectedApiEnvironment = apiEnvironment;
            
            _httpClient.BaseAddress = new Uri(GetBaseUrl() + "/");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", apiToken);
        }

        /// <summary>Based on SelectedApiEnvironment.</summary>
        /// <returns></returns>
        protected string GetBaseUrl() =>
            SelectedApiEnvironment == ApiEnvironment.Staging
                ? _baseUrlStaging
                : _baseUrlConsole;

        /// <summary>
        /// We already added "https://api.edgegap.com/" (or similar) BaseAddress via constructor.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="json">Serialize your model via Newtonsoft</param>
        /// <returns>
        /// - Success => returns HttpResponseMessage result
        /// - Error => Catches errs => returns null (no rethrow)
        /// </returns>
        protected async Task<HttpResponseMessage> PostAsync(string relativePath, string json = "{}")
        {
            StringContent stringContent = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage result;

            try
            {
                result = await _httpClient.PostAsync(relativePath, stringContent);
            }
            catch (HttpRequestException e)
            {
                UnityEngine.Debug.LogError($"HttpRequestException: {e.Message}");
                return null;
            }
            catch (TaskCanceledException e)
            {
                UnityEngine.Debug.LogError($"TaskCanceledException: Timeout - {e.Message}");
                return null;
            }

            return result;
        }
    }
}
