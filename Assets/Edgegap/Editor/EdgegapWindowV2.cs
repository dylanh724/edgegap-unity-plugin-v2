using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Edgegap.Editor.Api;
using Edgegap.Editor.Api.Models.Requests;
using Edgegap.Editor.Api.Models.Results;
using IO.Swagger.Model;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using Application = UnityEngine.Application;

namespace Edgegap.Editor
{
    /// <summary>
    /// Editor logic event handler for "UI Builder" EdgegapWindow.uxml, superceding` EdgegapWindow.cs`.
    /// </summary>
    public class EdgegapWindowV2 : EditorWindow
    {
        #region Vars
        public static bool IsLogLevelDebug => 
            EdgegapWindowMetadata.LOG_LEVEL == EdgegapWindowMetadata.LogLevel.Debug;
        private static readonly HttpClient _httpClient = new();
        private bool IsInitd;
        private VisualTreeAsset _visualTree;
        private bool _isApiTokenVerified; // Toggles the rest of the UI
        private bool _isContainerRegistryReady;
        private Sprite _appIconSpriteObj;
        private string _appIconBase64Str;
        private ApiEnvironment _apiEnvironment; // TODO: Swap out hard-coding with UI element?
        private string _appVersionName; // TODO: Swap out hard-coding with UI element?
        private GetRegistryCredentialsResult _credentials;
        private static readonly Regex _appNameAllowedCharsRegex = new(@"^[a-zA-Z0-9_\-+\.]*$");
        private GetCreateAppResult loadedApp;
        #endregion // Vars
        
        
        #region Vars -> Interactable Elements
        private Button _debugBtn;
        
        /// <summary>(!) This will only contain `*` chars: For the real token, see `_apiTokenInputUnmaskedStr`.</summary>
        private TextField _apiTokenInput;
        
        private Button _apiTokenVerifyBtn;
        private Button _apiTokenGetBtn;
        private VisualElement _postAuthContainer;

        private Foldout _appInfoFoldout;
        private Button _appLoadExistingBtn;
        private TextField _appNameInput;
        private ObjectField _appIconSpriteObjInput; // selects a Sprite object directly
        private Button _appCreateBtn;
        private Label _appCreateResultLabel;

        private Foldout _containerRegistryFoldout;
        private Toggle _containerUseCustomRegistryToggle;
        private VisualElement _containerCustomRegistryWrapper;
        private TextField _containerRegistryUrlInput;
        private TextField _containerImageRepositoryInput;
        private TextField _containerUsernameInput;
        private TextField _containerTokenInput;
        private TextField _containerNewTagVersionInput;
        private Button _containerBuildAndPushServerBtn;
        private Label _containerBuildAndPushResultLabel;
        
        private Foldout _deploymentsFoldout;
        private Button _deploymentsRefreshBtn;
        private Button _deploymentCreateBtn;
        private VisualElement _deploymentServerDataContainer; // readonly
        private Label _deploymentConnectionStatusLabel; // readonly
        private VisualElement _deploymentConnectionUrlLabel; // Readonly
        private Button _deploymentConnectionServerActionStopBtn;

        private Button _footerDocumentationBtn;
        private Button _footerNeedMoreGameServersBtn;

        #region // Vars -> Legacy v1
        // private Button _connectionBtn;
        // private TextField _containerImageTagInput;
        // private Toggle _autoIncrementTagInput;
        // private EnumField _apiEnvironmentSelect;
        #endregion // Vars -> Legacy v1
        
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

        public async void CreateGUI()
        {
            // Get UI elements from UI Builder
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            // Register callbacks and sync UI builder elements to fields here
            InitUIElements();
            syncFormWithObjectStatic();
            await syncFormWithObjectDynamicAsync(); // API calls

            #region Legacy code from v1 // TODO - Look into what this does
            // If we cached a deploymentId, restore the settings
            // bool hasActiveDeployment = !string.IsNullOrEmpty(_deploymentRequestId);
            //
            // if (hasActiveDeployment)
            //     RestoreActiveDeployment();
            // else
            //     DisconnectCallback();
            #endregion // Legacy code from v1: TODO - Look into what this does

            IsInitd = true;
        }

        /// <summary>The user closed the window. Save the data.</summary>
        protected void OnDisable()
        {
            unregisterClickEvents();
            unregisterFieldCallbacks();
            SyncObjectWithForm();
            
            #region Legacy v1
            // SaveToolData(); // Legacy v1
            // EdgegapServerDataManager.DeregisterServerDataContainer(_deploymentServerDataContainer); // Legacy v1
            #endregion // Legacy v1
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
            closeDisableGroups();
            registerClickCallbacks();
            registerFieldCallbacks();
            initToggleDynamicUi();
        }

        private void closeDisableGroups()
        {
            _appInfoFoldout.value = false;
            _containerRegistryFoldout.value = false;
            _deploymentsFoldout.value = false;
            
            _appInfoFoldout.SetEnabled(false);
            _containerRegistryFoldout.SetEnabled(false);
            _deploymentsFoldout.SetEnabled(false);
        }
        
        /// <summary>Set fields referencing UI Builder's fields. In order of appearance from top-to-bottom.</summary>
        private void setVisualElementsToFields()
        {
            _debugBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEBUG_BTN_ID);
            
