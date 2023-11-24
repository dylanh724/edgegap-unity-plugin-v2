using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Result model for `GET v1/status/{request_id}`.
    /// API Doc | https://docs.edgegap.com/api/#tag/Deployments/operation/deployment-status-get
    /// </summary>
    public class GetDeploymentStatusResult
    {
        [JsonProperty("message")]
        public string Message { get; set; }
        
        [JsonProperty("deployment_summary")]
        public DeploymentSummaryData DeploymentSummary { get; set; }


        public class DeploymentSummaryData
        {
            [JsonProperty("request_id")]
            public string RequestId { get; set; }

            [JsonProperty("fqdn")]
            public string Fqdn { get; set; }

            [JsonProperty("app_name")]
            public string AppName { get; set; }

            [JsonProperty("app_version")]
            public string AppVersion { get; set; }

            [JsonProperty("current_status")]
            public string CurrentStatus { get; set; }

            [JsonProperty("running")]
            public bool Running { get; set; }

            [JsonProperty("whitelisting_active")]
            public bool WhitelistingActive { get; set; }

            [JsonProperty("start_time")]
            public string StartTime { get; set; }

            [JsonProperty("removal_time")]
            public string RemovalTime { get; set; }

            [JsonProperty("elapsed_time")]
            public int ElapsedTime { get; set; }

            [JsonProperty("last_status")]
            public string LastStatus { get; set; }

            [JsonProperty("error")]
            public bool Error { get; set; }

            [JsonProperty("error_detail")]
            public string ErrorDetail { get; set; }

            [JsonProperty("ports")]
            public PortsData Ports { get; set; }

            [JsonProperty("public_ip")]
            public string PublicIp { get; set; }

            [JsonProperty("private_ip")]
            public string PrivateIp { get; set; }

            [JsonProperty("private_port")]
            public int PrivatePort { get; set; }

            [JsonProperty("private_protocol")]
            public string PrivateProtocol { get; set; }

            [JsonProperty("private_url")]
            public string PrivateUrl { get; set; }

            [JsonProperty("private_url_https")]
            public string PrivateUrlHttps { get; set; }

            [JsonProperty("private_url_http")]
            public string PrivateUrlHttp { get; set; }

            [JsonProperty("private_url_ws")]
            public string PrivateUrlWs { get; set; }

            [JsonProperty("private_url_wss")]
            public string PrivateUrlWss { get; set; }

            [JsonProperty("sessions")]
            public SessionData[] Sessions { get; set; }
            
        }
    }
}
