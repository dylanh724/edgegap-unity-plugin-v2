using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Edgegap.Editor.Api;
using IO.Swagger.Model;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;

namespace Edgegap.Editor
{
    /// <summary>
    /// Editor logic event handler for "UI Builder" EdgegapWindow.uxml
    /// V2, where `EdgegapWindow.cs`
    /// </summary>
    public class EdgegapWindowV2 : EditorWindow
    {
        #region Vars
        #region Vars -> Constants
        /// <summary>Log Debug+, or Errors only?</summary>
        public enum LogLevel
        {
            Debug,
            Error,
        }

        /// <summary>TODO: Move opt to UI?</summary>
        private const LogLevel logLevel = LogLevel.Debug;
        
        private bool IsLogLevelDebug => logLevel == LogLevel.Debug;
        
        private const int SERVER_STATUS_CRON_JOB_INTERVAL_MS = 10000; // Interval at which the server status is updated
        private const string EDGEGAP_CONTACT_EN_URL = "https://edgegap.com/en/resources/contact";
        private const string EDITOR_DATA_SERIALIZATION_NAME = "EdgegapSerializationData";

        const string DEBUG_BTN_ID = "DebugBtn";
        
        const string API_TOKEN_TXT_ID = "ApiTokenMaskedTxt";
        const string API_TOKEN_VERIFY_BTN_ID = "ApiTokenVerifyPurpleBtn";
        const string API_TOKEN_GET_BTN_ID = "ApiTokenGetBtn";
        const string POST_AUTH_CONTAINER_ID = "PostAuthContainer";
            
        const string APP_INFO_FOLDOUT_ID = "ApplicationInfoFoldout";
        const string APP_NAME_TXT_ID = "ApplicationNameTxt";
        const string APP_ICON_SPRITE_OBJ_ID = "ApplicationIconSprite";
        const string APP_CREATE_BTN_ID = "ApplicationCreateBtn";
            
        const string CONTAINER_REGISTRY_FOLDOUT_ID = "ContainerRegistryFoldout";
        const string CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID = "ContainerUseCustomRegistryToggle";
        const string CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID = "ContainerCustomRegistryWrapper";
        const string CONTAINER_REGISTRY_URL_TXT_ID = "ContainerRegistryUrlTxt";
        const string CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID = "ContainerImageRepositoryTxt";
        const string CONTAINER_USERNAME_TXT_ID = "ContainerUsernameTxt";
        const string CONTAINER_TOKEN_TXT_ID = "ContainerTokenTxt";
        const string CONTAINER_BUILD_AND_PUSH_BTN_ID = "ContainerBuildAndPushBtn";
            
        const string DEPLOYMENTS_FOLDOUT_ID = "DeploymentsFoldout";
        const string DEPLOYMENTS_REFRESH_BTN_ID = "DeploymentsRefreshBtn";
        const string DEPLOYMENT_CREATE_BTN_ID = "DeploymentCreateBtn";
        const string DEPLOYMENTS_CONTAINER_ID = "DeploymentConnectionsGroupBox"; // Dynamic
        const string DEPLOYMENT_CONNECTION_URL_LABEL_ID = "DeploymentConnectionUrlLabel"; // Dynamic
        const string DEPLOYMENT_CONNECTION_STATUS_LABEL_ID = "DeploymentConnectionStatusLabel"; // Dynamic
        const string DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID = "DeploymentConnectionServerActionStopBtn";
            
        const string FOOTER_DOCUMENTATION_BTN_ID = "FooterDocumentationBtn";
        const string FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID = "FooterNeedMoreGameServersBtn";
            
        [Obsolete("Hard-coded; not from UI. TODO: Get from UI")]
        const string APP_VERSION_NAME = "v1.0.0";

        [Obsolete("Hard-coded; not from UI. TODO: Get from UI")]
        private const ApiEnvironment API_ENVIRONMENT = ApiEnvironment.Console;
        #endregion // Vars -> / Constants
        
        private static readonly HttpClient _httpClient = new();
        private readonly System.Timers.Timer _updateServerStatusCronjob = new(SERVER_STATUS_CRON_JOB_INTERVAL_MS);
        private VisualTreeAsset _visualTree;
        private bool _shouldUpdateServerStatus = false;
        
        #region Vars -> Serialized fields for Editor value persistence
        // Editor persistence >> Not from UI
        [SerializeField] private bool _isApiTokenVerified; // Toggles the rest of the UI
        [SerializeField] private string _deploymentRequestId;
        [SerializeField] private string _userExternalIp;
        [FormerlySerializedAs("_isRegistryReady")]
        [SerializeField] private bool _isContainerRegistryReady;
        [SerializeField] private ApiEnvironment _apiEnvironment; // TODO: Swap out hard-coding with UI element?
        [SerializeField] private string _appVersionName; // TODO: Swap out hard-coding with UI element?
        // [SerializeField] private bool _autoIncrementTag = true; // TODO?
        // [SerializeField] private string _containerImageTag; // TODO?
        
        // Editor persistence >> From UI >> Header
        [FormerlySerializedAs("_apiTokenInputStr")]
        [SerializeField] private string _apiTokenInputUnmaskedStr;
        
        // Editor persistence >>  From UI >> Application Info
        [SerializeField] private string _appNameInputStr;
        [SerializeField] private Sprite _appIconSpriteObj;
        
