using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;

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
            EdgegapWindowMetadata.LogLevel logLevel = EdgegapWindowMetadata.LogLevel.Error)
            : base(apiEnvironment, apiToken, logLevel)
        {
        }


        #region API Methods
        /// <summary>
        /// POST to v1/app: Create an application that will regroup application versions.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/application-post 
        /// </summary>
        /// <returns>
        /// Http info with CreateApplicationResult data model
        /// - Success: 200 (no result model)
        /// - Fail: 409 (app already exists), 400 (reached limit)
        /// </returns>
        public async Task<EdgegapHttpResult<CreateApplicationResult>> CreateApp(CreateApplicationRequest request)
        {
            HttpResponseMessage response = await PostAsync("v1/app", request.ToString());
            EdgegapHttpResult<CreateApplicationResult> result = new(response);
            
            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }
        
        /// <summary>
        /// PATCH to v1/app: Update an application version with new specifications.
        /// - API Doc | https://docs.edgegap.com/api/#tag/Applications/operation/app-versions-patch
        /// </summary>
        /// <returns>
        /// Http info with CreateApplicationResult data model
        /// - Success: 200 (no result model)
        /// - Fail: 409 (app already exists), 400 (reached limit)
        /// </returns>
        public async Task<EdgegapHttpResult<CreateApplicationResult>> UpdateAppVersion(UpdateAppVersionRequest request)
        {
            string relativePath = $"v1/app/{request.AppName}/version/{request.VersionName}";
            HttpResponseMessage response = await PatchAsync(relativePath, request.ToString());
            EdgegapHttpResult<CreateApplicationResult> result = new(response);
            
            bool isSuccess = response.StatusCode == HttpStatusCode.OK; // 200
            if (!isSuccess)
                return result;
            
            return result;
        }
        #endregion // API Methods
    }
}
