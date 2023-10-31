using Edgegap.Editor.Api.Models.Requests;
using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for https://docs.edgegap.com/api/#tag/Applications/operation/application-post
    /// </summary>
    public class CreateApplicationResult : CreateApplicationRequest
    {
        [JsonProperty("create_time")]
        public string CreateTimeStr { get; set; }
        
        [JsonProperty("last_updated")]
        public string LastUpdatedStr { get; set; }

        
        /// <summary>Parse to json str</summary>
        public override string ToString() =>
            JsonConvert.SerializeObject(this);
    }
}