        // Editor persistence >> From UI >> Container Registry
        [SerializeField] private bool _containerUseCustomRegistryToggleBool;
        [SerializeField] private string _containerRegistryUrlInputStr;
        [SerializeField] private string _containerImageRepositoryInputStr;
        [SerializeField] private string _containerUsernameInputStr;
        [SerializeField] private string _containerTokenInputStr;
        #endregion // Vars -> /Serialized fields for Editor persistence
        
        
        #region Vars -> Interactable Elements
        private Button _debugBtn;
        
        /// <summary>(!) This will only contain `*` chars: For the real token, see `_apiTokenInputUnmaskedStr`.</summary>
        private TextField _apiTokenMaskedInput;
        private Button _apiTokenVerifyBtn;
        private Button _apiTokenGetBtn;
        private VisualElement _postAuthContainer;

        private Foldout _appInfoFoldout;
        private TextField _appNameInput;
        private ObjectField _appIconSpriteObjInput; // selects a Sprite object directly
        private Button _appCreateBtn;
        
        private Foldout _containerRegistryFoldout;
        private Toggle _containerUseCustomRegistryToggle;
        private VisualElement _containerCustomRegistryWrapper;
        private TextField _containerRegistryUrlInput;
        private TextField _containerImageRepositoryInput;
        private TextField _containerUsernameInput;
        private TextField _containerTokenInput;
        private Button _containerBuildAndPushServerBtn;
        
        private Foldout _deploymentsFoldout;
        private Button _deploymentsRefreshBtn;
        private Button _deploymentCreateBtn;
        private VisualElement _deploymentServerDataContainer; // readonly
        private Label _deploymentConnectionStatusLabel; // readonly
        private VisualElement _deploymentConnectionUrlLabel; // Readonly
        private Button _deploymentConnectionServerActionStopBtn;

        private Button _footerDocumentationBtn;
        private Button _footerNeedMoreGameServersBtn;

        // // [Unused in v2, but some explicitly set to a fallback value, for now]
        // private Button _connectionBtn;
        // private TextField _containerImageTagInput;
        // private Toggle _autoIncrementTagInput;
        // private EnumField _apiEnvironmentSelect;
        #endregion // Vars -> /Interactable Elements
        #endregion // Vars

        [MenuItem("Edgegap/Server Management %#e")]
        public static void ShowEdgegapToolWindow()
        {
            EdgegapWindowV2 window = GetWindow<EdgegapWindowV2>();
            window.titleContent = new GUIContent("Edgegap Server Management");
            window.maxSize = new Vector2(635, 860);
            window.minSize = window.maxSize;
        }

        
        #region Unity Funcs
        protected void OnEnable()
        {
            // Set root VisualElement and style
            _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Edgegap/Editor/EdgegapWindow.uxml");
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Edgegap/Editor/EdgegapWindow.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            LoadToolData();

            if (string.IsNullOrWhiteSpace(_userExternalIp))
                _userExternalIp = GetExternalIpAddress();
        }

        /// <summary>
        /// TODO: Replace this with a looping Task - no need to update status every frame
        /// </summary>
        protected void Update()
        {
            if (!_shouldUpdateServerStatus)
                return;
            
            _shouldUpdateServerStatus = false;
            updateServerStatus();
        }

        public void CreateGUI()
        {
            // Get UI elements from UI Builder
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            // Register callbacks and sync UI builder elements to fields here
            InitUIElements();
            SyncFormWithObject();

            // If we cached a deploymentId, restore the settings
            bool hasActiveDeployment = !string.IsNullOrEmpty(_deploymentRequestId);

            if (hasActiveDeployment)
                RestoreActiveDeployment();
            else
                DisconnectCallback();
        }

        protected void OnDestroy()
        {
            bool deploymentActive = !string.IsNullOrEmpty(_deploymentRequestId);

            if (!deploymentActive)
                return;
            
            EditorUtility.DisplayDialog(
                "Warning",
                $"You have an active deployment ({_deploymentRequestId}) that won't be stopped automatically.",
                "Ok"
            );
        }

        /// <summary>The user closed the window. Save the data.</summary>
        protected void OnDisable()
        {
            unregisterClickEvents();
            SyncObjectWithForm();
            SaveToolData();
            EdgegapServerDataManager.DeregisterServerDataContainer(_deploymentServerDataContainer);
        }
        #endregion // Unity Funcs

        
        #region Init
        /// <summary>
        /// Binds the form inputs to the associated variables and initializes the inputs as required.
        /// Requires the VisualElements to be loaded before this call. Otherwise, the elements cannot be found.
        /// </summary>
        private void InitUIElements()
        {
            setVisualElementsToFields();
            assertVisualElementKeys();
            registerClickCallbacks();
            registerFieldCallbacks();
            loadRegisterInitServerDataUiElements();
        }

