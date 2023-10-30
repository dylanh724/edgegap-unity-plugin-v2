using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Used for the v1/wizard API endpoint.
    /// </summary>
    public class EdgegapWizardApi : EdgegapApiBase
    {
        private const string WIZARD_API_PATH = "v1/wizard/";
        
        public EdgegapWizardApi(
            ApiEnvironment apiEnvironment, 
            string apiToken, 
            EdgegapWindowV2.LogLevel logLevel = EdgegapWindowV2.LogLevel.Error)
            : base(apiEnvironment, apiToken, WIZARD_API_PATH, logLevel)
        {
        }


        #region API Methods
        /// <summary>
        /// POST to v1/wizard/init-quick-start
        /// </summary>
        /// <returns>resultCode; only returns 204 on success</returns>
        public async Task<HttpStatusCode> InitQuickStart()
        {
            HttpResponseMessage result = await PostAsync("init-quick-start");
            HttpStatusCode httpStatusCode = result.StatusCode; 
            bool isSuccess = httpStatusCode == HttpStatusCode.NoContent; // 204
            
            // Log result
            if (!isSuccess)
            {
                Debug.LogError($"InitQuickStart POST !success: " +
                    $"{(int)httpStatusCode} {result.ReasonPhrase}");
            }
            else if (IsLogLevelDebug) 
                Debug.Log($"InitQuickStart POST success: {(int)httpStatusCode} ({result.ReasonPhrase})");
            
            return result.StatusCode;
        }
        
        /// <summary>
        /// GET to v1/wizard/registry-credentials
        /// </summary>
        /// <returns>TODO</returns>
        public async Task<HttpStatusCode> GetRegistryCredentials()
        {
            HttpResponseMessage result = await GetAsync("init-quick-start");
            HttpStatusCode httpStatusCode = result.StatusCode; 
            bool isSuccess = httpStatusCode == HttpStatusCode.NoContent; // 204
            
            // Log result
            if (!isSuccess)
            {
                Debug.LogError($"InitQuickStart POST !success: " +
                    $"{(int)httpStatusCode} {result.ReasonPhrase}");
            }
            else if (IsLogLevelDebug) 
                Debug.Log($"InitQuickStart POST success: {(int)httpStatusCode} ({result.ReasonPhrase})");
            
            return result.StatusCode;
        }
        #endregion // API Methods
    }
}
