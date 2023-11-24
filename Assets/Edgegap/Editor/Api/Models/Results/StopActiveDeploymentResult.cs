using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models.Results
{
    public class StopActiveDeploymentResult
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
            
            [JsonProperty("sessions")]
            public SessionData[] Sessions { get; set; }
            
            [JsonProperty("location")]
            public LocationData Location { get; set; }
            
            [JsonProperty("tags")]
            public string[] Tags { get; set; }
            
            [JsonProperty("sockets")]
            public string Sockets { get; set; }
            
            [JsonProperty("sockets_usage")]
            public string SocketsUsage { get; set; }
            
            [JsonProperty("command")]
            public string Command { get; set; }
            
            [JsonProperty("arguments")]
            public string Arguments { get; set; }
            
        }
        
        public class PortsData
        {
            // (!) BUG: API docs show dynamic vals; Ports expected to be an Array.
            // https://docs.edgegap.com/api/#tag/Deployments/operation/deployment-delete
            // "ports": {
            //     "7777": {
            //         "external": 31669,
            //         "internal": 7777,
            //         "protocol": "UDP",
            //         "name": "7777",
            //         "tls_upgrade": false,
            //         "link": "example.com:31669",
            //         "proxy": 65002
            //     },
            //     "web": {
            //         "external": 31587,
            //         "internal": 8080,
            //         "protocol": "http",
            //         "name": "web",
            //         "tls_upgrade": true,
            //         "link": "https://example.com:31587",
            //         "proxy": 65001
            //     }
            // }
        }

        // /// <summary>
        // /// TODO:
        // /// (!) Expected to be used in an array of Ports.
        // /// (!) However, API docs show Ports as a dynamic object. 
        // /// </summary>
        // public class PortData
        // {
        //     [JsonProperty("external")]
        //     public int External { get; set; }
        //     
        //     [JsonProperty("internal")]
        //     public int Internal { get; set; }
        //     
        //     [JsonProperty("protocol")]
        //     public string ProtocolStr { get; set; }
        //     
        //     [JsonProperty("name")]
        //     public string Name { get; set; }
        //     
        //     [JsonProperty("tls_upgrade")]
        //     public bool TlsUpgrade { get; set; }
        //     
        //     [JsonProperty("link")]
        //     public string Link { get; set; }
        //     
        //     [JsonProperty("proxy")]
        //     public int Proxy { get; set; }
        // }
        
        public class LocationData
        {
            [JsonProperty("city")]
            public string City { get; set; }
            
            [JsonProperty("country")]
            public string Country { get; set; }
            
            [JsonProperty("continent")]
            public string Continent { get; set; }
            
            [JsonProperty("administrative_division")]
            public string AdministrativeDivision { get; set; }
            
            [JsonProperty("timezone")]
            public string Timezone { get; set; }
            
            [JsonProperty("latitude")]
            public double Latitude { get; set; }
            
            [JsonProperty("longitude")]
            public double Longitude { get; set; }
        }
    }
}