        /// <summary>
        /// Sanity check: If we changed an #Id, we need to know early so we can update the const.
        /// </summary>
        private void assertVisualElementKeys()
        {
            try
            {
                Assert.IsNotNull(_apiTokenMaskedInput, $"Expected {nameof(_apiTokenMaskedInput)} via #{API_TOKEN_TXT_ID}");
                Assert.IsNotNull(_apiTokenVerifyBtn, $"Expected {nameof(_apiTokenVerifyBtn)} via #{API_TOKEN_VERIFY_BTN_ID}");
                Assert.IsNotNull(_apiTokenGetBtn, $"Expected {nameof(_apiTokenGetBtn)} via #{API_TOKEN_GET_BTN_ID}");
                Assert.IsNotNull(_postAuthContainer, $"Expected {nameof(_postAuthContainer)} via #{POST_AUTH_CONTAINER_ID}");
                
                Assert.IsNotNull(_appInfoFoldout, $"Expected {nameof(_appInfoFoldout)} via #{APP_INFO_FOLDOUT_ID}");
                Assert.IsNotNull(_appNameInput, $"Expected {nameof(_appNameInput)} via #{APP_NAME_TXT_ID}");
                Assert.IsNotNull(_appIconSpriteObjInput, $"Expected {nameof(_appIconSpriteObjInput)} via #{APP_ICON_SPRITE_OBJ_ID}");
                Assert.IsNotNull(_appCreateBtn, $"Expected {nameof(_appCreateBtn)} via #{APP_CREATE_BTN_ID}");

                Assert.IsNotNull(_containerRegistryFoldout, $"Expected {nameof(_containerRegistryFoldout)} via #{CONTAINER_REGISTRY_FOLDOUT_ID}");
                Assert.IsNotNull(_containerUseCustomRegistryToggle, $"Expected {nameof(_containerUseCustomRegistryToggle)} via #{CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID}");
                Assert.IsNotNull(_containerCustomRegistryWrapper, $"Expected {nameof(_containerCustomRegistryWrapper)} via #{CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID}");
                Assert.IsNotNull(_containerRegistryUrlInput, $"Expected {nameof(_containerRegistryUrlInput)} via #{CONTAINER_REGISTRY_URL_TXT_ID}");
                Assert.IsNotNull(_containerImageRepositoryInput, $"Expected {nameof(_containerImageRepositoryInput)} via #{CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID}");
                Assert.IsNotNull(_containerUsernameInput, $"Expected {nameof(_containerUsernameInput)} via #{CONTAINER_USERNAME_TXT_ID}");
                Assert.IsNotNull(_containerTokenInput, $"Expected {nameof(_containerTokenInput)} via #{CONTAINER_TOKEN_TXT_ID}");
                Assert.IsNotNull(_containerBuildAndPushServerBtn, $"Expected {nameof(_containerBuildAndPushServerBtn)} via #{CONTAINER_BUILD_AND_PUSH_BTN_ID}");

                Assert.IsNotNull(_deploymentsFoldout, $"Expected {nameof(_deploymentsFoldout)} via #{DEPLOYMENTS_FOLDOUT_ID}");
                Assert.IsNotNull(_deploymentsRefreshBtn, $"Expected {nameof(_deploymentsRefreshBtn)} via #{DEPLOYMENTS_REFRESH_BTN_ID}");
                Assert.IsNotNull(_deploymentCreateBtn, $"Expected {nameof(_deploymentCreateBtn)} via #{DEPLOYMENT_CREATE_BTN_ID}");
                Assert.IsNotNull(_deploymentServerDataContainer, $"Expected {nameof(_deploymentServerDataContainer)} via #{DEPLOYMENTS_CONTAINER_ID}");
                Assert.IsNotNull(_deploymentConnectionUrlLabel, $"Expected {nameof(_deploymentConnectionUrlLabel)} via #{DEPLOYMENT_CONNECTION_URL_LABEL_ID}");
                Assert.IsNotNull(_deploymentConnectionStatusLabel, $"Expected {nameof(_deploymentConnectionStatusLabel)} via #{DEPLOYMENT_CONNECTION_STATUS_LABEL_ID}");
                Assert.IsNotNull(_deploymentConnectionServerActionStopBtn, $"Expected {nameof(_deploymentConnectionServerActionStopBtn)} via #{DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID}");
                
                Assert.IsNotNull(_footerDocumentationBtn, $"Expected {nameof(_footerDocumentationBtn)} via #{FOOTER_DOCUMENTATION_BTN_ID}");
                Assert.IsNotNull(_footerNeedMoreGameServersBtn, $"Expected {nameof(_footerNeedMoreGameServersBtn)} via #{FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID}");
                
                // // TODO: Explicitly set, for now in v2 - but remember to assert later if we stop hard-coding these >>
                // _apiEnvironment
                // _appVersionName
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                _postAuthContainer.SetEnabled(false);
            }
        }

        private void loadRegisterInitServerDataUiElements()
        {
            VisualElement serverDataElement = EdgegapServerDataManager.GetServerDataVisualTree();
            EdgegapServerDataManager.RegisterServerDataContainer(serverDataElement);
            
            _deploymentServerDataContainer.Clear();
            _deploymentServerDataContainer.Add(serverDataElement);
            
            _postAuthContainer.SetEnabled(_isApiTokenVerified);
        }

        /// <summary>
        /// Register non-btn change actionss. We'll want to save for persistence, validate, etc
        /// </summary>
        private void registerFieldCallbacks()
        {
            _apiTokenMaskedInput.RegisterValueChangedCallback(ApiTokenInputChanged);
            _apiTokenMaskedInput.RegisterCallback<FocusOutEvent>(ApiTokenInputFocusOut);
            
            _appIconSpriteObjInput.RegisterValueChangedCallback(AppIconSpriteObjInput);
            _containerUseCustomRegistryToggle.RegisterValueChangedCallback(ContainerUseCustomRegistryToggle);
        }