            _apiTokenInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.API_TOKEN_TXT_ID);
            _apiTokenVerifyBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID);
            _apiTokenGetBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID);
            _postAuthContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID);
            
            _appInfoFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID);
            _appNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.APP_NAME_TXT_ID);
            _appLoadExistingBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.APP_LOAD_EXISTING_BTN_ID);
            _appIconSpriteObjInput = rootVisualElement.Q<ObjectField>(EdgegapWindowMetadata.APP_ICON_SPRITE_OBJ_ID);
            _appCreateBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.APP_CREATE_BTN_ID);
            _appCreateResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.APP_CREATE_RESULT_LABEL_ID);
            
            _containerRegistryFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.CONTAINER_REGISTRY_FOLDOUT_ID);
            _containerUseCustomRegistryToggle = rootVisualElement.Q<Toggle>(EdgegapWindowMetadata.CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID);
            _containerCustomRegistryWrapper = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID);
            _containerRegistryUrlInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_REGISTRY_URL_TXT_ID);
            _containerImageRepositoryInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID);
            _containerUsernameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_USERNAME_TXT_ID);
            _containerTokenInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID);
            _containerNewTagVersionInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.CONTAINER_NEW_TAG_VERSION_TXT_ID);
            _containerBuildAndPushServerBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_BTN_ID);
            _containerBuildAndPushResultLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_RESULT_LABEL_ID);

            _deploymentsFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.DEPLOYMENTS_FOLDOUT_ID);
            _deploymentsRefreshBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOYMENTS_REFRESH_BTN_ID);
            _deploymentCreateBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOYMENT_CREATE_BTN_ID);
            _deploymentServerDataContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.DEPLOYMENTS_CONTAINER_ID); // Dynamic
            _deploymentConnectionUrlLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_URL_LABEL_ID);
            _deploymentConnectionStatusLabel = rootVisualElement.Q<Label>(EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_STATUS_LABEL_ID);
            _deploymentConnectionServerActionStopBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID);
            
            _footerDocumentationBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.FOOTER_DOCUMENTATION_BTN_ID);
            _footerNeedMoreGameServersBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID);
            
            _apiEnvironment = EdgegapWindowMetadata.API_ENVIRONMENT; // (!) TODO: Hard-coded while unused in UI
            _appVersionName = EdgegapWindowMetadata.APP_VERSION_NAME; // (!) TODO: Hard-coded while unused in UI
            
            #region Unused in v2 UI
            // _containerImageTagInput = rootVisualElement.Q<TextField>("tag");
            // _autoIncrementTagInput = rootVisualElement.Q<Toggle>("autoIncrementTag");
            // _connectionBtn = rootVisualElement.Q<Button>("connectionBtn");
            // _apiEnvironmentSelect = rootVisualElement.Q<EnumField>("environmentSelect");
            // _appVersionNameInput = rootVisualElement.Q<TextField>("appVersionName");
            #endregion // Unused in v2 UI
        }

        /// <summary>
        /// Sanity check: If we implicitly changed an #Id, we need to know early so we can update the const.
        /// In order of appearance seen in setVisualElementsToFields().
        /// </summary>
        private void assertVisualElementKeys()
        {
            try
            {
                Assert.IsTrue(_apiTokenInput is { name: EdgegapWindowMetadata.API_TOKEN_TXT_ID },
                    $"Expected {nameof(_apiTokenInput)} via #{EdgegapWindowMetadata.API_TOKEN_TXT_ID}");
                
                Assert.IsTrue(_apiTokenVerifyBtn is { name: EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID },
                    $"Expected {nameof(_apiTokenVerifyBtn)} via #{EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID}");
                
                Assert.IsTrue(_apiTokenGetBtn is { name: EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID },
                    $"Expected {nameof(_apiTokenGetBtn)} via #{EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID}");
                
                Assert.IsTrue(_postAuthContainer is { name: EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID },
                    $"Expected {nameof(_postAuthContainer)} via #{EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID}");
                
                Assert.IsTrue(_appInfoFoldout is { name: EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID },
                    $"Expected {nameof(_appInfoFoldout)} via #{EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID}");
                
                Assert.IsTrue(_appNameInput is { name: EdgegapWindowMetadata.APP_NAME_TXT_ID },
                    $"Expected {nameof(_appNameInput)} via #{EdgegapWindowMetadata.APP_NAME_TXT_ID}");   
                
                Assert.IsTrue(_appLoadExistingBtn is { name: EdgegapWindowMetadata.APP_LOAD_EXISTING_BTN_ID },
                    $"Expected {nameof(_appLoadExistingBtn)} via #{EdgegapWindowMetadata.APP_LOAD_EXISTING_BTN_ID}");
                
                Assert.IsTrue(_appIconSpriteObjInput is { name: EdgegapWindowMetadata.APP_ICON_SPRITE_OBJ_ID },
                    $"Expected {nameof(_appIconSpriteObjInput)} via #{EdgegapWindowMetadata.APP_ICON_SPRITE_OBJ_ID}");
                
                Assert.IsTrue(_appCreateBtn is { name: EdgegapWindowMetadata.APP_CREATE_BTN_ID },
                    $"Expected {nameof(_appCreateBtn)} via #{EdgegapWindowMetadata.APP_CREATE_BTN_ID}");
                
                Assert.IsTrue(_appCreateResultLabel is { name: EdgegapWindowMetadata.APP_CREATE_RESULT_LABEL_ID },
                    $"Expected {nameof(_appCreateResultLabel)} via #{EdgegapWindowMetadata.APP_CREATE_RESULT_LABEL_ID}");
                
                Assert.IsTrue(_containerRegistryFoldout is { name: EdgegapWindowMetadata.CONTAINER_REGISTRY_FOLDOUT_ID },
                    $"Expected {nameof(_containerRegistryFoldout)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_FOLDOUT_ID}");
                
                Assert.IsTrue(_containerUseCustomRegistryToggle is { name: EdgegapWindowMetadata.CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID },
                    $"Expected {nameof(_containerUseCustomRegistryToggle)} via #{EdgegapWindowMetadata.CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID}");
                
                Assert.IsTrue(_containerCustomRegistryWrapper is { name: EdgegapWindowMetadata.CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID },
                    $"Expected {nameof(_containerCustomRegistryWrapper)} via #{EdgegapWindowMetadata.CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID}");
                
                Assert.IsTrue(_containerRegistryUrlInput is { name: EdgegapWindowMetadata.CONTAINER_REGISTRY_URL_TXT_ID },
                    $"Expected {nameof(_containerRegistryUrlInput)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_URL_TXT_ID}");
                
                Assert.IsTrue(_containerImageRepositoryInput is { name: EdgegapWindowMetadata.CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID },
                    $"Expected {nameof(_containerImageRepositoryInput)} via #{EdgegapWindowMetadata.CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID}");
                
                Assert.IsTrue(_containerUsernameInput is { name: EdgegapWindowMetadata.CONTAINER_USERNAME_TXT_ID },
                    $"Expected {nameof(_containerUsernameInput)} via #{EdgegapWindowMetadata.CONTAINER_USERNAME_TXT_ID}");
                
                Assert.IsTrue(_containerTokenInput is { name: EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID },
                    $"Expected {nameof(_containerTokenInput)} via #{EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID}");
                
                Assert.IsTrue(_containerNewTagVersionInput is { name: EdgegapWindowMetadata.CONTAINER_NEW_TAG_VERSION_TXT_ID },
                    $"Expected {nameof(_containerNewTagVersionInput)} via #{EdgegapWindowMetadata.CONTAINER_NEW_TAG_VERSION_TXT_ID}");
                
                Assert.IsTrue(_containerTokenInput is { name: EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID },
                    $"Expected {nameof(_containerTokenInput)} via #{EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID}");
                
                Assert.IsTrue(_containerBuildAndPushResultLabel is { name: EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_RESULT_LABEL_ID },
                    $"Expected {nameof(_containerBuildAndPushResultLabel)} via #{EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_RESULT_LABEL_ID}");
                
                Assert.IsTrue(_deploymentsFoldout is { name: EdgegapWindowMetadata.DEPLOYMENTS_FOLDOUT_ID },
                    $"Expected {nameof(_deploymentsFoldout)} via #{EdgegapWindowMetadata.DEPLOYMENTS_FOLDOUT_ID}");
                
                Assert.IsTrue(_deploymentsRefreshBtn is { name: EdgegapWindowMetadata.DEPLOYMENTS_REFRESH_BTN_ID },
                    $"Expected {nameof(_deploymentsRefreshBtn)} via #{EdgegapWindowMetadata.DEPLOYMENTS_REFRESH_BTN_ID}");
                
                Assert.IsTrue(_deploymentCreateBtn is { name: EdgegapWindowMetadata.DEPLOYMENT_CREATE_BTN_ID },
                    $"Expected {nameof(_deploymentCreateBtn)} via #{EdgegapWindowMetadata.DEPLOYMENT_CREATE_BTN_ID}");
                
                Assert.IsTrue(_deploymentServerDataContainer is { name: EdgegapWindowMetadata.DEPLOYMENTS_CONTAINER_ID },
                    $"Expected {nameof(_deploymentServerDataContainer)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CONTAINER_ID}");
                
                Assert.IsTrue(_deploymentConnectionUrlLabel is { name: EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_URL_LABEL_ID },
                    $"Expected {nameof(_deploymentConnectionUrlLabel)} via #{EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_URL_LABEL_ID}");
                
                Assert.IsTrue(_deploymentConnectionStatusLabel is { name: EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_STATUS_LABEL_ID },
                    $"Expected {nameof(_deploymentConnectionStatusLabel)} via #{EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_STATUS_LABEL_ID}");
                
                Assert.IsTrue(_deploymentConnectionServerActionStopBtn is { name: EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID },
                    $"Expected {nameof(_deploymentConnectionServerActionStopBtn)} via #{EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID}");
                
                
                Assert.IsTrue(_footerDocumentationBtn is { name: EdgegapWindowMetadata.FOOTER_DOCUMENTATION_BTN_ID },
                    $"Expected {nameof(_footerDocumentationBtn)} via #{EdgegapWindowMetadata.FOOTER_DOCUMENTATION_BTN_ID}");
                
                Assert.IsTrue(_footerNeedMoreGameServersBtn is { name: EdgegapWindowMetadata.FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID },
                    $"Expected {nameof(_footerNeedMoreGameServersBtn)} via #{EdgegapWindowMetadata.FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID}");
                

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

        private void initToggleDynamicUi()
        {
            hideResultLabels();
            loadApiTokenFromBase64Pref();
            _debugBtn.visible = EdgegapWindowMetadata.SHOW_DEBUG_BTN;
        }

        /// <summary>
        /// Load ApiToken from PlayerPrefs. !persisted via ViewDataKey so we don't save
        /// plaintext; base64 is at least better than nothing.
        /// </summary>
        private void loadApiTokenFromBase64Pref()
        {
            string apiTokenBase64Str = PlayerPrefs.GetString(EdgegapWindowMetadata.API_TOKEN_KEY_STR_PREF_ID, null);
            if (apiTokenBase64Str != null)
                _apiTokenInput.SetValueWithoutNotify(Base64Decode(apiTokenBase64Str));
        }
        
        /// <summary>For example, result labels (success/err) should be hidden on init</summary>
        private void hideResultLabels()
        {
            _appCreateResultLabel.visible = false;
            _containerBuildAndPushResultLabel.visible = false;
        }

        /// <summary>
        /// Register non-btn change actionss. We'll want to save for persistence, validate, etc
        /// </summary>
        private void registerFieldCallbacks()
        {
            _apiTokenInput.RegisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.RegisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _appNameInput.RegisterValueChangedCallback(onAppNameInputChanged);
            
            _containerUseCustomRegistryToggle.RegisterValueChangedCallback(onContainerUseCustomRegistryToggle);
            _containerNewTagVersionInput.RegisterValueChangedCallback(onContainerNewTagVersionInputChanged);
        }

        /// <summary>
        /// Register click actions, mostly from buttons: Need to -= unregistry them @ OnDisable()
        /// </summary>
        private void registerClickCallbacks()
        {
            _debugBtn.clickable.clicked += onDebugBtnClick;
            
            _apiTokenVerifyBtn.clickable.clicked += onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked += onApiTokenGetBtnClick;
            
            _appCreateBtn.clickable.clicked += onAppCreateBtnClickAsync;
            _appLoadExistingBtn.clickable.clicked += onAppLoadExistingBtnClickAsync;
            
            _containerBuildAndPushServerBtn.clickable.clicked += onContainerBuildAndPushServerBtnClickAsync;
            
            _deploymentsRefreshBtn.clickable.clicked += onDeploymentsRefreshBtnClick;
            _deploymentCreateBtn.clickable.clicked += onDeploymentCreateBtnClick;
            _deploymentConnectionServerActionStopBtn.clickable.clicked += onDeploymentServerActionStopBtnClick;
            
            _footerDocumentationBtn.clickable.clicked += onFooterDocumentationBtnClick;
            _footerNeedMoreGameServersBtn.clickable.clicked += onFooterNeedMoreGameServersBtnClick;
        }
        
        
        #region Init -> Button clicks
        /// <summary>
        /// Experiment here! You may want to log what you're doing
        /// in case you inadvertently leave it on.
        /// </summary>
        private void onDebugBtnClick() => debugEnableAllGroups();

        private void debugEnableAllGroups()
        {
            Debug.Log("debugEnableAllGroups");
            
            _appInfoFoldout.SetEnabled(true);
            _appInfoFoldout.SetEnabled(true);
            _containerRegistryFoldout.SetEnabled(true);
            _deploymentsFoldout.SetEnabled(true);
            
            if (_containerUseCustomRegistryToggle.value)
                _containerCustomRegistryWrapper.SetEnabled(true);
        }
        
        private void onApiTokenVerifyBtnClick() => _ = verifyApiTokenGetRegistryCredsAsync();
        private void onApiTokenGetBtnClick() => openGetApiTokenWebsite();

        /// <summary>Process UI + validation before/after API logic</summary>
        private async void onAppCreateBtnClickAsync()
        {
            // Assert data locally before calling API
            assertAppNameExists();

            _appCreateResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor("Creating...", 
                EdgegapWindowMetadata.StatusColors.Processing);

            try { await createAppAsync(); }
            finally
            {
                _appCreateBtn.SetEnabled(checkHasAppName()); 
                _appCreateResultLabel.visible = _appCreateResultLabel.text != EdgegapWindowMetadata.LOADING_RICH_STR;
            }
        }

        /// <summary>Process UI + validation before/after API logic</summary>
        private async void onAppLoadExistingBtnClickAsync()
        {
            // Assert UI data locally before calling API
            assertAppNameExists();

            try { await GetAppAsync(); }
            finally
            {
                _appLoadExistingBtn.SetEnabled(checkHasAppName());
                _appCreateResultLabel.visible = _appCreateResultLabel.text != EdgegapWindowMetadata.LOADING_RICH_STR;
            }
        }

        /// <summary>Process UI + validation before/after API logic</summary>
        private async void onContainerBuildAndPushServerBtnClickAsync()
        {
            // Assert data locally before calling API
            // Validate custom container registry, app name
            try
            {
                assertAppNameExists();
                Assert.IsTrue(
                    !_containerImageRepositoryInput.value.EndsWith("/"),
                    $"Expected {nameof(_containerImageRepositoryInput)} to !contain " +
                    "trailing slash (should end with /appName)");
            }
            catch (Exception e)
            {
                Debug.LogError($"onContainerBuildAndPushServerBtnClickAsync Error: {e}");
                throw;
            }

            // Hide previous result labels, disable btns (to reenable when done)
            hideResultLabels();
            _containerBuildAndPushServerBtn.SetEnabled(false);
            
            // Show new loading status
            _containerBuildAndPushResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                EdgegapWindowMetadata.PROCESSING_RICH_STR, 
                EdgegapWindowMetadata.StatusColors.Processing);

            try { await buildAndPushServerAsync(); }
            finally
            {
                _containerBuildAndPushServerBtn.SetEnabled(checkHasAppName()); 
                _containerBuildAndPushResultLabel.visible = _containerBuildAndPushResultLabel.text != EdgegapWindowMetadata.PROCESSING_RICH_STR;
            }
        }

        private bool checkHasAppName() => _appNameInput.value.Length > 0;
        private void onDeploymentsRefreshBtnClick() => _ = updateServerStatusAsync();
        private void onDeploymentCreateBtnClick() => _ = startServerCallbackAsync();
        private void onDeploymentServerActionStopBtnClick() => _ = stopServerCallbackAsync();
        private void onFooterDocumentationBtnClick() => openDocumentationWebsite();
        private void onFooterNeedMoreGameServersBtnClick() => openNeedMoreGameServersWebsite();
        #endregion // Init -> /Button Clicks
        #endregion // Init

        
        /// <summary>Throw if !appName val</summary>
        private void assertAppNameExists() =>
            Assert.IsTrue(!string.IsNullOrEmpty(_appNameInput.value), 
                $"Expected {nameof(_appNameInput)} val");
        
        /// <summary>
        /// Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.
        /// </summary>
        private void unregisterClickEvents()
        {
            _debugBtn.clickable.clicked -= onDebugBtnClick;
            
            _apiTokenVerifyBtn.clickable.clicked -= onApiTokenVerifyBtnClick;
            _apiTokenGetBtn.clickable.clicked -= onApiTokenGetBtnClick;
            
            _appCreateBtn.clickable.clicked -= onAppCreateBtnClickAsync;
            _appLoadExistingBtn.clickable.clicked -= onAppLoadExistingBtnClickAsync;

            _containerBuildAndPushServerBtn.clickable.clicked -= onContainerBuildAndPushServerBtnClickAsync;
            
            _deploymentsRefreshBtn.clickable.clicked -= onDeploymentsRefreshBtnClick;
            _deploymentCreateBtn.clickable.clicked -= onDeploymentCreateBtnClick;
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= onDeploymentServerActionStopBtnClick;
            
            _footerDocumentationBtn.clickable.clicked -= onFooterDocumentationBtnClick;
            _footerNeedMoreGameServersBtn.clickable.clicked -= onFooterNeedMoreGameServersBtnClick;
        }

        private void unregisterFieldCallbacks()
        {
            _apiTokenInput.UnregisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.UnregisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _containerUseCustomRegistryToggle.UnregisterValueChangedCallback(onContainerUseCustomRegistryToggle);
            
            // Dirty deployment connection action btn workarounds (from legacy code)
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= StartServerCallback;
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= StopServerCallback;
        }

        /// <summary>TODO: Save persistent data?</summary>
        private void SyncObjectWithForm()
        {
            _appIconSpriteObj = _appIconSpriteObjInput.value as Sprite;
        }

        /// <summary>TODO: Load persistent data?</summary>
        private void syncFormWithObjectStatic()
        {
            // Only show the rest of the form if apiToken is verified
            _postAuthContainer.SetEnabled(_isApiTokenVerified);
            _appIconSpriteObjInput.value = _appIconSpriteObj;
            _containerCustomRegistryWrapper.SetEnabled(_containerUseCustomRegistryToggle.value);
            _containerUseCustomRegistryToggle.value = _containerUseCustomRegistryToggle.value;

            // _appVersionNameInput.value = _appVersionName; // TODO: Hard-coded while unused in v2 UI
            // _apiEnvironmentSelect.value = _apiEnvironment; // TODO: Hard-coded while unused in v2 UI
            
            // _containerImageTagInput.value = _containerImageTag; // TODO: Unused in V2 UI
            // _autoIncrementTagInput.value = _autoIncrementTag;  // TODO: Unused in V2 UI
            
            // Only enable certain elements if appName exists
            bool hasAppName = checkHasAppName();
            _appCreateBtn.SetEnabled(hasAppName);
            _appLoadExistingBtn.SetEnabled(hasAppName);
        }

        /// <summary>
        /// Dynamically set form based on API call results.
        /// => If APIToken is cached via PlayerPrefs, verify => gets registry creds.
        /// => If appName is cached via ViewDataKey, loads the app.
        /// </summary>
        private async Task syncFormWithObjectDynamicAsync()
        {
            if (string.IsNullOrEmpty(_apiTokenInput.value))
                return;
            
            // We found a cached api token: Verify =>
            if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken; " +
                "calling verifyApiTokenGetRegistryCredsAsync =>");
            await verifyApiTokenGetRegistryCredsAsync();

            // Was the API token verified + we found a cached app name? Load the app =>
            // But ignore errs, since we're just *assuming* the app exists since the appName was filled
            if (_isApiTokenVerified && checkHasAppName())
            {
                if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamicAsync: Found apiToken && appName; " +
                    "calling GetAppAsync =>");
                try { await GetAppAsync(); }
                finally { _appLoadExistingBtn.SetEnabled(checkHasAppName()); }
            }
        }
        

        #region Immediate non-button changes
        /// <summary>
        /// On change, validate -> update custom container registry suffix.
        /// Toggle create app btn if 1+ char
        /// </summary>
        /// <param name="evt"></param>
        private void onAppNameInputChanged(ChangeEvent<string> evt)
        { 
            // Validate: Only allow alphanumeric, underscore, dash, plus, period
            if (!_appNameAllowedCharsRegex.IsMatch(evt.newValue))
                _appNameInput.value = evt.previousValue; // Revert to the previous value
            else
                setContainerImageRepositoryVal(); // Valid -> Update the custom container registry suffix
            
            // Toggle btns on 1+ char entered
            bool hasAppName = checkHasAppName();
            _appCreateBtn.SetEnabled(hasAppName);
            _appLoadExistingBtn.SetEnabled(hasAppName);
        }
        
        /// <summary>
        /// While changing the token, we temporarily unmask. On change, set state to !verified.
        /// </summary>
        /// <param name="evt"></param>
        private void onApiTokenInputChanged(ChangeEvent<string> evt)
        {
            // Unmask while changing
            TextField apiTokenTxt = evt.target as TextField;
            apiTokenTxt.isPasswordField = false;

            // Token changed? Reset form to !verified state and fold all groups
            _isApiTokenVerified = false;
            _postAuthContainer.SetEnabled(false);
            closeDisableGroups();
            
            // Toggle "Verify" btn on 1+ char entered
            _apiTokenVerifyBtn.SetEnabled(evt.newValue.Length > 0);
        }

        /// <summary>Unmask while typing</summary>
        /// <param name="evt"></param>
        private void onApiTokenInputFocusOut(FocusOutEvent evt)
        {
            TextField apiTokenTxt = evt.target as TextField;
            apiTokenTxt.isPasswordField = true;
        }
        
        /// <summary>On toggle, enable || disable the custom registry inputs (below the Toggle).</summary>
        private void onContainerUseCustomRegistryToggle(ChangeEvent<bool> evt) =>
            _containerCustomRegistryWrapper.SetEnabled(evt.newValue);

        /// <summary>On empty, we fallback to "latest", a fallback val from EdgegapWindowMetadata.cs</summary>
        /// <param name="evt"></param>
        private void onContainerNewTagVersionInputChanged(ChangeEvent<string> evt)
        {
            if (!string.IsNullOrEmpty(evt.newValue))
                return;
            
            // Set fallback value -> select all for UX, since the user may not expect this
            _containerNewTagVersionInput.value = EdgegapWindowMetadata.DEFAULT_VERSION_TAG;
            _containerNewTagVersionInput.SelectAll();
        }
        #endregion // Immediate non-button changes

        
        /// <summary>
        /// Used for converting a Sprite to a base64 string: By default, textures are !readable,
        /// and we don't want to have to instruct users how to make it readable for UX.
        /// Instead, we'll make a copy of that texture -> make it readable.
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private Texture2D makeTextureReadable(Texture2D original)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                original.width,
                original.height
            );
            
            Graphics.Blit(original, rt);
            Texture2D readableTexture = new Texture2D(original.width, original.height);

            Rect rect = new Rect(
                0,
                0,
                rt.width,
                rt.height);
            
            readableTexture.ReadPixels(rect, destX: 0, destY: 0);
            readableTexture.Apply();
            RenderTexture.ReleaseTemporary(rt);
            
            return readableTexture;
        }

        /// <summary>From Base64 string -> to Sprite</summary>
        /// <param name="imgBase64Str">Edgegap build app requires a max size of 200</param>
        /// <returns>Sprite</returns>
        private Sprite getSpriteFromBase64Str(string imgBase64Str)
        {
            if (string.IsNullOrEmpty(imgBase64Str))
                return null;
            
            try
            {
                byte[] imageBytes = Convert.FromBase64String(imgBase64Str);

                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(imageBytes);

                Rect rect = new(
                    x: 0.0f,
                    y: 0.0f,
                    texture.width,
                    texture.height);
            
                return Sprite.Create(
                    texture, 
                    rect, 
                    pivot: new Vector2(0.5f, 0.5f), 
                    pixelsPerUnit: 100.0f);
            }
            catch (Exception e)
            {
                Debug.Log($"Warning: getSpriteFromBase64Str failed (returning null) - {e}");
                return null;
            }
        }

        /// <summary>From Sprite -> to Base64 string</summary>
        /// <param name="sprite"></param>
        /// <param name="maxKbSize">Edgegap build app requires a max size of 200</param>
        /// <returns>imageBase64Str</returns>
        private string getBase64StrFromSprite(Sprite sprite, int maxKbSize = 200)
        {
            if (sprite == null)
                return null;
            
            try
            {
                Texture2D texture = makeTextureReadable(sprite.texture);

                // Crop the texture to the sprite's rectangle (instead of the entire texture)
                Texture2D croppedTexture = new Texture2D(
                    (int)sprite.rect.width, 
                    (int)sprite.rect.height);
                
                Color[] pixels = texture.GetPixels(
                    (int)sprite.rect.x, 
                    (int)sprite.rect.y, 
                    (int)sprite.rect.width, 
                    (int)sprite.rect.height
                );
                
                croppedTexture.SetPixels(pixels);
                croppedTexture.Apply();

                // Encode to PNG -> 
                byte[] textureBytes = croppedTexture.EncodeToPNG();

                // Validate size
                const int oneKb = 1024;
                int pngTextureSizeKb = textureBytes.Length / oneKb;
                bool isPngLessThanMaxSize = pngTextureSizeKb < maxKbSize;

                if (!isPngLessThanMaxSize)
                {
                    textureBytes = croppedTexture.EncodeToJPG();
                    int jpgTextureSizeKb = textureBytes.Length / oneKb;
                    bool isJpgLessThanMaxSize = pngTextureSizeKb < maxKbSize;
                    
                    Assert.IsTrue(isJpgLessThanMaxSize, $"Expected texture PNG to be < {maxKbSize}kb " +
                        $"in size (but found {jpgTextureSizeKb}kb); then tried JPG, but is still {jpgTextureSizeKb}kb in size");
                    Debug.LogWarning($"App icon PNG was too large (max {maxKbSize}), so we converted to JPG");
                }
                
                string base64ImageString = Convert.ToBase64String(textureBytes); // eg: "Aaabbcc=="
                return base64ImageString;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifies token => apps/container groups -> gets registry creds (if any).
        /// TODO: UX - Show loading spinner.
        /// </summary>
        private async Task verifyApiTokenGetRegistryCredsAsync()
        {
            if (IsLogLevelDebug) Debug.Log("verifyApiTokenGetRegistryCredsAsync");
            
            // Disable most ui while we verify
            _isApiTokenVerified = false;
            _apiTokenVerifyBtn.SetEnabled(false);
            SyncContainerEnablesToState();
            hideResultLabels();
                
            EdgegapWizardApi wizardApi = new EdgegapWizardApi(
                EdgegapWindowMetadata.API_ENVIRONMENT, 
                _apiTokenInput.value.Trim(),
                EdgegapWindowMetadata.LOG_LEVEL);
            
            EdgegapHttpResult initQuickStartResultCode = await wizardApi.InitQuickStart();

            _apiTokenVerifyBtn.SetEnabled(true);
            _isApiTokenVerified = initQuickStartResultCode.IsResultCode204;
            
            if (!_isApiTokenVerified)
            {
                SyncContainerEnablesToState();
                return;
            }
            
            // Verified: Let's see if we have active registry credentials // TODO: This will later be a result model
            EdgegapHttpResult<GetRegistryCredentialsResult> getRegistryCredentialsResult = await wizardApi.GetRegistryCredentials();

            if (getRegistryCredentialsResult.IsResultCode200)
            {
                // Success
                _credentials = getRegistryCredentialsResult.Data;
                PlayerPrefs.SetString(EdgegapWindowMetadata.API_TOKEN_KEY_STR_PREF_ID, Base64Encode(_apiTokenInput.value));
                prefillContainerRegistryForm(_credentials);
            }
            else
            {
                // Fail
            }

            // Unlock the rest of the form, whether we prefill the container registry or not
            SyncContainerEnablesToState();
        }

        /// <summary>
        /// We have container registry params; we'll prefill registry container fields.
        /// </summary>
        /// <param name="credentials">GetRegistryCredentialsResult</param>
        private void prefillContainerRegistryForm(GetRegistryCredentialsResult credentials)
        {
            if (IsLogLevelDebug) Debug.Log("prefillContainerRegistryForm");

            if (credentials == null)
                throw new Exception($"!{nameof(credentials)}");

            _containerRegistryUrlInput.value = credentials.RegistryUrl;

            setContainerImageRepositoryVal();
            _containerUsernameInput.value = credentials.Username;
            _containerTokenInput.value = credentials.Token;
        }

        /// <summary>
        /// Sets to "{credentials.Project}/{appName}" from cached credentials, forcing lowercased appName.
        /// </summary>
        private void setContainerImageRepositoryVal()
        {
            // ex: "xblade1-9sa8dfh9sda8hf/mygame1"
            string project = _credentials?.Project ?? "";
            string appName = _appNameInput?.value.ToLowerInvariant() ?? "";
            _containerImageRepositoryInput.value = $"{project}/{appName}";
        }
        
        public static string Base64Encode(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainBytes);
        }

        public static string Base64Decode(string base64EncodedText)
        {
            var base64Bytes = Convert.FromBase64String(base64EncodedText);
            return Encoding.UTF8.GetString(base64Bytes);
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
            _deploymentsFoldout.value = isApiTokenVerifiedAndContainerReady && _containerUseCustomRegistryToggle.value;

            // + Requires _containerUseCustomRegistryToggleBool
            _containerCustomRegistryWrapper.SetEnabled(isApiTokenVerifiedAndContainerReady && 
                _containerUseCustomRegistryToggle.value);
        }

        private void openGetApiTokenWebsite()
        {
            if (IsLogLevelDebug) Debug.Log("openGetApiTokenWebsite");
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_GET_A_TOKEN_URL);
        }
        
        /// <returns>isSuccess; sets _isContainerRegistryReady + loadedApp</returns>
        private async Task<bool> GetAppAsync()
        {
            if (IsLogLevelDebug) Debug.Log("GetAppAsync");
            
            // Hide previous result labels, disable btns (to reenable when done)
            hideResultLabels();
            _appCreateBtn.SetEnabled(false);
            _apiTokenVerifyBtn.SetEnabled(false);
         
            // Show new loading status
            _appCreateResultLabel.text = EdgegapWindowMetadata.WrapRichTextInColor(
                EdgegapWindowMetadata.LOADING_RICH_STR, 
                EdgegapWindowMetadata.StatusColors.Processing);
            _appCreateResultLabel.visible = true;
            
            EdgegapAppApi appApi = new EdgegapAppApi(
                EdgegapWindowMetadata.API_ENVIRONMENT, 
                _apiTokenInput.value.Trim(),
                EdgegapWindowMetadata.LOG_LEVEL);
            
            EdgegapHttpResult<GetCreateAppResult> getAppResult = await appApi.GetApp(_appNameInput.value);
            onGetCreateApplicationResult(getAppResult);

            return _isContainerRegistryReady;
        }
        
        /// <summary>
        /// TODO: Add err handling for reaching app limit (max 2 for free tier).
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        private async Task createAppAsync()
        {
            if (IsLogLevelDebug) Debug.Log("createAppAsync");
            
            // Hide previous result labels, disable btns (to reenable when done)
            hideResultLabels();
            _appCreateBtn.SetEnabled(false);
            _apiTokenVerifyBtn.SetEnabled(false);
            
            EdgegapAppApi appApi = new EdgegapAppApi(
                EdgegapWindowMetadata.API_ENVIRONMENT, 
                _apiTokenInput.value.Trim(),
                EdgegapWindowMetadata.LOG_LEVEL);

            CreateAppRequest createAppRequest = new(
                _appNameInput.value,
                isActive: true,
                getBase64StrFromSprite(_appIconSpriteObj) ?? "");
            
            EdgegapHttpResult<GetCreateAppResult> createAppResult = await appApi.CreateApp(createAppRequest);
            onGetCreateApplicationResult(createAppResult);
        }

        /// <summary>Get || Create results both handled here. On success, sets _isContainerRegistryReady + loadedApp data</summary>
        /// <param name="result"></param>
        private void onGetCreateApplicationResult(EdgegapHttpResult<GetCreateAppResult> result)
        {
            // Assert the result itself || result's create time exists
            bool isSuccess = result.IsResultCode200 || result.IsResultCode409; // 409 == app already exists
            _isContainerRegistryReady = isSuccess;
            loadedApp = result.Data;

            _appCreateResultLabel.text = getFriendlyCreateAppResultStr(result);
            _containerRegistryFoldout.value = _isContainerRegistryReady;
            _appCreateBtn.SetEnabled(true);
            _apiTokenVerifyBtn.SetEnabled(true);
            SyncContainerEnablesToState();
            
            // Only show status label if we're init'd; otherwise, we auto-tried to get the existing app that
            // we knew had a chance of not being there
            _appCreateResultLabel.visible = IsInitd;
            
            // App base64 img? Parse to sprite, overwrite app image UI/cache
            if (!string.IsNullOrEmpty(loadedApp.Image))
            {
                _appIconSpriteObj = getSpriteFromBase64Str(loadedApp.Image);
                _appIconSpriteObjInput.value = _appIconSpriteObj;
            }
            
            // On fail, shake the "Add more game servers" btn // 400 == # of apps limit reached
            bool isCreate = result.HttpMethod == HttpMethod.Post;
            bool isCreateFailAppNumCapMaxed = isCreate && !_isContainerRegistryReady && result.IsResultCode400;
            if (isCreateFailAppNumCapMaxed)
            {
                ButtonShaker shaker = new ButtonShaker(_footerNeedMoreGameServersBtn);
                _ = shaker.ApplyShakeAsync();
            }
        }
        
        /// <returns>Generally "Success" || "Error: {error}" || "Warning: {error}"</returns>
        private string getFriendlyCreateAppResultStr(EdgegapHttpResult<GetCreateAppResult> createAppResult)
        {
            string coloredResultStr = null;
            
            if (!_isContainerRegistryReady)
            {
                // Error
                string resultStr = $"<b>Error:</b> {createAppResult?.Error?.ErrorMessage}";
                coloredResultStr = EdgegapWindowMetadata.WrapRichTextInColor(
                    resultStr, EdgegapWindowMetadata.StatusColors.Error);
            }
            else if (createAppResult.IsResultCode409)
            {
                // Warn: App already exists - Still success, but just a warn
                string resultStr = $"<b>Warning:</b> {createAppResult.Error.ErrorMessage}";
                coloredResultStr = EdgegapWindowMetadata.WrapRichTextInColor(
                    resultStr, EdgegapWindowMetadata.StatusColors.Warn);
            }
            else
            {
                // Success
                coloredResultStr = EdgegapWindowMetadata.WrapRichTextInColor(
                    "Success", EdgegapWindowMetadata.StatusColors.Success);
            }

            return coloredResultStr;
        }

        /// <summary>Open contact form in desired locale</summary>
        private void openNeedMoreGameServersWebsite()
        {
            //// TODO: Localized contact form
            // bool isFrenchLocale = Application.systemLanguage == SystemLanguage.French;
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_CONTACT_EN_URL);
        }
        
        private void openDocumentationWebsite()
        {
            string documentationUrl = _apiEnvironment.GetDocumentationUrl();

            if (!string.IsNullOrEmpty(documentationUrl))
                Application.OpenURL(documentationUrl);
            else
            {
                string apiEnvName = Enum.GetName(typeof(ApiEnvironment), _apiEnvironment);
                Debug.LogWarning($"Could not open documentation for api environment {apiEnvName}: No documentation URL.");
            }
        }
        
        
        #region Untested Legacy V1 Code
        private readonly System.Timers.Timer _updateServerStatusCronjob = 
            new(EdgegapWindowMetadata.SERVER_STATUS_CRON_JOB_INTERVAL_MS);
        
        private bool _shouldUpdateServerStatus = false;
        private string _deploymentRequestId;
        private string _userExternalIp;
        // [SerializeField] private bool _autoIncrementTag = true; // TODO? Used by v1 api
        // [SerializeField] private string _containerImageTag; // TODO? Used by v1 api
        
        // private void loadRegisterInitServerDataUiElements()
        // {
        //     VisualElement serverDataElement = EdgegapServerDataManager.GetServerDataVisualTree();
        //     EdgegapServerDataManager.RegisterServerDataContainer(serverDataElement);
        //     
        //     _deploymentServerDataContainer.Clear();
        //     _deploymentServerDataContainer.Add(serverDataElement);
        // }
        
        // protected void OnDestroy()
        // {
        //     bool deploymentActive = !string.IsNullOrEmpty(_deploymentRequestId);
        //
        //     if (!deploymentActive)
        //         return;
        //     
        //     EditorUtility.DisplayDialog(
        //         "Warning",
        //         $"You have an active deployment ({_deploymentRequestId}) that won't be stopped automatically.",
        //         "Ok"
        //     );
        // }
        
        // protected void Update()
        // {
        //     if (!_shouldUpdateServerStatus)
        //         return;
        //     
        //     _shouldUpdateServerStatus = false;
        //     updateServerStatusAsync();
        // }
        
        // private void RestoreActiveDeployment()
        // {
        //     ConnectCallback();
        //
        //     _shouldUpdateServerStatus = true;
        //     StartServerStatusCronjob();
        // }
        
        // /// <summary>Old "Verify" btn from legacy v1?</summary>
        // /// <param name="selectedApiEnvironment"></param>
        // /// <param name="selectedAppName"></param>
        // /// <param name="selectedAppVersionName"></param>
        // /// <param name="selectedApiTokenValue"></param>
        // [Obsolete("If verifying, apiToken, see v2's verifyApiTokenGetRegistryCredsAsync()")]
        // private async void Connect(
        //     ApiEnvironment selectedApiEnvironment,
        //     string selectedAppName,
        //     string selectedAppVersionName,
        //     string selectedApiTokenValue
        // )
        // {
        //     SetToolUIState(ToolState.Connecting);
        //
        //     _httpClient.BaseAddress = new Uri(selectedApiEnvironment.GetApiUrl());
        //
        //     string path = $"/v1/app/{selectedAppName}/version/{selectedAppVersionName}";
        //
        //     // Headers
        //     _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        //     _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", selectedApiTokenValue);
        //
        //     // Make HTTP request
        //     HttpResponseMessage response = await _httpClient.GetAsync(path);
        //
        //     if (response.IsSuccessStatusCode)
        //     {
        //         SyncObjectWithForm();
        //         SetToolUIState(ToolState.Connected);
        //     }
        //     else
        //     {
        //         int status = (int)response.StatusCode;
        //         string title;
        //         string message;
        //
        //         if (status == 401)
        //         {
        //             string apiEnvName = Enum.GetName(typeof(ApiEnvironment), selectedApiEnvironment);
        //             title = "Invalid credentials";
        //             message = $"Could not find an Edgegap account with this API key for the {apiEnvName} environment.";
        //         }
        //         else if (status == 404)
        //         {
        //             title = "App not found";
        //             message = $"Could not find app {selectedAppName} with version {selectedAppVersionName}.";
        //         }
        //         else
        //         {
        //             title = "Oops";
        //             message = $"There was an error while connecting you to the Edgegap API. Please try again later.";
        //         }
        //
        //         EditorUtility.DisplayDialog(title, message, "Ok");
        //         SetToolUIState(ToolState.Disconnected);
        //     }
        // }
        
        // private void ConnectCallback()
        // {
        //     string selectedAppName = _appNameInput.value;
        //     string selectedVersionName = _appVersionName; // TODO: Hard-coded while unused in UI
        //     string selectedApiKey = _apiTokenInputUnmasked.Value;
        //
        //     bool validAppName = !string.IsNullOrEmpty(selectedAppName) && !string.IsNullOrWhiteSpace(selectedAppName);
        //     bool validVersionName = !string.IsNullOrEmpty(selectedVersionName) && !string.IsNullOrWhiteSpace(selectedVersionName);
        //     bool validApiKey = selectedApiKey.StartsWith("token ");
        //     bool isValidToConnect = validAppName && validVersionName && validApiKey; 
        //     
        //     if (!isValidToConnect)
        //     {
        //         EditorUtility.DisplayDialog(
        //             "Could not connect - Invalid data",
        //             "The data provided is invalid. " +
        //             "Make sure every field is filled, and that you provide your complete Edgegap API token " +
        //             "(including the \"token\" part).", 
        //             "Ok"
        //         );
        //         return;
        //     }
        //
        //     string apiKeyValue = selectedApiKey.Substring(6);
        //     Connect(_apiEnvironment, selectedAppName, selectedVersionName, apiKeyValue);
        // }
        
        /// <summary>
        /// With a call to an external resource, determines the current user's public IP address.
        /// </summary>
        /// <returns>External IP address</returns>
        private string GetExternalIpAddress()
        {
            string externalIpString = new WebClient()
                .DownloadString("https://icanhazip.com")
                .Replace("\\r\\n", "")
                .Replace("\\n", "")
                .Trim();
            IPAddress externalIp = IPAddress.Parse(externalIpString);

            return externalIp.ToString();
        }

        // private void DisconnectCallback()
        // {
        //     if (string.IsNullOrEmpty(_deploymentRequestId))
        //         SetToolUIState(ToolState.Disconnected);
        //     else
        //     {
        //         EditorUtility.DisplayDialog(
        //             "Cannot disconnect", 
        //             "Make sure no server is running in the Edgegap tool before disconnecting", 
        //             "Ok");
        //     }
        // }

        private float ProgressCounter = 0;

        private void ShowBuildWorkInProgress(string status) =>
            EditorUtility.DisplayProgressBar(
                "Build and push progress", 
                status, 
                ProgressCounter++ / 50);

        /// <summary>Build & Push - Legacy from v1, modified for v2</summary>
        private async Task buildAndPushServerAsync()
        {
            if (IsLogLevelDebug) Debug.Log("buildAndPushServerAsync");

            // Legacy Code Start >>
            
            // SetToolUIState(ToolState.Building);
            SyncObjectWithForm();
            ProgressCounter = 0;
            
            try
            {
                // check for installation and setup docker file
                if (!await EdgegapBuildUtils.DockerSetupAndInstalationCheck())
                {
                    onBuildPushError("Docker installation not found. " +
                        "Docker can be downloaded from:\n\nhttps://www.docker.com/");
                    return;
                }

                if (!EdgegapWindowMetadata.SKIP_SERVER_BUILD_WHEN_PUSHING)
                {
                    // create server build
                    BuildReport buildResult = EdgegapBuildUtils.BuildServer();
                    if (buildResult.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    {
                        onBuildPushError("Edgegap build failed");
                        return;
                    }    
                }
                else
                    Debug.LogWarning(nameof(EdgegapWindowMetadata.SKIP_SERVER_BUILD_WHEN_PUSHING));

                string registry = _containerRegistryUrlInput.value;
                string imageName = _containerImageRepositoryInput.value;
                string tag = _containerNewTagVersionInput.value;
                
                // // increment tag for quicker iteration // TODO? `_autoIncrementTag` !exists in V2.
                // if (_autoIncrementTag)
                // {
                //     tag = EdgegapBuildUtils.IncrementTag(tag);
                // }

                // create docker image
                if (!EdgegapWindowMetadata.SKIP_DOCKER_IMAGE_BUILD_WHEN_PUSHING)
                {
                    await EdgegapBuildUtils.DockerBuild(
                        registry,
                        imageName,
                        tag,
                        ShowBuildWorkInProgress);
                    SetToolUIState(ToolState.Pushing);
                }
                else
                    Debug.LogWarning(nameof(EdgegapWindowMetadata.SKIP_DOCKER_IMAGE_BUILD_WHEN_PUSHING));

                // (v2) Login to registry
                bool isContainerLoginSuccess = await EdgegapBuildUtils.LoginContainerRegistry(
                    _containerRegistryUrlInput.value, 
                    _containerUsernameInput.value,
                    _containerTokenInput.value,
                    ShowBuildWorkInProgress);

                if (!isContainerLoginSuccess)
                {
                    onBuildPushError("Unable to login to docker registry. " +
                        "Make sure your registry url + username are correct. " +
                        $"See doc:\n\n{EdgegapWindowMetadata.EDGEGAP_HOW_TO_LOGIN_VIA_CLI_DOC_URL}");
                    return;
                }

                // push docker image
                bool isPushSuccess = await EdgegapBuildUtils.DockerPush(
                        registry,
                        imageName,
                        tag,
                        ShowBuildWorkInProgress);
                
                if (!isPushSuccess)
                {
                    onBuildPushError("Unable to push docker image to registry. " +
                        $"Make sure your {registry} registry url + username are correct. " +
                        $"See doc:\n\n{EdgegapWindowMetadata.EDGEGAP_HOW_TO_LOGIN_VIA_CLI_DOC_URL}");
                    return;
                }

                // update edgegap server settings for new tag
                ShowBuildWorkInProgress("Updating server info on Edgegap");
                EdgegapAppApi appApi = new(_apiEnvironment, _apiTokenInput.value);
                
                UpdateAppVersionRequest updateAppVerReq = new(_appNameInput.value)
                {
                    VersionName = _containerNewTagVersionInput.value,
                    DockerImage = imageName,
                    DockerRepository = registry,
                    DockerTag = tag,
                };

                EdgegapHttpResult<UpsertAppVersionResult> updateAppVersionResult = await appApi.UpsertAppVersion(updateAppVerReq);

                if (updateAppVersionResult.HasErr)
                {
                    onBuildPushError($"Unable to update docker tag/version:\n{updateAppVersionResult.Error.ErrorMessage}");
                    return;
                }

                // cleanup
                onBuildAndPushSuccess(tag);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError(ex);
                onBuildPushError("Edgegap build and push failed");
            }
        }

        private void onBuildAndPushSuccess(string tag)
        {
            // _containerImageTag = tag; // TODO?
            syncFormWithObjectStatic();
            EditorUtility.ClearProgressBar();
            SetToolUIState(ToolState.Connected);
            
            _containerBuildAndPushResultLabel.text = $"Success ({tag})";
            _containerBuildAndPushResultLabel.visible = true;
                
            Debug.Log("Server built and pushed successfully");
        }

        /// <summary>(v2) Docker cmd error, detected by "ERROR" in log stream.</summary>
        private void onBuildPushError(string msg)
        {
            EditorUtility.DisplayDialog("Error", msg, "Ok");
            SetToolUIState(ToolState.Connected);
            EditorUtility.ClearProgressBar();
        }
        
        // [Obsolete("Use EdgegapAppApi.UpdateAppVersion")]
        // /// <summary>
        // /// Legacy code, slightly edited to accommodate v2.
        // /// TODO: Revamp to use ApiBase scripts.
        // /// </summary>
        // /// <param name="newTag"></param>
        // /// <exception cref="Exception"></exception>
        // private async Task updateAppTagOnEdgegap(string newTag)
        // {
        //     string relativePath = $"/v1/app/{_appNameInput.value}/version/{_appVersionName}";
        //
        //     // Setup post data
        //     AppVersionUpdatePatchData updatePatchData = new AppVersionUpdatePatchData
        //     {
        //         DockerImage = _containerImageRepositoryInput.value, 
        //         DockerRepository = _containerRegistryUrlInput.value, 
        //         DockerTag = newTag,
        //     };
        //     string json = JsonConvert.SerializeObject(updatePatchData);
        //     StringContent patchData = new StringContent(json, Encoding.UTF8, "application/json");
        //
        //     // Make HTTP request
        //     string fullUri = _apiEnvironment.GetApiUrl() + relativePath;
        //     HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), fullUri);
        //     request.Content = patchData;
        //
        //     HttpResponseMessage response = await _httpClient.SendAsync(request);
        //     string content = await response.Content.ReadAsStringAsync();
        //
        //     if (!response.IsSuccessStatusCode)
        //     {
        //         throw new Exception($"Could not update Edgegap server tag. " +
        //             $"Got {(int)response.StatusCode} with response:\n{content}");
        //     }
        // }

        /// <summary>Legacy from v1 - untested</summary>
        private async Task startServerCallbackAsync()
        {
            if (IsLogLevelDebug) Debug.Log("startServerCallbackAsync");
            hideResultLabels();
            
            SetToolUIState(ToolState.ProcessingDeployment); // Prevents being called multiple times.

            const string path = "/v1/deploy";
            
            // Setup post data
            DeployPostData deployPostData = new DeployPostData(
                _appNameInput.value, 
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

                updateServerStatusAsync();
                StartServerStatusCronjob();
            }
            else
            {
                Debug.LogError($"Could not start Edgegap server. " +
                    $"Got {(int)response.StatusCode} with response:\n{content}");
                SetToolUIState(ToolState.Connected);
            }
        }

        /// <summary>Legacy from v1 - untested</summary>
        private async Task stopServerCallbackAsync()
        {
            throw new NotImplementedException("TODO: stopServerCallbackAsync (legacy code not yet tested to be compatible with v2");
            
            if (IsLogLevelDebug) Debug.Log("stopServerCallbackAsync");
            hideResultLabels();
            
            string path = $"/v1/stop/{_deploymentRequestId}";

            // Make HTTP request
            HttpResponseMessage response = await _httpClient.DeleteAsync(path);

            if (response.IsSuccessStatusCode)
            {
                updateServerStatusAsync();
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

        /// <summary>Legacy from v1 - untested</summary>
        private async Task updateServerStatusAsync()
        {
            throw new NotImplementedException("TODO: updateServerStatusAsync (legacy code not yet tested to be compatible with v2");
            
            if (IsLogLevelDebug) Debug.Log("updateServerStatusAsync");
            hideResultLabels();
            
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
            setServerActionUI(toolState); // Desync'd in V2
            SetDockerRepoInfoUI(toolState);
            // SetConnectionButtonUI(toolState); // Unused in v2
        }
        
        /// <summary>
        /// Legacy code, slightly edited to prevent syntax errs with V2. TODO: Use ApiBase.
        /// </summary>
        private void setServerActionUI(ToolState toolState)
        {
            bool canStartDeployment = toolState.CanStartDeployment();
            bool canStopDeployment = toolState.CanStopDeployment();

            // A bit dirty, but ensures the callback is not bound multiple times on the button.
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= StartServerCallback;
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= StopServerCallback;

            _deploymentConnectionServerActionStopBtn.SetEnabled(canStartDeployment || canStopDeployment);
            _containerBuildAndPushServerBtn.SetEnabled(canStartDeployment);

            if (canStopDeployment)
            {
                _deploymentConnectionServerActionStopBtn.text = "Stop Server";
                _deploymentConnectionServerActionStopBtn.clickable.clicked += StopServerCallback;
            }
            else
            {
                _deploymentConnectionServerActionStopBtn.text = "Start Server";
                _deploymentConnectionServerActionStopBtn.clickable.clicked += StartServerCallback;
            }
        }

        private void SetDockerRepoInfoUI(ToolState toolState)
        {
            bool connected = toolState.CanStartDeployment();
            _containerRegistryUrlInput.SetEnabled(connected);
            _containerImageRepositoryInput.SetEnabled(connected);
            _containerBuildAndPushServerBtn.SetEnabled(connected);
            
            // // Unused in v2 >>
            // _autoIncrementTagInput.SetEnabled(connected);
            // _containerImageTagInput.SetEnabled(connected);
        }
        
        private void SetServerActionUI(ToolState toolState)
        {
            bool canStartDeployment = toolState.CanStartDeployment();
            bool canStopDeployment = toolState.CanStopDeployment();

            // A bit dirty, but ensures the callback is not bound multiple times on the button.
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= StartServerCallback;
            _deploymentConnectionServerActionStopBtn.clickable.clicked -= StopServerCallback;

            _deploymentConnectionServerActionStopBtn.SetEnabled(canStartDeployment || canStopDeployment);

            _containerBuildAndPushServerBtn.SetEnabled(canStartDeployment);

            if (canStopDeployment)
            {
                _deploymentConnectionServerActionStopBtn.text = "Stop Server";
                _deploymentConnectionServerActionStopBtn.clickable.clicked += StopServerCallback;
            }
            else
            {
                _deploymentConnectionServerActionStopBtn.text = "Start Server";
                _deploymentConnectionServerActionStopBtn.clickable.clicked += StartServerCallback;
            }
        }
        
        /// <summary>
        /// Legacy code, slightly edited to use explicit vars + prevent syntax errs with V2. TODO: Use ApiBase.
        /// </summary>
        private async void StartServerCallback()
        {
            SetToolUIState(ToolState.ProcessingDeployment); // Prevents being called multiple times.

            const string path = "/v1/deploy";

            // Setup post data
            DeployPostData deployPostData = new DeployPostData(
                _appNameInput.value, 
                _appVersionName, 
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

                UpdateServerStatus();
                StartServerStatusCronjob();
            }
            else
            {
                Debug.LogError($"Could not start Edgegap server. " +
                    $"Got {(int)response.StatusCode} with response:\n{content}");
                
                SetToolUIState(ToolState.Connected);
            }
        }
        
        /// <summary>
        /// Legacy code, slightly edited to use explicit vars + prevent syntax errs with V2. TODO: Use ApiBase.
        /// </summary>
        private async void StopServerCallback()
        {
            string path = $"/v1/stop/{_deploymentRequestId}";

            // Make HTTP request
            HttpResponseMessage response = await _httpClient.DeleteAsync(path);

            if (response.IsSuccessStatusCode)
            {
                UpdateServerStatus();
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
        
        /// <summary>
        /// Legacy code, slightly edited to use explicit vars + prevent syntax errs with V2. TODO: Use ApiBase.
        /// </summary>
        private async void UpdateServerStatus()
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

                if (serverStatus is ServerStatus.Ready or ServerStatus.Error)
                {
                    toolState = ToolState.DeploymentRunning;
                }
                else
                {
                    toolState = ToolState.ProcessingDeployment;
                }
            }

            SetToolUIState(toolState);
        }

        private void SetConnectionInfoUI(ToolState toolState)
        {
            bool canEditConnectionInfo = toolState.CanEditConnectionInfo();

            _apiTokenInput.SetEnabled(canEditConnectionInfo);
            _appNameInput.SetEnabled(canEditConnectionInfo);
            
            // // Unused in v2 >>
            // _apiEnvironmentSelect.SetEnabled(canEditConnectionInfo);
            // _appVersionNameInput.SetEnabled(canEditConnectionInfo);
       
        }

        /// <summary>
        /// Save the tool's serializable data to the EditorPrefs to allow persistence across restarts.
        /// Any field with [SerializeField] will be saved.
        /// </summary>
        private void SaveToolData()
        {
            string data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(EdgegapWindowMetadata.EDITOR_DATA_SERIALIZATION_NAME, data);
        }

        /// <summary>
        /// Load the tool's serializable data from the EditorPrefs to the object, restoring the tool's state.
        /// </summary>
        private void LoadToolData()
        {
            string data = EditorPrefs.GetString(
                EdgegapWindowMetadata.EDITOR_DATA_SERIALIZATION_NAME, 
                JsonUtility.ToJson(this, false));
            
            JsonUtility.FromJsonOverwrite(data, this);
        }
        #endregion // Untested Legacy V1 Code 
    }
}