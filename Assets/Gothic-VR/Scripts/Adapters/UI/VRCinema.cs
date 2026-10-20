#if GOTHIC_HVR_INSTALLED
using System.Collections.Generic;
using Gothic.Core;
using Gothic.Core.Adapters.Video;
using Gothic.Core.Const;
using Gothic.Core.Logging;
using Gothic.VR.Adapters.HVROverrides;
using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.VR.Adapters.UI
{
    /// <summary>
    /// Videos (PlayVideo from Daedalus, the logo videos) in a dark "cinema": the VR camera renders only a screen
    /// (UI layer) on black, the world is muted. Created and fed by VRCinemaService.
    /// Several videos in a row (G1 ending: 3 videos, logos: 2) are queued.
    /// The player is locked meanwhile: no movement, turning, crouching, jumping or menu.
    /// Skip the current video: any trigger or A/X (keyboard Space/Escape).
    /// The original Bink videos (.bik) are decoded by BinkPlayer.
    /// The world is paused (Time.timeScale 0) like in the engine, where the video blocks the game - otherwise dialog
    /// lines queued meanwhile would all play at once after the video.
    /// </summary>
    public class VRCinema : MonoBehaviour
    {
        private const float _screenDistance = 3f;
        private const float _screenWidth = 3.2f;
        private const float _skipInputDelay = 0.5f;

        private readonly Queue<string> _queue = new();

        private Camera _camera;
        private int _savedCullingMask;
        private CameraClearFlags _savedClearFlags;
        private Color _savedBackground;
        private float _savedTimeScale = 1f;

        private VRPlayerController _playerController;
        private bool _savedMovementEnabled;
        private bool _savedRotationEnabled;
        private bool _savedCanCrouch;
        private bool _savedCanJump;
        private bool _savedTeleporterEnabled;
        private bool _savedMenuButtonEnabled;

        private GameObject _screen;
        private AudioSource _audioSource;
        private BinkPlayer _binkPlayer;
        private float _playStartTime;
        private bool _isRunning;


        /// <summary>
        /// Player whose controls are locked during the videos. Optional - without it only the view is changed.
        /// </summary>
        public void SetPlayerController(VRPlayerController playerController)
        {
            _playerController = playerController;
        }

        /// <summary>
        /// Queues a Bink video file. Only the first one starts the cinema, the others follow.
        /// </summary>
        public void Enqueue(string path)
        {
            _queue.Enqueue(path);
            if (!_isRunning)
                PlayNext();
        }

        private void PlayNext()
        {
            if (_queue.Count == 0)
            {
                Close();
                return;
            }

            if (_screen == null && !Open())
            {
                Close();
                return;
            }

            _isRunning = true;
            var path = _queue.Dequeue();
            Logger.Log($"[VRCinema] Playing {path}", LogCat.VR);
            _playStartTime = Time.unscaledTime;
            StopBink();
            PlayBink(path);
        }

        private void PlayBink(string path)
        {
            _binkPlayer = gameObject.AddComponent<BinkPlayer>();
            if (!_binkPlayer.Play(path, _screen.GetComponent<MeshRenderer>(), _audioSource, PlayNext, out var aspect))
            {
                PlayNext();
                return;
            }
            _screen.transform.localScale = new Vector3(_screenWidth, _screenWidth / aspect, 1f);
        }

        private void StopBink()
        {
            if (_binkPlayer == null)
                return;
            _binkPlayer.Stop();
            Destroy(_binkPlayer);
            _binkPlayer = null;
        }

        private bool Open()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Logger.LogWarning("[VRCinema] No main camera - video skipped.", LogCat.VR);
                _queue.Clear();
                return false;
            }

            // Dark room: only the screen (UI layer) on black.
            _savedCullingMask = _camera.cullingMask;
            _savedClearFlags = _camera.clearFlags;
            _savedBackground = _camera.backgroundColor;
            _camera.cullingMask = 1 << Constants.UILayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            AudioListener.pause = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            LockPlayer();

            _screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _screen.name = "_CinemaScreen";
            _screen.layer = Constants.UILayer;
            Destroy(_screen.GetComponent<Collider>());

            // Stays where it is (no head following). PlayVideo often comes before the script teleports the hero - the
            // screen is placed anew then.
            _screen.AddComponent<VRPlaceInFrontOfCamera>().Configure(_camera.transform, _screenDistance);

            // The world is paused via AudioListener.pause - the video's own source ignores it.
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.ignoreListenerPause = true;
            _audioSource.spatialBlend = 0f;
            return true;
        }

        private void Update()
        {
            if (_screen == null || Time.unscaledTime - _playStartTime < _skipInputDelay)
                return;

            if (IsSkipPressed())
            {
                Logger.Log("[VRCinema] Skipped", LogCat.VR);
                StopBink();
                PlayNext();
            }
        }

        private static bool IsSkipPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                return true;

            foreach (var side in new[] { HVRHandSide.Left, HVRHandSide.Right })
            {
                if (HVRController.GetButtonState(side, HVRButtons.Trigger).JustActivated ||
                    HVRController.GetButtonState(side, HVRButtons.Primary).JustActivated)
                    return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            // Destroyed without Close (scene change during a video): the VR camera lives in the persistent Player
            // scene - never leave it dark or the game paused.
            if (_isRunning)
                RestoreWorld();
            ReleaseResources();
        }

        private void Close()
        {
            if (_isRunning)
                RestoreWorld();
            ReleaseResources();

            _isRunning = false;
            Destroy(gameObject);
            Logger.Log("[VRCinema] Closed", LogCat.VR);
            GlobalEventDispatcher.VideoPlaybackFinished.Invoke();
        }

        private void RestoreWorld()
        {
            RestoreCamera();
            UnlockPlayer();
            AudioListener.pause = false;
            Time.timeScale = _savedTimeScale;
        }

        /// <summary>
        /// The camera only shows the screen, but HVR still moves/crouches the player and the menu button would open
        /// the menu. The previous values are kept, as a dialog or seat might have locked the player already.
        /// </summary>
        private void LockPlayer()
        {
            if (_playerController == null)
                return;

            _savedMovementEnabled = _playerController.MovementEnabled;
            _savedRotationEnabled = _playerController.RotationEnabled;
            _savedCanCrouch = _playerController.CanCrouch;
            _savedCanJump = _playerController.CanJump;
            _savedTeleporterEnabled = _playerController.Teleporter.enabled;
            _savedMenuButtonEnabled = _playerController.VrInputs.IsMenuButtonEnabled;

            _playerController.MovementEnabled = false;
            _playerController.RotationEnabled = false;
            _playerController.CanCrouch = false;
            _playerController.CanJump = false;
            _playerController.Teleporter.enabled = false;
            _playerController.VrInputs.IsMenuButtonEnabled = false;
        }

        private void UnlockPlayer()
        {
            if (_playerController == null)
                return;

            _playerController.MovementEnabled = _savedMovementEnabled;
            _playerController.RotationEnabled = _savedRotationEnabled;
            _playerController.CanCrouch = _savedCanCrouch;
            _playerController.CanJump = _savedCanJump;
            _playerController.Teleporter.enabled = _savedTeleporterEnabled;
            _playerController.VrInputs.IsMenuButtonEnabled = _savedMenuButtonEnabled;
            _playerController = null;
        }

        private void RestoreCamera()
        {
            if (_camera == null)
                return;

            _camera.cullingMask = _savedCullingMask;
            _camera.clearFlags = _savedClearFlags;
            _camera.backgroundColor = _savedBackground;
            _camera = null;
        }

        /// <summary>
        /// Safe to call twice (Close, then OnDestroy of the same cinema).
        /// </summary>
        private void ReleaseResources()
        {
            StopBink();

            if (_screen != null)
                Destroy(_screen);
            _screen = null;
        }
    }
}
#endif
