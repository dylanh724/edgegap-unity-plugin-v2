using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using IO.Swagger.Model;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Edgegap.Editor
{
    /// <summary>
    /// Editor logic event handler for "UI Builder" EdgegapWindow.uxml
    /// V2, where `EdgegapWindow.cs`
    /// </summary>
    public class EdgegapWindowV2 : EditorWindow
    {
        static readonly HttpClient _httpClient = new();

        private const string EditorDataSerializationName = "EdgegapSerializationData";
        private const int ServerStatusCronjobIntervalMs = 10000; // Interval at which the server status is updated

        private readonly System.Timers.Timer _updateServerStatusCronjob = new(ServerStatusCronjobIntervalMs);

        private VisualTreeAsset _visualTree;
        private bool _shouldUpdateServerStatus = false;
        
        
        #region Serialized fields for Editor value persistence
        // Editor persistence >> Not from UI
        [SerializeField] private string _userExternalIp;
        [SerializeField] private string _deploymentRequestId;
        
        // Editor persistence >> From UI >> Header
        [SerializeField] private string _apiTokenInputStr;
        
        // Editor persistence >>  From UI >> Application Info
        [SerializeField] private string _appNameInputStr;
        [SerializeField] private string _appIconPathInputStr; // New in V2
        
        // Editor persistence >> From UI >> Container Registry
        [SerializeField] private bool _containerUseCustomRegistryToggleBool; // New in V2
        [SerializeField] private string _containerRegistryUrlInputStr;
        [SerializeField] private string _containerImageRepositoryInputStr;
        
        // // Unused in v2 UI >>
        // [SerializeField] private ApiEnvironment _apiEnvironment;
        // [SerializeField] private bool _autoIncrementTag = true;
        // [SerializeField] private string _containerImageTag;
        #endregion // Serialized fields for Editor persistence
        
        
        #region Interactable Elements
        // Interactable elements >> Header
        private TextField _apiTokenInput;
        
        // Interactable elements >> Application Info
        private TextField _appNameInput;
        private TextField _appIconPathInput; // New in v2
        private Button _appCreateBtn; // New in v2
        
        // Interactable elements >> Container Registry
        private Toggle _containerUseCustomRegistryToggle; // New in v2
        private TextField _containerRegistryUrlInput;
        private TextField _containerImageRepositoryInput;
        private Button _containerBuildAndPushServerBtn;
        
        // Interactable elements >> Deployments
        private Button _createNewDeploymentBtn;
        private Button _serverActionBtn;

        // Interactable elements >> Footer
        private Button _documentationBtn;
        private Button _addMoreGameServersBtn;

        // Interactable elements >> Unused in v2 UI
        // private Button _connectionBtn;
        // private TextField _containerImageTagInput;
        // private Toggle _autoIncrementTagInput;
        // private EnumField _apiEnvironmentSelect;
        // private TextField _appVersionNameInput;
        #endregion // Interactable Elements

        
        // Readonly elements
        private Label _connectionStatusLabel;
        private VisualElement _serverDataContainer;

        [MenuItem("Edgegap/Server Management %#e")]
        public static void ShowEdgegapToolWindow()
        {
            EdgegapWindow window = GetWindow<EdgegapWindow>();
            window.titleContent = new GUIContent("Edgegap Server Management");
        }

        protected void OnEnable()
        {
            // Set root VisualElement and style
            _visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Edgegap/Editor/EdgegapWindow.uxml");
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Edgegap/Editor/EdgegapWindow.uss");
            rootVisualElement.styleSheets.Add(styleSheet);

            LoadToolData();

            if (string.IsNullOrWhiteSpace(_userExternalIp))
            {
                _userExternalIp = GetExternalIpAddress();
            }
        }

        protected void Update()
        {
            if (!_shouldUpdateServerStatus)
                return;
            
            _shouldUpdateServerStatus = false;
            UpdateServerStatus();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            _visualTree.CloneTree(rootVisualElement);

            InitUIElements();
            SyncFormWithObject();

            bool hasActiveDeployment = !string.IsNullOrEmpty(_deploymentRequestId);

            if (hasActiveDeployment)
            {
                RestoreActiveDeployment();
            }
            else
            {
                DisconnectCallback();
            }
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

        protected void OnDisable()
        {
            SyncObjectWithForm();
            SaveToolData();
            EdgegapServerDataManager.DeregisterServerDataContainer(_serverDataContainer);
        }

        /// <summary>
        /// Binds the form inputs to the associated variables and initializes the inputs as required.
        /// Requires the VisualElements to be loaded before this call. Otherwise, the elements cannot be found.
        /// </summary>
        private void InitUIElements()
        {
            _apiTokenInput = rootVisualElement.Q<TextField>("ApiTokenTxt");
            _appNameInput = rootVisualElement.Q<TextField>("appName");

            _containerRegistryUrlInput = rootVisualElement.Q<TextField>("containerRegistry");
            _containerImageRepositoryInput = rootVisualElement.Q<TextField>("containerImageRepo");
            
            _serverActionBtn = rootVisualElement.Q<Button>("serverActionBtn");
            _documentationBtn = rootVisualElement.Q<Button>("documentationBtn");
            _containerBuildAndPushServerBtn = rootVisualElement.Q<Button>("buildAndPushBtn");
            _containerBuildAndPushServerBtn.clickable.clicked += BuildAndPushServer;

            _connectionStatusLabel = rootVisualElement.Q<Label>("connectionStatusLabel");
            _serverDataContainer = rootVisualElement.Q<VisualElement>("serverDataContainer");
            
            #region Unused in v2 UI
            // _containerImageTagInput = rootVisualElement.Q<TextField>("tag");
            // _autoIncrementTagInput = rootVisualElement.Q<Toggle>("autoIncrementTag");
            // _connectionBtn = rootVisualElement.Q<Button>("connectionBtn");
            // _apiEnvironmentSelect = rootVisualElement.Q<EnumField>("environmentSelect");
            // _appVersionNameInput = rootVisualElement.Q<TextField>("appVersionName");
            #endregion // Unused in v2 UI

            // Load initial server data UI element and register for updates.
            VisualElement serverDataElement = EdgegapServerDataManager.GetServerDataVisualTree();
            EdgegapServerDataManager.RegisterServerDataContainer(serverDataElement);
            _serverDataContainer.Clear();
            _serverDataContainer.Add(serverDataElement);

            _documentationBtn.clickable.clicked += OpenDocumentationCallback;

            // // [Unused in v2 UI]
            // // Init the ApiEnvironment dropdown
            // _apiEnvironmentSelect.Init(ApiEnvironment.Console);
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
            {
                SetToolUIState(ToolState.Disconnected);
            }
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

        private async void BuildAndPushServer()
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
                await UpdateAppTagOnEdgegap(tag);

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

        private async void StartServerCallback()
        {
            SetToolUIState(ToolState.ProcessingDeployment); // Prevents being called multiple times.

            const string path = "/v1/deploy";

            const string appVersionName = null; // TODO?
            
            // Setup post data
            DeployPostData deployPostData = new DeployPostData(
                _appNameInputStr, 
                appVersionName, 
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

        private void StartServerStatusCronjob()
        {
            _updateServerStatusCronjob.Elapsed += (sourceObject, elaspedEvent) => 
                _shouldUpdateServerStatus = true;
            
            _updateServerStatusCronjob.AutoReset = true;
            _updateServerStatusCronjob.Start();
        }

        private void StopServerStatusCronjob() => _updateServerStatusCronjob.Stop();

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

                if (serverStatus == ServerStatus.Ready || serverStatus == ServerStatus.Error)
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

        private void RestoreActiveDeployment()
        {
            ConnectCallback();

            _shouldUpdateServerStatus = true;
            StartServerStatusCronjob();
        }

        private void SyncObjectWithForm()
        {
            _apiTokenInputStr = _apiTokenInput.value;
            _appNameInputStr = _appNameInput.value;

            _containerRegistryUrlInputStr = _containerRegistryUrlInput.value;
            _containerImageRepositoryInputStr = _containerImageRepositoryInput.value;

            // Unused in v2 ui >>
            // _apiEnvironment = (ApiEnvironment)_apiEnvironmentSelect.value;
            // _appVersionName = _appVersionNameInput.value;
            // _autoIncrementTag = _autoIncrementTagInput.value;
            // _containerImageTag = _containerImageTagInput.value;
        }

        private void SyncFormWithObject()
        {
            _apiTokenInput.value = _apiTokenInputStr;
            _appNameInput.value = _appNameInputStr;

            _containerRegistryUrlInput.value = _containerRegistryUrlInputStr;
            _containerImageRepositoryInput.value = _containerImageRepositoryInputStr;
            
            // // Unused in v2 ui >>
            // _apiEnvironmentSelect.value = _apiEnvironment;
            // _appVersionNameInput.value = _appVersionName;
            // _containerImageTagInput.value = _containerImageTag;
            // _autoIncrementTagInput.value = _autoIncrementTag;
        }

        private void SetToolUIState(ToolState toolState)
        {
            SetConnectionInfoUI(toolState);
            SetConnectionButtonUI(toolState);
            SetServerActionUI(toolState);
            SetDockerRepoInfoUI(toolState);
        }

        private void SetDockerRepoInfoUI(ToolState toolState)
        {
            bool connected = toolState.CanStartDeployment();
            _containerRegistryUrlInput.SetEnabled(connected);
            _autoIncrementTagInput.SetEnabled(connected);
            _containerImageRepositoryInput.SetEnabled(connected);
            _containerImageTagInput.SetEnabled(connected);

        }

        private void SetConnectionInfoUI(ToolState toolState)
        {
            bool canEditConnectionInfo = toolState.CanEditConnectionInfo();

            _apiTokenInput.SetEnabled(canEditConnectionInfo);
            _apiEnvironmentSelect.SetEnabled(canEditConnectionInfo);
            _appNameInput.SetEnabled(canEditConnectionInfo);
            _appVersionNameInput.SetEnabled(canEditConnectionInfo);
       
        }

        [Obsolete("Unused in v2 UI")]
        private void SetConnectionButtonUI(ToolState toolState)
        {
            bool canConnect = toolState.CanConnect();
            bool canDisconnect = toolState.CanDisconnect();

            _connectionBtn.SetEnabled(canConnect || canDisconnect);

            // A bit dirty, but ensures the callback is not bound multiple times on the button.
            _connectionBtn.clickable.clicked -= ConnectCallback;
            _connectionBtn.clickable.clicked -= DisconnectCallback;

            if (canConnect || toolState == ToolState.Connecting)
            {
                _connectionBtn.text = "Connect";
                _connectionStatusLabel.text = "Awaiting connection";
                _connectionStatusLabel.RemoveFromClassList("text--success");
                _connectionBtn.clickable.clicked += ConnectCallback;
            }
            else
            {
                _connectionBtn.text = "Disconnect";
                _connectionStatusLabel.text = "Connected";
                _connectionStatusLabel.AddToClassList("text--success");
                _connectionBtn.clickable.clicked += DisconnectCallback;
            }
        }

        private void SetServerActionUI(ToolState toolState)
        {
            bool canStartDeployment = toolState.CanStartDeployment();
            bool canStopDeployment = toolState.CanStopDeployment();

            // A bit dirty, but ensures the callback is not bound multiple times on the button.
            _serverActionBtn.clickable.clicked -= StartServerCallback;
            _serverActionBtn.clickable.clicked -= StopServerCallback;

            _serverActionBtn.SetEnabled(canStartDeployment || canStopDeployment);

            _containerBuildAndPushServerBtn.SetEnabled(canStartDeployment);

            if (canStopDeployment)
            {
                _serverActionBtn.text = "Stop Server";
                _serverActionBtn.clickable.clicked += StopServerCallback;
            }
            else
            {
                _serverActionBtn.text = "Start Server";
                _serverActionBtn.clickable.clicked += StartServerCallback;
            }
        }

        /// <summary>
        /// Save the tool's serializable data to the EditorPrefs to allow persistence across restarts.
        /// Any field with [SerializeField] will be saved.
        /// </summary>
        private void SaveToolData()
        {
            string data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(EditorDataSerializationName, data);
        }

        /// <summary>
        /// Load the tool's serializable data from the EditorPrefs to the object, restoring the tool's state.
        /// </summary>
        private void LoadToolData()
        {
            string data = EditorPrefs.GetString(
                EditorDataSerializationName, 
                JsonUtility.ToJson(this, false));
            
            JsonUtility.FromJsonOverwrite(data, this);
        }
    }
}