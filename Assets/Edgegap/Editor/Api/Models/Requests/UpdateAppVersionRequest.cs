using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for https://docs.edgegap.com/api/#tag/Applications/operation/app-versions-patch
    /// </summary>
    public class UpdateAppVersionRequest
    {
        /// <summary>The name of the application.</summary>
        [JsonIgnore]
        public string AppName { get; set; }
        
        /// <summary>
        /// The name of the application version.
        /// </summary>
        [JsonIgnore]
        public string VersionName { get; set; }
        
        /// <summary>The Repository where the image is.</summary>
        /// <example>"registry.edgegap.com" || "harbor.edgegap.com" || "docker.io"</example>
        [JsonProperty("docker_repository")]
        public string DockerRegistry { get; set; }

        /// <summary>The name of your image.</summary>
        /// <example>"edgegap/demo" || "myCompany-someId/mylowercaseapp"</example>
        [JsonProperty("docker_image")]
        public string DockerImage { get; set; }

        /// <summary>The tag of your image</summary>
        /// <example>"0.1.2" || "latest" (although "latest" !recommended; use actual versions in production)</example>
        [JsonProperty("docker_tag")]
        public string DockerTag { get; set; }


        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