        /// <summary>
        /// Register click actions, mostly from buttons: Need to -= unregistry them @ OnDisable
        /// </summary>
        private void registerClickCallbacks()
        {
            _debugBtn.clickable.clicked += onDebugBtnClick;
            
            _apiTokenVerifyBtn.clickable.clicked += onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked += onApiTokenGetBtnClick;
            
            _appCreateBtn.clickable.clicked += onAppCreateBtnClick;
            
            _containerBuildAndPushServerBtn.clickable.clicked += onContainerBuildAndPushServerBtnClick;
            
            _deploymentsRefreshBtn.clickable.clicked += onDeploymentsRefreshBtnClick;
            _deploymentCreateBtn.clickable.clicked += onDeploymentCreateBtnClick;
            _deploymentConnectionServerActionStopBtn.clickable.clicked += onDeploymentServerActionStopBtnClick;
            
            _footerDocumentationBtn.clickable.clicked += onFooterDocumentationBtnClick;
            _footerNeedMoreGameServersBtn.clickable.clicked += onFooterNeedMoreGameServersBtnClick;
        }

        /// <summary>Set fields referencing UI Builder's fields</summary>
        private void setVisualElementsToFields()
        {
            _debugBtn = rootVisualElement.Q<Button>(DEBUG_BTN_ID);
            
            _apiTokenMaskedInput = rootVisualElement.Q<TextField>(API_TOKEN_TXT_ID);
            _apiTokenVerifyBtn = rootVisualElement.Q<Button>(API_TOKEN_VERIFY_BTN_ID);
            _apiTokenGetBtn = rootVisualElement.Q<Button>(API_TOKEN_GET_BTN_ID);
            _postAuthContainer = rootVisualElement.Q<VisualElement>(POST_AUTH_CONTAINER_ID);
            
            _appInfoFoldout = rootVisualElement.Q<Foldout>(APP_INFO_FOLDOUT_ID);
            _appNameInput = rootVisualElement.Q<TextField>(APP_NAME_TXT_ID);
            _appIconSpriteObjInput = rootVisualElement.Q<ObjectField>(APP_ICON_SPRITE_OBJ_ID);
            _appCreateBtn = rootVisualElement.Q<Button>(APP_CREATE_BTN_ID);
            
            _containerRegistryFoldout = rootVisualElement.Q<Foldout>(CONTAINER_REGISTRY_FOLDOUT_ID);
            _containerUseCustomRegistryToggle = rootVisualElement.Q<Toggle>(CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID);
            _containerCustomRegistryWrapper = rootVisualElement.Q<VisualElement>(CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID);
            _containerRegistryUrlInput = rootVisualElement.Q<TextField>(CONTAINER_REGISTRY_URL_TXT_ID);
            _containerImageRepositoryInput = rootVisualElement.Q<TextField>(CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID);
            _containerUsernameInput = rootVisualElement.Q<TextField>(CONTAINER_USERNAME_TXT_ID);
            _containerTokenInput = rootVisualElement.Q<TextField>(CONTAINER_TOKEN_TXT_ID);
            _containerBuildAndPushServerBtn = rootVisualElement.Q<Button>(CONTAINER_BUILD_AND_PUSH_BTN_ID);

            _deploymentsFoldout = rootVisualElement.Q<Foldout>(DEPLOYMENTS_FOLDOUT_ID);
            _deploymentsRefreshBtn = rootVisualElement.Q<Button>(DEPLOYMENTS_REFRESH_BTN_ID);
            _deploymentCreateBtn = rootVisualElement.Q<Button>(DEPLOYMENT_CREATE_BTN_ID);
            _deploymentServerDataContainer = rootVisualElement.Q<VisualElement>(DEPLOYMENTS_CONTAINER_ID); // Dynamic
            _deploymentConnectionUrlLabel = rootVisualElement.Q<Label>(DEPLOYMENT_CONNECTION_URL_LABEL_ID);
            _deploymentConnectionStatusLabel = rootVisualElement.Q<Label>(DEPLOYMENT_CONNECTION_STATUS_LABEL_ID);
            _deploymentConnectionServerActionStopBtn = rootVisualElement.Q<Button>(DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID);
            
            _footerDocumentationBtn = rootVisualElement.Q<Button>(FOOTER_DOCUMENTATION_BTN_ID);
            _footerNeedMoreGameServersBtn = rootVisualElement.Q<Button>(FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID);
            
            _apiEnvironment = API_ENVIRONMENT; // (!) TODO: Hard-coded while unused in UI
            _appVersionName = APP_VERSION_NAME; // (!) TODO: Hard-coded while unused in UI
            
            #region Unused in v2 UI
            // _containerImageTagInput = rootVisualElement.Q<TextField>("tag");
            // _autoIncrementTagInput = rootVisualElement.Q<Toggle>("autoIncrementTag");
            // _connectionBtn = rootVisualElement.Q<Button>("connectionBtn");
            // _apiEnvironmentSelect = rootVisualElement.Q<EnumField>("environmentSelect");
            // _appVersionNameInput = rootVisualElement.Q<TextField>("appVersionName");
            #endregion // Unused in v2 UI
        }
        
        private void RestoreActiveDeployment()
        {
            ConnectCallback();

            _shouldUpdateServerStatus = true;
            StartServerStatusCronjob();
        }
        
        
        #region Init -> Button clicks
        // ##########################################################
        // TODO UX: Disable btn on click
        // TODO UX: Show loading spinner on click
        // TODO UX: Restore btn on done (and hide loading spinner)
        // TODO UX: On success, reflect UI 
        // TODO UX: On error, reflect UI
        // ##########################################################

