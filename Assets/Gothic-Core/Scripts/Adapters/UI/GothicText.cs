using Gothic.Core.Services.UI;
using TMPro;
using UnityEngine;

namespace Gothic.Core.Adapters.UI
{
    /// <summary>
    /// Texts created at runtime in Gothic's font: an empty TMP font, so every character falls back to the sprite asset
    /// made from the game's font (FontService). Only works after the game booted (FontService created the sprites).
    /// </summary>
    public static class GothicText
    {
        private const string _emptyFontPath = "FontAsset/LiberationSans SDF empty";

        private static TMP_FontAsset _emptyFont;

        /// <param name="isTinted">The font sprites take the text color (e.g. red/green numbers).</param>
        public static void Apply(TMP_Text text, bool isHighlighted = false, bool isTinted = false)
        {
            if (_emptyFont == null)
                _emptyFont = Resources.Load<TMP_FontAsset>(_emptyFontPath);
            if (_emptyFont != null)
                text.font = _emptyFont;

            var fontService = ReflexProjectInstaller.DIContainer.Resolve<FontService>();
            var spriteAsset = isHighlighted ? fontService.HighlightSpriteAsset : fontService.DefaultSpriteAsset;
            if (spriteAsset != null)
                text.spriteAsset = spriteAsset;

            text.tintAllSprites = isTinted;
        }
    }
}
