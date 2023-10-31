using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Wraps the v1/app API endpoint: Applications Control API.
    /// - API Doc | https://docs.edgegap.com/api/#tag/Applications 
    /// </summary>
    public class EdgegapAppApi : EdgegapApiBase
    {
        public EdgegapAppApi(
            ApiEnvironment apiEnvironment, 
            string apiToken, 
            EdgegapWindowV2.LogLevel logLevel = EdgegapWindowV2.LogLevel.Error)
            : base(apiEnvironment, apiToken, logLevel)
        {
        }


        #region API Methods
        /// <summary>
        /// POST to v1/app: Create an application that will regroup application versions.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/application-post 
        /// </summary>
        /// <returns>CreateApplicationResult; 200 == success</returns>
        public async Task<CreateApplicationResult> CreateApp(CreateApplicationRequest request)
        {
            HttpResponseMessage response = await PostAsync("v1/app", request.ToString());
            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200

            if (!isSuccess)
                return null;
            
            // Serialize result to CreateApplicationResult
            string resultJson = await response.Content.ReadAsStringAsync();
            CreateApplicationResult resultObj = JsonConvert.DeserializeObject<CreateApplicationResult>(resultJson);

            if (IsLogLevelDebug)
                Debug.Log($"{nameof(CreateApp)} result: {JObject.Parse(resultJson)}");
            
            return resultObj;
        }
        #endregion // API Methods
    }
}
