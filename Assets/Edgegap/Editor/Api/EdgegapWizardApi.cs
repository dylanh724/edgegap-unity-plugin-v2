using System.Net.Http;
using System.Threading.Tasks;

namespace Edgegap.Editor.Api
{
    /// <summary>
    /// Used for the v1/wizard API endpoint.
    /// </summary>
    public class EdgegapWizardApi : EdgegapApiBase
    {
        private const string WIZARD_URL = "v1/wizard";
        private string _wizardBaseUrl => $"{GetBaseUrl()}/{WIZARD_URL}";
        
        public EdgegapWizardApi(ApiEnvironment apiEnvironment, string apiToken) 
            : base(apiEnvironment, apiToken)
        {
        }

        
        #region API Methods
        /// <summary>
        /// POST to v1/wizard/init-quick-start
        /// </summary>
        public async Task<string> InitQuickStart()
        {
            HttpResponseMessage result = await PostAsync("init-quick-start");
            
            if (result == null) 
                return null;
            
            string json = await result.Content.ReadAsStringAsync();
            return json; // TODO: Instead, return an explicitly-typed result model
        }
        #endregion // API Methods
    }
}
