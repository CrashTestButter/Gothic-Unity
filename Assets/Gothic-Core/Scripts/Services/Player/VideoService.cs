using System.Collections.Generic;
using System.IO;
using System.Linq;
using Gothic.Core.Logging;
using Gothic.Core.Services.Context;
using Gothic.Core.Extensions;
using Reflex.Attributes;
using Logger = Gothic.Core.Logging.Logger;

namespace Gothic.Core.Services.Player
{
    public class VideoService
    {
        public List<string> VideoFileNamesMp4 = new();
        public List<string> VideoFilePathsMp4 = new();
        // Original Bink videos - played directly by BinkPlayer (no conversion needed).
        public List<string> VideoFilePathsBik = new();

        
        [Inject] private readonly ContextGameVersionService _contextGameVersionService;


        public void InitVideos()
        {
            var videoFileFolder = $"{_contextGameVersionService.RootPath}/_work/DATA/video/";

            if (!Directory.Exists(videoFileFolder))
            {
                Logger.LogError($"Video folder >{videoFileFolder}< not found!", LogCat.Loading);
                return;
            }

            VideoFilePathsMp4 = Directory.EnumerateFiles(videoFileFolder, "*.mp4").ToList();
            VideoFileNamesMp4 = VideoFilePathsMp4.Select(Path.GetFileName).ToList();
            VideoFilePathsBik = Directory.EnumerateFiles(videoFileFolder)
                .Where(i => Path.GetExtension(i).EqualsIgnoreCase(".bik")).ToList();

            if (VideoFilePathsMp4.IsEmpty())
            {
                Logger.LogWarning($"No MP4 videos found in the video folder at >{videoFileFolder}<.", LogCat.Loading);
            }
        }

        /// <summary>
        /// Path of the video a script asks for (name with or without extension), null if missing.
        /// An MP4 (converted by the player) wins, otherwise the original Bink file is decoded by BinkPlayer.
        /// </summary>
        public string FindVideo(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            return VideoFilePathsMp4
                       .FirstOrDefault(i => Path.GetFileNameWithoutExtension(i).EqualsIgnoreCase(name)) ??
                   VideoFilePathsBik
                       .FirstOrDefault(i => Path.GetFileNameWithoutExtension(i).EqualsIgnoreCase(name));
        }
    }
}
