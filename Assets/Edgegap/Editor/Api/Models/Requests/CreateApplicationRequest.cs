using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Requests
{
    /// <summary>
    /// Request model for https://docs.edgegap.com/api/#tag/Applications/operation/application-post
    /// </summary>
    public class CreateApplicationRequest
    {
        [JsonProperty("name")]
        public string AppName { get; set; }
        
        [JsonProperty("is_active")]
        public bool IsActive { get; set; }
        
        /// <summary>Optional</summary>
        [JsonProperty("is_telemetry_agent_active")]
        public bool IsTelemetryAgentActive { get; set; }
        
        [JsonProperty("image")]
        public string Image { get; set; }


        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
