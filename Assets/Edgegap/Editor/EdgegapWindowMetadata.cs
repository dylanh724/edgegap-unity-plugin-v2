using System;

namespace Edgegap.Editor
{
    public static class EdgegapWindowMetadata
    {
        /// <summary>Log Debug+, or Errors only?</summary>
        public enum LogLevel
        {
            Debug,
            Error,
        }

        /// <summary>TODO: Move opt to UI?</summary>
        public const LogLevel LOG_LEVEL = LogLevel.Error;
        public const bool SHOW_DEBUG_BTN = false;

        public const int SERVER_STATUS_CRON_JOB_INTERVAL_MS = 10000; // Interval at which the server status is updated
        public const string EDGEGAP_CREATE_TOKEN_URL = "https://app.edgegap.com/user-settings?tab=tokens";
        public const string EDGEGAP_CONTACT_EN_URL = "https://edgegap.com/en/resources/contact";
        public const string EDITOR_DATA_SERIALIZATION_NAME = "EdgegapSerializationData";
        public const string SUCCESS_COLOR_HEX = "#8AEE8C"; // Earthy lime green
        public const string FAIL_COLOR_HEX = "#EE9A8A"; // Blood orange

        #region Player Pref Key Ids for persistence
        /// <summary>Cached as base64</summary>
        public const string API_TOKEN_KEY_STR_PREF_ID = "ApiTokenKey";
        #endregion // Player Pref Key Ids for persistence
        
        public const string DEBUG_BTN_ID = "DebugBtn";
        public const string API_TOKEN_TXT_ID = "ApiTokenMaskedTxt";
        public const string API_TOKEN_VERIFY_BTN_ID = "ApiTokenVerifyPurpleBtn";
        public const string API_TOKEN_GET_BTN_ID = "ApiTokenGetBtn";
        public const string POST_AUTH_CONTAINER_ID = "PostAuthContainer";
            
        public const string APP_INFO_FOLDOUT_ID = "ApplicationInfoFoldout";
        public const string APP_NAME_TXT_ID = "ApplicationNameTxt";
        public const string APP_ICON_SPRITE_OBJ_ID = "ApplicationIconSprite";
        public const string APP_CREATE_BTN_ID = "ApplicationCreateBtn";
        public const string APP_CREATE_RESULT_LABEL_ID = "ApplicationCreateResultLabel";

        public const string CONTAINER_REGISTRY_FOLDOUT_ID = "ContainerRegistryFoldout";
        public const string CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID = "ContainerUseCustomRegistryToggle";
        public const string CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID = "ContainerCustomRegistryWrapper";
        public const string CONTAINER_REGISTRY_URL_TXT_ID = "ContainerRegistryUrlTxt";
        public const string CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID = "ContainerImageRepositoryTxt";
        public const string CONTAINER_USERNAME_TXT_ID = "ContainerUsernameTxt";
        public const string CONTAINER_TOKEN_TXT_ID = "ContainerTokenTxt";
        public const string CONTAINER_BUILD_AND_PUSH_BTN_ID = "ContainerBuildAndPushBtn";
            
        public const string DEPLOYMENTS_FOLDOUT_ID = "DeploymentsFoldout";
        public const string DEPLOYMENTS_REFRESH_BTN_ID = "DeploymentsRefreshBtn";
        public const string DEPLOYMENT_CREATE_BTN_ID = "DeploymentCreateBtn";
        public const string DEPLOYMENTS_CONTAINER_ID = "DeploymentConnectionsGroupBox"; // Dynamic
        public const string DEPLOYMENT_CONNECTION_URL_LABEL_ID = "DeploymentConnectionUrlLabel"; // Dynamic
        public const string DEPLOYMENT_CONNECTION_STATUS_LABEL_ID = "DeploymentConnectionStatusLabel"; // Dynamic
        public const string DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID = "DeploymentConnectionServerActionStopBtn";
            
        public const string FOOTER_DOCUMENTATION_BTN_ID = "FooterDocumentationBtn";
        public const string FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID = "FooterNeedMoreGameServersBtn";
            
        [Obsolete("Hard-coded; not from UI. TODO: Get from UI")]
        public const string APP_VERSION_NAME = "v1.0.0";

        [Obsolete("Hard-coded; not from UI. TODO: Get from UI")]
        public const ApiEnvironment API_ENVIRONMENT = ApiEnvironment.Console;
    }
}
