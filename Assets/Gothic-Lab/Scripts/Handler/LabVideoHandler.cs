using System.IO;
using System.Linq;
using Gothic.Core.Adapters.Video;
using Gothic.Core.Services.Player;
using Gothic.Core;
using Gothic.Core.Services;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace Gothic.Lab.Handler
{
    public class LabVideoHandler : AbstractLabHandler
    {
        [SerializeField] private TMP_Dropdown _fileSelector;
        [SerializeField] private MeshRenderer _screen;
        [SerializeField] private AudioSource _audioSource;


        [Inject] private readonly VideoService _videoService;

        private BinkPlayer _binkPlayer;


        public override void Bootstrap()
        {
            _fileSelector.options = _videoService.VideoFilePathsBik
                .Select(i => new TMP_Dropdown.OptionData(Path.GetFileName(i))).ToList();
        }

        public void VideoPlayClick()
        {
            if (_binkPlayer != null)
                Destroy(_binkPlayer);

            _binkPlayer = gameObject.AddComponent<BinkPlayer>();
            if (!_binkPlayer.Play(_videoService.VideoFilePathsBik[_fileSelector.value], _screen, _audioSource, null,
                    out var aspect))
                return;

            var screenWidth = _screen.transform.localScale.x;
            _screen.transform.localScale = new Vector3(screenWidth, screenWidth / aspect, 1f);
        }
    }
}
