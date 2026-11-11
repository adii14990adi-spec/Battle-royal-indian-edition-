using UnityEngine;
using UnityEngine.UI;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Loads the committed Battle Horizons circular emblem sprite from Resources
    /// and builds crisp non-raycasting uGUI logo widgets for loading screens,
    /// menus, and in-game HUD branding.
    /// </summary>
    public static class GameLogo
    {
        public const string ResourcePath = "UI/battle_horizons_logo";

        private static Sprite _cachedRuntimeSprite;

        public static Sprite LoadSprite()
        {
            var sprite = Resources.Load<Sprite>(ResourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            if (_cachedRuntimeSprite != null)
            {
                return _cachedRuntimeSprite;
            }

            var texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null)
            {
                return null;
            }

            _cachedRuntimeSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            _cachedRuntimeSprite.name = "BattleHorizonsLogoSprite";
            return _cachedRuntimeSprite;
        }

        public static RectTransform CreateLogoImage(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 size,
            Vector2 anchoredPosition,
            float alpha = 1f)
        {
            var sprite = LoadSprite();
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            return rect;
        }
    }
}