        private void onDebugBtnClick()
        {
            if (IsLogLevelDebug) Debug.Log("onDebugBtnClick");
            _postAuthContainer.SetEnabled(!_postAuthContainer.enabledInHierarchy);
        }
        
        private void onApiTokenVerifyBtnClick() => verifyApiToken();
        private void onApiTokenGetBtnClick() => getApiToken();
        private void onAppCreateBtnClick() => createApp();
        private void onContainerBuildAndPushServerBtnClick() => buildAndPushServer();
        private void onDeploymentsRefreshBtnClick() => updateServerStatus();
        private void onDeploymentCreateBtnClick() => startServerCallback();
        private void onDeploymentServerActionStopBtnClick() => stopServerCallback();
        private void onFooterDocumentationBtnClick() => openDocumentationCallback();
        private void onFooterNeedMoreGameServersBtnClick() => openNeedMoreGameServersWebsite();
        #endregion // Init -> /Button Clicks
        #endregion // Init

        
        /// <summary>Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.</summary>
        private void unregisterClickEvents()
        {
            _debugBtn.clickable.clicked -= onDebugBtnClick;
            _apiTokenVerifyBtn.clickable.clicked -= onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked -= onApiTokenGetBtnClick;
            _appCreateBtn.clickable.clicked -= onAppCreateBtnClick;
            _containerBuildAndPushServerBtn.clickable.clicked -= onContainerBuildAndPushServerBtnClick;
            _deploymentsRefreshBtn.clickable.clicked -= onDeploymentsRefreshBtnClick;
            _deploymentCreateBtn.clickable.clicked -= onDeploymentCreateBtnClick;
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= onDeploymentServerActionStopBtnClick;
            _footerDocumentationBtn.clickable.clicked -= onFooterDocumentationBtnClick;
            _footerNeedMoreGameServersBtn.clickable.clicked -= onFooterNeedMoreGameServersBtnClick;
        }
        
        private void ConnectCallback()
        {
            string selectedAppName = _appNameInput.value;
            string selectedVersionName = _appVersionName; // TODO: Hard-coded while unused in UI
            string selectedApiKey = _apiTokenInputUnmaskedStr;

            bool validAppName = !string.IsNullOrEmpty(selectedAppName) && !string.IsNullOrWhiteSpace(selectedAppName);
            bool validVersionName = !string.IsNullOrEmpty(selectedVersionName) && !string.IsNullOrWhiteSpace(selectedVersionName);
            bool validApiKey = selectedApiKey.StartsWith("token ");
            bool isValidToConnect = validAppName && validVersionName && validApiKey; 
            
            if (!isValidToConnect)
            {
                EditorUtility.DisplayDialog(
                    "Could not connect - Invalid data",
                    "The data provided is invalid. " +
                    "Make sure every field is filled, and that you provide your complete Edgegap API token " +
                    "(including the \"token\" part).", 
                    "Ok"
                );
                return;
            }

            string apiKeyValue = selectedApiKey.Substring(6);
            Connect(_apiEnvironment, selectedAppName, selectedVersionName, apiKeyValue);
        }
        
        private void SyncObjectWithForm()
        {
            // Looking for _apiTokenInputUnmaskedStr? This is already handed @ ApiTokenInputChanged()
            _appNameInputStr = _appNameInput.value;
            _appIconSpriteObj = _appIconSpriteObjInput.value as Sprite;
                
            _containerUseCustomRegistryToggleBool = _containerUseCustomRegistryToggle.value;
            _containerRegistryUrlInputStr = _containerRegistryUrlInput.value;
            _containerImageRepositoryInputStr = _containerImageRepositoryInput.value;
            _containerUsernameInputStr = _containerUsernameInput.value;
            _containerTokenInputStr = _containerTokenInput.value;

            // _appVersionName = _appVersionNameInput.value; // TODO: Hard-coded while unused in v2 UI
            // _apiEnvironment = (ApiEnvironment)_apiEnvironmentSelect.value; // TODO: Hard-coded while unused in v2 UI
            
            // _containerImageTag = _containerImageTagInput.value; // TODO: Unused in v2 UI
            // _autoIncrementTag = _autoIncrementTagInput.value; // TODO: Unused in V2 UI
        }

        private void SyncFormWithObject()
        {
            // Fill the masked input with '*' chars instead of the actual val
            _apiTokenMaskedInput.value = _apiTokenInputUnmaskedStr; // This will auto-mask with `*` on change
            
            // Only show the rest of the form if apiToken is verified
            _postAuthContainer.SetEnabled(_isApiTokenVerified);
            
            _appNameInput.value = _appNameInputStr;
            _appIconSpriteObjInput.value = _appIconSpriteObj;
            
            _containerCustomRegistryWrapper.SetEnabled(_containerUseCustomRegistryToggleBool);
            _containerUseCustomRegistryToggle.value = _containerUseCustomRegistryToggleBool;
            _containerRegistryUrlInput.value = _containerRegistryUrlInputStr;
            _containerImageRepositoryInput.value = _containerImageRepositoryInputStr;
            _containerUsernameInput.value = _containerUsernameInputStr;
            _containerTokenInput.value = _containerTokenInputStr;

            // _appVersionNameInput.value = _appVersionName; // TODO: Hard-coded while unused in v2 UI
            // _apiEnvironmentSelect.value = _apiEnvironment; // TODO: Hard-coded while unused in v2 UI
            
            // _containerImageTagInput.value = _containerImageTag; // TODO: Unused in V2 UI
            // _autoIncrementTagInput.value = _autoIncrementTag;  // TODO: Unused in V2 UI
        }
        
