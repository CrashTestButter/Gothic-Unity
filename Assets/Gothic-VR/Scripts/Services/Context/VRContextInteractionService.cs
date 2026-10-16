#if GOTHIC_HVR_INSTALLED
using System.Linq;
using Gothic.Core;
using Gothic.Core.Const;
using Gothic.Core.Extensions;
using Gothic.Core.Logging;
using Gothic.Core.Manager;
using Gothic.Core.Models.Caches;
using Gothic.Core.Models.Config;
using Gothic.Core.Services.Caches;
using Gothic.Core.Services.Config;
using Gothic.Core.Services.Context;
using Gothic.VR.Adapters.HVROverrides;
using Gothic.VR.Adapters.Marvin;
using Gothic.VR.Adapters.Player;
using Gothic.VR.Adapters.UI;
using HurricaneVR.Framework.Core.UI;
using HurricaneVRExtensions.Simulator;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Services.Context
{
    public class VRContextInteractionService : IContextInteractionService
    {
        [Inject] private readonly ConfigService _configService;
        [Inject] private readonly ResourceCacheService _resourceCacheService;
        [Inject] private readonly VRWeaponService _vrWeaponService;
        [Inject] private readonly MarvinService _marvinService;

        private const string _contextName = "VR";

        private VRPlayerController _playerController;
        private GameObject _marvinRoot;

        public VRContextInteractionService()
        {
            this.Inject();

            _vrWeaponService.Init();

            GlobalEventDispatcher.LoadingSceneLoaded.AddListener(OnLoadingSceneLoaded);
            GlobalEventDispatcher.GothicInisInitialized.AddListener(() =>
            {
                SetRenderDistance(_configService.Gothic.IniVisualRange);
                _playerController.SetNormalControls();
            });
            GlobalEventDispatcher.PlayerPrefUpdated.AddListener(PlayerPrefsUpdated);
            GlobalEventDispatcher.MarvinModeChanged.AddListener(OnMarvinModeChanged);
            GlobalEventDispatcher.MarvinLogoPoked.AddListener(OnMarvinLogoPoked);
        }

        public string GetContextName()
        {
            return _contextName;
        }

        private void OnLoadingSceneLoaded()
        {
            // Needed for: World -> Open MainMenu -> Hit "Load"/"New Game"
            _playerController.MenuHandler.gameObject.SetActive(false);
        }

        public float GetFrameRate()
        {
            // If we have no VR device attached to our computer, we will get an NPE for activeLoader.
            if (_configService.Dev.EnableVRDeviceSimulator || XRGeneralSettings.Instance.Manager.activeLoader == null)
            {
                return 0;
            }

            var xrDisplay = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>();
            xrDisplay.TryGetDisplayRefreshRate(out var xrRefreshRate);

            return xrRefreshRate;
        }

        public void SetupPlayerController(DeveloperConfig developerConfig)
        {
            // We call this method once at the very beginning of game start. We can safely assume we are in the Player.unity scene.
            var activeScene = SceneManager.GetActiveScene();

            // HVR Player
            _playerController = activeScene.GetComponentInChildren<VRPlayerController>(true)!;
            _playerController.transform.parent.parent.gameObject.SetActive(true);
            _playerController.SetNormalControls(true);

            // XRDeviceSimulator
            var simulatorGO = activeScene.GetComponentInChildren<HVRBodySimulator>(true)!;
            simulatorGO.gameObject.SetActive(developerConfig.EnableVRDeviceSimulator);

            // Marvin Mode
            _marvinRoot = activeScene.GetComponentInChildren<MarvinRootHandler>(true)!.gameObject;
            _marvinRoot.SetActive(_marvinService.IsMarvinMode);

            // PrintScreen() messages in front of the camera
            var printScreenGo = _resourceCacheService.TryGetPrefabObject(PrefabType.UiPrintScreen);
            SceneManager.MoveGameObjectToScene(printScreenGo, activeScene);
            printScreenGo.GetComponent<VRPrintScreen>().Init(_playerController.Camera);
        }

        private void OnMarvinModeChanged(bool isActive)
        {
            _marvinRoot.SetActive(isActive);
        }

        /// <summary>
        /// Vibrates the hand whose laser pointer clicked the logo. Only while the pokes count (menu open in game),
        /// so the main menu doesn't hint at the activation.
        /// </summary>
        private void OnMarvinLogoPoked(PointerEventData eventData)
        {
            if (!_marvinService.IsActivationInputActive)
                return;

            var pointer = HVRInputModule.Instance.Pointers.FirstOrDefault(i => i.PointerEventData == eventData);
            if (pointer == null)
            {
                Logger.LogWarning("[VRContextInteractionService] No VR pointer found for the Marvin logo click.",
                    LogCat.Ui);
                return;
            }

            ReflexProjectInstaller.DIContainer.Resolve<VrHapticsService>()
                .Vibrate(pointer.HandSide, VrHapticsService.VibrationType.Info);
        }
        
        private void PlayerPrefsUpdated(string key, object value)
        {
            if (key == GothicIniConfig.IniKeyVisualRange)
            {
                SetRenderDistance(int.Parse((string)value));
            }
        }

        private void SetRenderDistance(int value)
        {
            // Starting with value=0 (20%) and ending with value=14 (300%)
            _playerController.Camera.GetComponent<Camera>().farClipPlane = GothicIniConfig.IniVisualRangeFactor * (value + 1);
        }

        public GameObject GetCurrentPlayerController()
        {
            return _playerController.gameObject;
        }

        public VRPlayerController GetVRPlayerController()
        {
            return _playerController;
        }

        public VRPlayerInputs GetVRPlayerInputs()
        {
            return _playerController.VrInputs;
        }

        public void LockPlayerInPlace()
        {
            _playerController.SetLockedControls();
        }

        public void UnlockPlayer()
        {
            _playerController.SetUnlockedControls();
        }

        public void SetWalkingControls()
        {
            _playerController.SetWalkingControls();
        }

        public void SetWaterWalkingControls()
        {
            _playerController.SetWaterWalkingControls();
        }

        public void SetSwimmingControls()
        {
            _playerController.SetSwimmingControls();
        }

        public void SetDivingControls()
        {
            _playerController.SetDivingControls();
        }

        public void TeleportPlayerTo(Vector3 position, Quaternion rotation = default)
        {
            _playerController.Teleporter.Teleport(position);
            
            // Changing the rotation inside HVRTeleporter didn't work for y-axis. Therefore, setting it now.
            _playerController.transform.rotation = rotation;
        }

        public void KeepPlayerWithin(Vector3 center, float radius)
        {
            var controller = _playerController.CharacterController;
            if (controller == null)
                return;

            var position = _playerController.transform.position;
            var offset = position - center;
            offset.y = 0f;
            if (offset.magnitude <= radius)
                return;

            var delta = center + offset.normalized * radius - position;
            delta.y = 0f;
            controller.Move(delta);
        }

        public void InitUIInteraction()
        {
            // Find all ui canvases and add to HVR Input module (To activate red laser pointer for clicking/grabbing)
            // WARNING: As it leverages FindObjectsOfType which looks through all opened scenes trees, this can become quite slow. Execute very carefully!
            var allCanvases = Object.FindObjectsOfType<Canvas>(true);
            HVRInputModule.Instance.UICanvases = allCanvases.ToList();
        }

        public void IntroduceChapter(string chapter, string text, string texture, string wav, int time)
        {
            var playerScene = SceneManager.GetSceneByName(Constants.ScenePlayer);
            
            // Check if we already loaded the Chapter change prefab
            var chapterPrefab = playerScene.GetRootGameObjects()
                .FirstOrDefault(i => i.GetComponentInChildren<VRIntroduceChapter>());
            
            if (chapterPrefab == null)
            {
                chapterPrefab = _resourceCacheService.TryGetPrefabObject(PrefabType.StoryIntroduceChapter);
                SceneManager.MoveGameObjectToScene(chapterPrefab, playerScene);
            }
            
            chapterPrefab.GetComponent<VRIntroduceChapter>().DisplayIntroduction(chapter, text, texture, wav, time);
        }

        public void DisableMenus()
        {
            _playerController.VrInputs.IsMenuButtonEnabled = false;
        }

        public void EnableMenus()
        {
            _playerController.VrInputs.IsMenuButtonEnabled = true;
        }
    }
}
#endif
