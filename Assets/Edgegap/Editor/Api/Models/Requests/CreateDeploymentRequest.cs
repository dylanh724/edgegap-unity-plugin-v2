using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for `POST v1/deploy`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deploy
    /// </summary>
    public class CreateDeploymentRequest
    {
        #region Required
        /// <summary>*Required: The name of the App you want to deploy.</summary>
        [JsonProperty("app_name")]
        public string AppName { get; set; }
        
        /// <summary>
        /// *Required: The name of the App Version you want to deploy;
        /// if not present, the last version created is picked.
        /// </summary>
        [JsonProperty("version_name")]
        public string VersionName { get; set; }
        #endregion // Required
        
        
        /// <summary>Used by Newtonsoft</summary>
        public CreateDeploymentRequest()
        {
        }

        /// <summary>Init with required info.</summary>
        /// <param name="appName">The name of the application.</param>
        /// <param name="versionName">
        /// The name of the App Version you want to deploy, if not present,
        /// the last version created is picked.
        /// </param>
        public CreateDeploymentRequest(string appName, string versionName)
        {
            this.AppName = appName;
            this.VersionName = versionName;
        }
        
        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