        /// <summary>From UI "Verify" btn</summary>
        /// <param name="selectedApiEnvironment"></param>
        /// <param name="selectedAppName"></param>
        /// <param name="selectedAppVersionName"></param>
        /// <param name="selectedApiTokenValue"></param>
        private async void Connect(
            ApiEnvironment selectedApiEnvironment,
            string selectedAppName,
            string selectedAppVersionName,
            string selectedApiTokenValue
        )
        {
            SetToolUIState(ToolState.Connecting);

            _httpClient.BaseAddress = new Uri(selectedApiEnvironment.GetApiUrl());

            string path = $"/v1/app/{selectedAppName}/version/{selectedAppVersionName}";

            // Headers
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", selectedApiTokenValue);

            // Make HTTP request
            HttpResponseMessage response = await _httpClient.GetAsync(path);

            if (response.IsSuccessStatusCode)
            {
                SyncObjectWithForm();
                SetToolUIState(ToolState.Connected);
            }
            else
            {
                int status = (int)response.StatusCode;
                string title;
                string message;

                if (status == 401)
                {
                    string apiEnvName = Enum.GetName(typeof(ApiEnvironment), selectedApiEnvironment);
                    title = "Invalid credentials";
                    message = $"Could not find an Edgegap account with this API key for the {apiEnvName} environment.";
                }
                else if (status == 404)
                {
                    title = "App not found";
                    message = $"Could not find app {selectedAppName} with version {selectedAppVersionName}.";
                }
                else
                {
                    title = "Oops";
                    message = $"There was an error while connecting you to the Edgegap API. Please try again later.";
                }

                EditorUtility.DisplayDialog(title, message, "Ok");
                SetToolUIState(ToolState.Disconnected);
            }
        }
        

        #region Immediate non-button changes
        /// <summary>
        /// - There's no built-in way to add a password (*) char mask in UI Builder,
        /// so we do it manually on change, storing the actual value elsewhere
        /// </summary>
        /// <param name="evt"></param>
        private void ApiTokenInputChanged(ChangeEvent<string> evt)
        {
            // Cache the real token val
            if (!evt.newValue.StartsWith('*'))
                _apiTokenInputUnmaskedStr = evt.newValue;

            // Reset form to !verified state
            _isApiTokenVerified = false;
            _postAuthContainer.SetEnabled(false);
        }
        
        /// <summary>Replace the input with masked `*` chars on focus out</summary>
        /// <param name="evt"></param>
        private void ApiTokenInputFocusOut(FocusOutEvent evt) =>
            _apiTokenMaskedInput.value = "".PadLeft(_apiTokenInputUnmaskedStr.Length, '*');
        
        private void ContainerUseCustomRegistryToggle(ChangeEvent<bool> evt) =>
            _containerCustomRegistryWrapper.SetEnabled(evt.newValue);
        
        private void AppIconSpriteObjInput(ChangeEvent<UnityEngine.Object> evt)
        {
            // TODO: Show preview?
        }
        #endregion // Immediate non-button changes
        
        private async Task verifyApiToken()
        {
            if (IsLogLevelDebug) Debug.Log("verifyApiToken");
                
            EdgegapWizardApi wizardApi = new EdgegapWizardApi(
                API_ENVIRONMENT, 
                _apiTokenInputUnmaskedStr.Trim(),
                logLevel);
            
            HttpStatusCode resultStatusCode = await wizardApi.InitQuickStart();
            
            _isApiTokenVerified = resultStatusCode == HttpStatusCode.NoContent; // 204
            SyncContainerEnablesToState();

            if (!_isApiTokenVerified)
                return;
            
            // Verified: TODO: Get app info so
            
            
            // Toggle the remainder of the form
            // SyncContainerEnablesToState();
        }

        /// <summary>
        /// Toggle container groups and foldouts on/off based on:
        /// - _isApiTokenVerified
        /// </summary>
        private void SyncContainerEnablesToState()
        {
            // Requires _isApiTokenVerified
            _postAuthContainer.SetEnabled(_isApiTokenVerified); // Entire body container
            _appInfoFoldout.SetEnabled(_isApiTokenVerified);
            _appInfoFoldout.value = _isApiTokenVerified;
  
            // + Requires _isContainerRegistryReady
            bool isApiTokenVerifiedAndContainerReady = _isApiTokenVerified && _isContainerRegistryReady;
            
            _containerRegistryFoldout.SetEnabled(isApiTokenVerifiedAndContainerReady);
            _containerRegistryFoldout.value = isApiTokenVerifiedAndContainerReady;
            
            _deploymentsFoldout.SetEnabled(isApiTokenVerifiedAndContainerReady);
            _deploymentsFoldout.value = isApiTokenVerifiedAndContainerReady;

            // + Requires _containerUseCustomRegistryToggleBool
            _containerCustomRegistryWrapper.SetEnabled(isApiTokenVerifiedAndContainerReady && 
                _containerUseCustomRegistryToggleBool);
        }

        private void getApiToken()
        {
            if (IsLogLevelDebug) Debug.Log("getApiToken");
            throw new NotImplementedException("TODO: getApiToken");
        }
        
