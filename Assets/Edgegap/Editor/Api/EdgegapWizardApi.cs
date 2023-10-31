using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Edgegap.Editor.Api
{
    /// <summary>Wraps the v1/wizard API endpoint. Used for internal purposes.</summary>
    public class EdgegapWizardApi : EdgegapApiBase
    {
        /// <summary>Extended path after the base uri</summary>
        public EdgegapWizardApi(
            ApiEnvironment apiEnvironment, 
            string apiToken, 
            EdgegapWindowV2.LogLevel logLevel = EdgegapWindowV2.LogLevel.Error)
            : base(apiEnvironment, apiToken, logLevel)
        {
        }


        #region API Methods
        /// <summary>POST to v1/wizard/init-quick-start</summary>
        /// <returns>resultCode; 204 on success</returns>
        public async Task<HttpStatusCode> InitQuickStart()
        {
            string json = new JObject { ["source"] = "unity" }.ToString();
            HttpResponseMessage response = await PostAsync("v1/wizard/init-quick-start", json);
            HttpStatusCode resultStatusCode = response.StatusCode; 
            
            // bool isSuccess = resultStatusCode == HttpStatusCode.NoContent; // 204
            return resultStatusCode;
        }
        
        /// <summary>GET to v1/wizard/registry-credentials</summary>
        /// <returns>
        /// - TODO: This will later return a data model later; 200 (or 204?) on success
        /// - There will only be errs if called before a successful InitQuickStart().
        /// </returns>
        public async Task<HttpStatusCode> GetRegistryCredentials()
        {
            HttpResponseMessage response = await GetAsync("v1/wizard/registry-credentials");
            HttpStatusCode resultStatusCode = response.StatusCode; 
            
            // bool isSuccess = resultStatusCode == HttpStatusCode.OK; // 200
            return resultStatusCode;
        }
        #endregion // API Methods
    }
}
