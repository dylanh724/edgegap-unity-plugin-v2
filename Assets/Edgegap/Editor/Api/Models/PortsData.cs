using Newtonsoft.Json;

namespace Edgegap.Editor.Api.Models
{
    /// <summary>
    /// Used in `PortsData` for `UpdateAppVersionRequest`, `CreateAppVersionRequest`.
    /// </summary>
    public class PortsData
    {
        #region BREAKING BUG
        // (!) BUG: API docs sometimes show dynamic vals as an object; not an array. This object can ONLY be used for arrays of PortsData.
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
        #endregion // BREAKING BUG
        
        
        /// <summary>1024~49151; Default 7770</summary>
        [JsonProperty("port")]
        public int Port { get; set; } = EdgegapWindowMetadata.PORT_DEFAULT;
       
        /// <summary>Default "UDP"</summary>
        [JsonProperty("protocol")]
        public string ProtocolStr { get; set; } = EdgegapWindowMetadata.DEFAULT_PROTOCOL_TYPE.ToString();
        
        [JsonProperty("to_check")]
        public bool ToCheck { get; set; } = true;
        
        [JsonProperty("tls_upgrade")]
        public bool TlsUpgrade { get; set; }
        
        [JsonProperty("name")]
        public string PortName { get; set; } = "Game Port";
    }
}