        private void createApp()
        {
            if (IsLogLevelDebug) Debug.Log("createApp");
            throw new NotImplementedException("TODO: createApp");
        }

        /// <summary>Open contact form in desired locale</summary>
        private void openNeedMoreGameServersWebsite()
        {
            // TODO: Swap site to French, if French locale >> Currently, there is no French-specific Contact form
            // bool isFrenchLocale = Application.systemLanguage == SystemLanguage.French;
            Application.OpenURL(EDGEGAP_CONTACT_EN_URL);
        }

        private void openDocumentationCallback()
        {
            string documentationUrl = _apiEnvironment.GetDocumentationUrl();

            if (!string.IsNullOrEmpty(documentationUrl))
                UnityEngine.Application.OpenURL(documentationUrl);
            else
            {
                string apiEnvName = Enum.GetName(typeof(ApiEnvironment), _apiEnvironment);
                Debug.LogWarning($"Could not open documentation for api environment {apiEnvName}: No documentation URL.");
            }
        }
        
        /// <summary>
        /// With a call to an external resource, determines the current user's public IP address.
        /// </summary>
        /// <returns>External IP address</returns>
        private string GetExternalIpAddress()
        {
            string externalIpString = new WebClient()
                .DownloadString("http://icanhazip.com")
                .Replace("\\r\\n", "")
                .Replace("\\n", "")
                .Trim();
            IPAddress externalIp = IPAddress.Parse(externalIpString);

            return externalIp.ToString();
        }

        private void DisconnectCallback()
        {
            if (string.IsNullOrEmpty(_deploymentRequestId))
                SetToolUIState(ToolState.Disconnected);
            else
            {
                EditorUtility.DisplayDialog(
                    "Cannot disconnect", 
                    "Make sure no server is running in the Edgegap tool before disconnecting", 
                    "Ok");
            }
        }

        private float ProgressCounter = 0;

        private void ShowBuildWorkInProgress(string status) =>
            EditorUtility.DisplayProgressBar(
                "Build and push progress", 
                status, 
                ProgressCounter++ / 50);

        private async void buildAndPushServer()
        {
            SetToolUIState(ToolState.Building);

            SyncObjectWithForm();
            ProgressCounter = 0;
            Action<string> onError = (msg) =>
            {
                EditorUtility.DisplayDialog("Error", msg, "Ok");
                SetToolUIState(ToolState.Connected);
            };

            try
            {
                // check for installation and setup docker file
                if (!await EdgegapBuildUtils.DockerSetupAndInstalationCheck())
                {
                    onError("Docker installation not found. Docker can be downloaded from:\n\nhttps://www.docker.com/");
                    return;
                }

                // create server build
                BuildReport buildResult = EdgegapBuildUtils.BuildServer();
                if (buildResult.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    onError("Edgegap build failed");
                    return;
                }

                string registry = _containerRegistryUrlInputStr;
                string imageName = _containerImageRepositoryInputStr;
                string tag = null; // TODO?
                
                // // increment tag for quicker iteration // TODO?
                // if (_autoIncrementTag)
                // {
                //     tag = EdgegapBuildUtils.IncrementTag(tag);
                // }

                // create docker image
                await EdgegapBuildUtils.DockerBuild(registry, imageName, tag, ShowBuildWorkInProgress);

                SetToolUIState(ToolState.Pushing);

                // push docker image
                if (!await EdgegapBuildUtils.DockerPush(registry, imageName, tag, ShowBuildWorkInProgress))
                {
                    onError("Unable to push docker image to registry. Make sure you're logged in to " + registry);
                    return;
                }

                // update edgegap server settings for new tag
                ShowBuildWorkInProgress("Updating server info on Edgegap");
                // await UpdateAppTagOnEdgegap(tag); // TODO?

                // cleanup
                // _containerImageTag = tag; // TODO?
                SyncFormWithObject();
                EditorUtility.ClearProgressBar();
                SetToolUIState(ToolState.Connected);

                Debug.Log("Server built and pushed successfully");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError(ex);
                onError("Edgegap build and push failed");
            }
        }

        private async void startServerCallback()
        {
            SetToolUIState(ToolState.ProcessingDeployment); // Prevents being called multiple times.

            const string path = "/v1/deploy";
            
            // Setup post data
            DeployPostData deployPostData = new DeployPostData(
                _appNameInputStr, 
                _appVersionName, // TODO: Hard-coded while unused in UI 
                new List<string> { _userExternalIp });
            
            string json = JsonConvert.SerializeObject(deployPostData);
            StringContent postData = new StringContent(json, Encoding.UTF8, "application/json");

            // Make HTTP request
            HttpResponseMessage response = await _httpClient.PostAsync(path, postData);
            string content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Parse response
                Deployment parsedResponse = JsonConvert.DeserializeObject<Deployment>(content);

                _deploymentRequestId = parsedResponse.RequestId;

                updateServerStatus();
                StartServerStatusCronjob();
            }
            else
            {
                Debug.LogError($"Could not start Edgegap server. " +
                    $"Got {(int)response.StatusCode} with response:\n{content}");
                SetToolUIState(ToolState.Connected);
            }
        }

        private async void stopServerCallback()
        {
            string path = $"/v1/stop/{_deploymentRequestId}";

            // Make HTTP request
            HttpResponseMessage response = await _httpClient.DeleteAsync(path);

            if (response.IsSuccessStatusCode)
            {
                updateServerStatus();
                SetToolUIState(ToolState.ProcessingDeployment);
            }
            else
            {
                // Parse response
                string content = await response.Content.ReadAsStringAsync();

                Debug.LogError($"Could not stop Edgegap server. " +
                    $"Got {(int)response.StatusCode} with response:\n{content}");
            }
        }

