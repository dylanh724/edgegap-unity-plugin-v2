using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
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
        private VisualTreeAsset _visualTree;
        private bool _isApiTokenVerified; // Toggles the rest of the UI
        private bool _isContainerRegistryReady;
        private Sprite _appIconSpriteObj;
        private string _appIconBase64Str;
        private ApiEnvironment _apiEnvironment; // TODO: Swap out hard-coding with UI element?
        private string _appVersionName; // TODO: Swap out hard-coding with UI element?
        #endregion // Vars
        
        
        #region Vars -> Interactable Elements
        private Button _debugBtn;
        
        /// <summary>(!) This will only contain `*` chars: For the real token, see `_apiTokenInputUnmaskedStr`.</summary>
        private TextField _apiTokenInput;
        
        private Button _apiTokenVerifyBtn;
        private Button _apiTokenGetBtn;
        private VisualElement _postAuthContainer;

        private Foldout _appInfoFoldout;
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

        public void CreateGUI()
        {
            // Get UI elements from UI Builder
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            // Register callbacks and sync UI builder elements to fields here
            InitUIElements();
            syncFormWithObjectStatic();
            syncFormWithObjectDynamic();

            #region Legacy code from v1 // TODO - Look into what this does
            // If we cached a deploymentId, restore the settings
            // bool hasActiveDeployment = !string.IsNullOrEmpty(_deploymentRequestId);
            //
            // if (hasActiveDeployment)
            //     RestoreActiveDeployment();
            // else
            //     DisconnectCallback();
            #endregion // Legacy code from v1: TODO - Look into what this does
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

        /// <summary>
        /// Sanity check: If we changed an #Id, we need to know early so we can update the const.
        /// </summary>
        private void assertVisualElementKeys()
        {
            try
            {
                Assert.IsNotNull(_apiTokenInput, $"Expected {nameof(_apiTokenInput)} via #{EdgegapWindowMetadata.API_TOKEN_TXT_ID}");
                Assert.IsNotNull(_apiTokenVerifyBtn, $"Expected {nameof(_apiTokenVerifyBtn)} via #{EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID}");
                Assert.IsNotNull(_apiTokenGetBtn, $"Expected {nameof(_apiTokenGetBtn)} via #{EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID}");
                Assert.IsNotNull(_postAuthContainer, $"Expected {nameof(_postAuthContainer)} via #{EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID}");
                
                Assert.IsNotNull(_appInfoFoldout, $"Expected {nameof(_appInfoFoldout)} via #{EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID}");
                Assert.IsNotNull(_appNameInput, $"Expected {nameof(_appNameInput)} via #{EdgegapWindowMetadata.APP_NAME_TXT_ID}");
                Assert.IsNotNull(_appIconSpriteObjInput, $"Expected {nameof(_appIconSpriteObjInput)} via #{EdgegapWindowMetadata.APP_ICON_SPRITE_OBJ_ID}");
                Assert.IsNotNull(_appCreateBtn, $"Expected {nameof(_appCreateBtn)} via #{EdgegapWindowMetadata.APP_CREATE_BTN_ID}");
                Assert.IsNotNull(_appCreateResultLabel, $"Expected {nameof(_appCreateResultLabel)} via #{EdgegapWindowMetadata.APP_CREATE_RESULT_LABEL_ID}");

                Assert.IsNotNull(_containerRegistryFoldout, $"Expected {nameof(_containerRegistryFoldout)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_FOLDOUT_ID}");
                Assert.IsNotNull(_containerUseCustomRegistryToggle, $"Expected {nameof(_containerUseCustomRegistryToggle)} via #{EdgegapWindowMetadata.CONTAINER_USE_CUSTOM_REGISTRY_TOGGLE_ID}");
                Assert.IsNotNull(_containerCustomRegistryWrapper, $"Expected {nameof(_containerCustomRegistryWrapper)} via #{EdgegapWindowMetadata.CONTAINER_CUSTOM_REGISTRY_WRAPPER_ID}");
                Assert.IsNotNull(_containerRegistryUrlInput, $"Expected {nameof(_containerRegistryUrlInput)} via #{EdgegapWindowMetadata.CONTAINER_REGISTRY_URL_TXT_ID}");
                Assert.IsNotNull(_containerImageRepositoryInput, $"Expected {nameof(_containerImageRepositoryInput)} via #{EdgegapWindowMetadata.CONTAINER_IMAGE_REPOSITORY_URL_TXT_ID}");
                Assert.IsNotNull(_containerUsernameInput, $"Expected {nameof(_containerUsernameInput)} via #{EdgegapWindowMetadata.CONTAINER_USERNAME_TXT_ID}");
                Assert.IsNotNull(_containerTokenInput, $"Expected {nameof(_containerTokenInput)} via #{EdgegapWindowMetadata.CONTAINER_TOKEN_TXT_ID}");
                Assert.IsNotNull(_containerBuildAndPushServerBtn, $"Expected {nameof(_containerBuildAndPushServerBtn)} via #{EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_BTN_ID}");

                Assert.IsNotNull(_deploymentsFoldout, $"Expected {nameof(_deploymentsFoldout)} via #{EdgegapWindowMetadata.DEPLOYMENTS_FOLDOUT_ID}");
                Assert.IsNotNull(_deploymentsRefreshBtn, $"Expected {nameof(_deploymentsRefreshBtn)} via #{EdgegapWindowMetadata.DEPLOYMENTS_REFRESH_BTN_ID}");
                Assert.IsNotNull(_deploymentCreateBtn, $"Expected {nameof(_deploymentCreateBtn)} via #{EdgegapWindowMetadata.DEPLOYMENT_CREATE_BTN_ID}");
                Assert.IsNotNull(_deploymentServerDataContainer, $"Expected {nameof(_deploymentServerDataContainer)} via #{EdgegapWindowMetadata.DEPLOYMENTS_CONTAINER_ID}");
                Assert.IsNotNull(_deploymentConnectionUrlLabel, $"Expected {nameof(_deploymentConnectionUrlLabel)} via #{EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_URL_LABEL_ID}");
                Assert.IsNotNull(_deploymentConnectionStatusLabel, $"Expected {nameof(_deploymentConnectionStatusLabel)} via #{EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_STATUS_LABEL_ID}");
                Assert.IsNotNull(_deploymentConnectionServerActionStopBtn, $"Expected {nameof(_deploymentConnectionServerActionStopBtn)} via #{EdgegapWindowMetadata.DEPLOYMENT_CONNECTION_SERVER_ACTION_STOP_BTN_ID}");
                
                Assert.IsNotNull(_footerDocumentationBtn, $"Expected {nameof(_footerDocumentationBtn)} via #{EdgegapWindowMetadata.FOOTER_DOCUMENTATION_BTN_ID}");
                Assert.IsNotNull(_footerNeedMoreGameServersBtn, $"Expected {nameof(_footerNeedMoreGameServersBtn)} via #{EdgegapWindowMetadata.FOOTER_NEED_MORE_GAME_SERVERS_BTN_ID}");
                
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
            _debugBtn.visible = EdgegapWindowMetadata.SHOW_DEBUG_BTN;
            
            string apiTokenBase64Str = PlayerPrefs.GetString(EdgegapWindowMetadata.API_TOKEN_KEY_STR_PREF_ID, null);
            if (apiTokenBase64Str != null)
                _apiTokenInput.SetValueWithoutNotify(Base64Decode(apiTokenBase64Str));
        }
        
        /// <summary>For example, result labels (success/err) should be hidden on init</summary>
        private void hideResultLabels()
        {
            _appCreateResultLabel.visible = false;
        }

        /// <summary>
        /// Register non-btn change actionss. We'll want to save for persistence, validate, etc
        /// </summary>
        private void registerFieldCallbacks()
        {
            _apiTokenInput.RegisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.RegisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _containerUseCustomRegistryToggle.RegisterValueChangedCallback(onContainerUseCustomRegistryToggle);
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
            _debugBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.DEBUG_BTN_ID);
            
            _apiTokenInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.API_TOKEN_TXT_ID);
            _apiTokenVerifyBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_VERIFY_BTN_ID);
            _apiTokenGetBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.API_TOKEN_GET_BTN_ID);
            _postAuthContainer = rootVisualElement.Q<VisualElement>(EdgegapWindowMetadata.POST_AUTH_CONTAINER_ID);
            
            _appInfoFoldout = rootVisualElement.Q<Foldout>(EdgegapWindowMetadata.APP_INFO_FOLDOUT_ID);
            _appNameInput = rootVisualElement.Q<TextField>(EdgegapWindowMetadata.APP_NAME_TXT_ID);
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
            _containerBuildAndPushServerBtn = rootVisualElement.Q<Button>(EdgegapWindowMetadata.CONTAINER_BUILD_AND_PUSH_BTN_ID);

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
        
        
        #region Init -> Button clicks
        // ##########################################################
        // TODO UX: Disable btn on click
        // TODO UX: Show loading spinner on click
        // TODO UX: Restore btn on done (and hide loading spinner)
        // TODO UX: On success, reflect UI 
        // TODO UX: On error, reflect UI
        // ##########################################################

        /// <summary>
        /// Experiment here! You may want to log what you're doing
        /// in case you inadvertently leave it on.
        /// </summary>
        private void onDebugBtnClick()
        {
            Debug.Log("onDebugBtnClick: Enabling foldout groups");
            _appInfoFoldout.SetEnabled(true);
            _appInfoFoldout.SetEnabled(true);
            _containerRegistryFoldout.SetEnabled(true);
            _deploymentsFoldout.SetEnabled(true);
        }
        
        private void onApiTokenVerifyBtnClick() => verifyApiTokenGetRegistryCreds();
        private void onApiTokenGetBtnClick() => getApiToken();
        private void onAppCreateBtnClick() => createApplication();
        private void onContainerBuildAndPushServerBtnClick() => buildAndPushServer();
        private void onDeploymentsRefreshBtnClick() => updateServerStatus();
        private void onDeploymentCreateBtnClick() => startServerCallback();
        private void onDeploymentServerActionStopBtnClick() => stopServerCallback();
        private void onFooterDocumentationBtnClick() => openDocumentationCallback();
        private void onFooterNeedMoreGameServersBtnClick() => openNeedMoreGameServersWebsite();
        #endregion // Init -> /Button Clicks
        #endregion // Init

        
        /// <summary>
        /// Prevents memory leaks, mysterious errors and "ghost" values set from a previous session.
        /// </summary>
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

        private void unregisterFieldCallbacks()
        {
            _apiTokenInput.UnregisterValueChangedCallback(onApiTokenInputChanged);
            _apiTokenInput.UnregisterCallback<FocusOutEvent>(onApiTokenInputFocusOut);

            _containerUseCustomRegistryToggle.UnregisterValueChangedCallback(onContainerUseCustomRegistryToggle);
        }
        
        private void SyncObjectWithForm()
        {
            _appIconSpriteObj = _appIconSpriteObjInput.value as Sprite;
        }

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
        }

        /// <summary>Sync form further based on API call results.</summary>
        private async Task syncFormWithObjectDynamic()
        {
            if (string.IsNullOrEmpty(_apiTokenInput.value))
                return;
            
            if (IsLogLevelDebug) Debug.Log("syncFormWithObjectDynamic: Found APIToken; " +
                "calling verifyApiTokenGetRegistryCreds");
            
            await verifyApiTokenGetRegistryCreds();
        }
        

        #region Immediate non-button changes
        /// <summary>
        /// - There's no built-in way to add a password (*) char mask in UI Builder,
        /// so we do it manually on change, storing the actual value elsewhere
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
            readableTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.ReleaseTemporary(rt);
            return readableTexture;
        }

        /// <summary>From Sprite -> to Base64</summary>
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
        private async Task verifyApiTokenGetRegistryCreds()
        {
            if (IsLogLevelDebug) Debug.Log("verifyApiTokenGetRegistryCreds");
            
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
                PlayerPrefs.SetString(EdgegapWindowMetadata.API_TOKEN_KEY_STR_PREF_ID, Base64Encode(_apiTokenInput.value));
                prefillContainerRegistryForm(getRegistryCredentialsResult.Data);
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

            _containerUseCustomRegistryToggle.value = true;
            _containerRegistryUrlInput.value = credentials.RegistryUrl;
            _containerImageRepositoryInput.value = credentials.Project;
            _containerUsernameInput.value = credentials.Username;
            _containerTokenInput.value = credentials.Token;
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
            _containerRegistryFoldout.value = isApiTokenVerifiedAndContainerReady && _containerUseCustomRegistryToggle.value;
            
            _deploymentsFoldout.SetEnabled(isApiTokenVerifiedAndContainerReady);
            _deploymentsFoldout.value = isApiTokenVerifiedAndContainerReady && _containerUseCustomRegistryToggle.value;

            // + Requires _containerUseCustomRegistryToggleBool
            _containerCustomRegistryWrapper.SetEnabled(isApiTokenVerifiedAndContainerReady && 
                _containerUseCustomRegistryToggle.value);
        }

        private void getApiToken()
        {
            if (IsLogLevelDebug) Debug.Log("getApiToken");
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_CREATE_TOKEN_URL);
        }
        
        /// <summary>
        /// TODO: Add err handling for reaching app limit (max 2 for free tier).
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        private async Task createApplication()
        {
            if (IsLogLevelDebug) Debug.Log("createApplication");
            
            // Hide previous result labels, disable btns (to reenable when done)
            hideResultLabels();
            _appCreateBtn.SetEnabled(false);
            _apiTokenVerifyBtn.SetEnabled(false);
            
            EdgegapAppApi appApi = new EdgegapAppApi(
                EdgegapWindowMetadata.API_ENVIRONMENT, 
                _apiTokenInput.value.Trim(),
                EdgegapWindowMetadata.LOG_LEVEL);

            CreateApplicationRequest createAppRequest = new()
            {
                AppName = _appNameInput.value,
                Image = getBase64StrFromSprite(_appIconSpriteObj) ?? "",
                IsActive = true,
            };
            
            EdgegapHttpResult<CreateApplicationResult> result = await appApi.CreateApp(createAppRequest);
            onCreateApplicationResult(result);
        }

        private void onCreateApplicationResult(EdgegapHttpResult<CreateApplicationResult> result)
        {
            // Assert the result itself || result's create time exists
            _isContainerRegistryReady = result.IsResultCode200;;

            string resultColorHex = _isContainerRegistryReady 
                ? EdgegapWindowMetadata.SUCCESS_COLOR_HEX 
                : EdgegapWindowMetadata.FAIL_COLOR_HEX;
            
            string resultText = _isContainerRegistryReady
                ? "Success"
                : $"<b>Error:</b> {result.Error.ErrorMessage}";
            
            _appCreateResultLabel.text = $"<color={resultColorHex}>{resultText}</color>";
            _appCreateResultLabel.visible = true;

            _appCreateBtn.SetEnabled(true);
            _apiTokenVerifyBtn.SetEnabled(true);
            SyncContainerEnablesToState();
            
            // On fail, shake the "Add more game servers" btn // 400 == # of apps limit reached
            if (!_isContainerRegistryReady && result.IsResultCode400)
            {
                ButtonShaker shaker = new ButtonShaker(_footerNeedMoreGameServersBtn);
                _ = shaker.ApplyShake();
            }
        }

        /// <summary>Open contact form in desired locale</summary>
        private void openNeedMoreGameServersWebsite()
        {
            //// TODO: Localized contact form
            // bool isFrenchLocale = Application.systemLanguage == SystemLanguage.French;
            Application.OpenURL(EdgegapWindowMetadata.EDGEGAP_CONTACT_EN_URL);
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
        //     updateServerStatus();
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
        // [Obsolete("If verifying, apiToken, see v2's verifyApiTokenGetRegistryCreds()")]
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

        /// <summary>Legacy from v1 - untested</summary>
        private async void buildAndPushServer()
        {
            if (IsLogLevelDebug) Debug.Log("buildAndPushServer");
            hideResultLabels();
            
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

                string registry = _containerRegistryUrlInput.value;
                string imageName = _containerImageRepositoryInput.value;
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
                syncFormWithObjectStatic();
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

        /// <summary>Legacy from v1 - untested</summary>
        private async void startServerCallback()
        {
            if (IsLogLevelDebug) Debug.Log("startServerCallback");
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

        /// <summary>Legacy from v1 - untested</summary>
        private async void stopServerCallback()
        {
            if (IsLogLevelDebug) Debug.Log("stopServerCallback");
            hideResultLabels();
            
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

        /// <summary>Legacy from v1 - untested</summary>
        private async void updateServerStatus()
        {
            if (IsLogLevelDebug) Debug.Log("updateServerStatus");
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

            _apiTokenInput.SetEnabled(canEditConnectionInfo);
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