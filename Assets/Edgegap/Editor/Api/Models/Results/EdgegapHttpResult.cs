using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Edgegap.Editor.Api.Models.Results
{
    /// <summary>
    /// Wraps the inner json data with outer http info.
    /// This class overload contains no json-deserialiable data result.
    /// </summary>
    public class EdgegapHttpResult
    {
        /// <summary>HTTP Status code for the request.</summary>
        public HttpStatusCode StatusCode { get; }
        
        /// <summary>This could be err, success, or null.</summary>
        public string Json { get; }
        
        /// <summary>
        /// Typically is sent by servers together with the status code.
        /// Useful for fallback err descriptions, often based on the status code.
        /// </summary>
        public string ReasonPhrase { get; }

        /// <summary>Contains `message` with friendly info.</summary>
        public bool HasErr => Error != null;
        public EdgegapErrorResult Error { get; set; }
        
        #region Common Shortcuts
        public bool IsResultCode200 => StatusCode == HttpStatusCode.OK;
        public bool IsResultCode204 => StatusCode == HttpStatusCode.NoContent;
        public bool IsResultCode409 => StatusCode == HttpStatusCode.Conflict;
        public bool IsResultCode400 => StatusCode == HttpStatusCode.BadRequest;
        #endregion // Common Shortcuts
        
        
        /// <summary>
        /// Constructor that initializes the class based on an HttpResponseMessage.
        /// </summary>
        public EdgegapHttpResult(HttpResponseMessage httpResponse)
        {
            this.ReasonPhrase = httpResponse.ReasonPhrase;
            this.StatusCode = httpResponse.StatusCode;
            
            this.Json = httpResponse.Content.ReadAsStringAsync().Result;
            
            this.Error = JsonConvert.DeserializeObject<EdgegapErrorResult>(Json);
            if (Error != null && string.IsNullOrEmpty(Error.ErrorMessage))
                Error = null;
        }
    }

    /// <summary>
    /// Wraps the inner json data with outer http info.
    /// This class overload contains json-deserialiable data result.
    /// </summary>
    public class EdgegapHttpResult<TResult> : EdgegapHttpResult
    {
        /// <summary>The actual result model from Json. Could be null!</summary>
        public TResult Data { get; set; }
        
        
        public EdgegapHttpResult(HttpResponseMessage httpResponse, bool isLogLevelDebug = false) 
            : base(httpResponse)
        {
            // Assuming JSON content and using Newtonsoft.Json for deserialization
            bool isDeserializable = httpResponse.Content != null &&
                httpResponse.Content.Headers.ContentType.MediaType == "application/json";

            if (isDeserializable)
                this.Data = JsonConvert.DeserializeObject<TResult>(Json);

            if (isLogLevelDebug)
                UnityEngine.Debug.Log($"{typeof(TResult).Name} result: {JObject.Parse(Json)}"); // Prettified
        }
    }
}