        private void StartServerStatusCronjob()
        {
            _updateServerStatusCronjob.Elapsed += (sourceObject, elaspedEvent) => 
                _shouldUpdateServerStatus = true;
            
            _updateServerStatusCronjob.AutoReset = true;
            _updateServerStatusCronjob.Start();
        }

        private void StopServerStatusCronjob() => _updateServerStatusCronjob.Stop();

        private async void updateServerStatus()
        {
            Status serverStatusResponse = await FetchServerStatus();

            ToolState toolState;
            ServerStatus serverStatus = serverStatusResponse.GetServerStatus();

            if (serverStatus == ServerStatus.Terminated)
            {
                EdgegapServerDataManager.SetServerData(null, _apiEnvironment);

                if (_updateServerStatusCronjob.Enabled)
                {
                    StopServerStatusCronjob();
                }

                _deploymentRequestId = null;
                toolState = ToolState.Connected;
            }
            else
            {
                EdgegapServerDataManager.SetServerData(serverStatusResponse, _apiEnvironment);

                toolState = serverStatus is ServerStatus.Ready or ServerStatus.Error 
                    ? ToolState.DeploymentRunning 
                    : ToolState.ProcessingDeployment;
            }

            SetToolUIState(toolState);
        }

        private async Task<Status> FetchServerStatus()
        {
            string path = $"/v1/status/{_deploymentRequestId}";

            // Make HTTP request
            HttpResponseMessage response = await _httpClient.GetAsync(path);

            // Parse response
            string content = await response.Content.ReadAsStringAsync();

            Status parsedData;

            if (response.IsSuccessStatusCode)
            {
                parsedData = JsonConvert.DeserializeObject<Status>(content);
            }
            else
            {
                if ((int)response.StatusCode == 400)
                {
                    Debug.LogError("The deployment that was active in the tool is now unreachable. " +
                        "Considering it Terminated.");
                    
                    parsedData = new Status { CurrentStatus = ServerStatus.Terminated.GetLabelText() };
                }
                else
                {
                    Debug.LogError(
                        $"Could not fetch status of Edgegap deployment {_deploymentRequestId}. " +
                        $"Got {(int)response.StatusCode} with response:\n{content}"
                    );
                    parsedData = new Status { CurrentStatus = ServerStatus.NA.GetLabelText() };
                }
            }

            return parsedData;
        }

        private void SetToolUIState(ToolState toolState)
        {
            SetConnectionInfoUI(toolState);
            SetServerActionUI(toolState);
            SetDockerRepoInfoUI(toolState);
            // SetConnectionButtonUI(toolState); // Unused in v2
        }

        private void SetDockerRepoInfoUI(ToolState toolState)
        {
            bool connected = toolState.CanStartDeployment();
            _containerRegistryUrlInput.SetEnabled(connected);
            _containerImageRepositoryInput.SetEnabled(connected);
            
            // // Unused in v2 >>
            // _autoIncrementTagInput.SetEnabled(connected);
            // _containerImageTagInput.SetEnabled(connected);

        }

        private void SetConnectionInfoUI(ToolState toolState)
        {
            bool canEditConnectionInfo = toolState.CanEditConnectionInfo();

            _apiTokenMaskedInput.SetEnabled(canEditConnectionInfo);
            _appNameInput.SetEnabled(canEditConnectionInfo);
            
            // // Unused in v2 >>
            // _apiEnvironmentSelect.SetEnabled(canEditConnectionInfo);
            // _appVersionNameInput.SetEnabled(canEditConnectionInfo);
       
        }

        private void SetServerActionUI(ToolState toolState)
        {
            bool canStartDeployment = toolState.CanStartDeployment();
            bool canStopDeployment = toolState.CanStopDeployment();

            // A bit dirty, but ensures the callback is not bound multiple times on the button.
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= startServerCallback;
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= stopServerCallback;

            _deploymentConnectionServerActionStopBtn.SetEnabled(canStartDeployment || canStopDeployment);

            _containerBuildAndPushServerBtn.SetEnabled(canStartDeployment);

            if (canStopDeployment)
            {
                _deploymentConnectionServerActionStopBtn.text = "Stop Server";
                _deploymentConnectionServerActionStopBtn.clickable.clicked += stopServerCallback;
            }
            else
            {
                _deploymentConnectionServerActionStopBtn.text = "Start Server";
                _deploymentConnectionServerActionStopBtn.clickable.clicked += startServerCallback;
            }
        }

        /// <summary>
        /// Save the tool's serializable data to the EditorPrefs to allow persistence across restarts.
        /// Any field with [SerializeField] will be saved.
        /// </summary>
        private void SaveToolData()
        {
            string data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(EDITOR_DATA_SERIALIZATION_NAME, data);
        }

        /// <summary>
        /// Load the tool's serializable data from the EditorPrefs to the object, restoring the tool's state.
        /// </summary>
        private void LoadToolData()
        {
            string data = EditorPrefs.GetString(
                EDITOR_DATA_SERIALIZATION_NAME, 
                JsonUtility.ToJson(this, false));
            
            JsonUtility.FromJsonOverwrite(data, this);
        }
    }
}