using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Full-screen cinematic Battle Horizons: Last Survive loading screen.
    /// Renders the 16:9 warzone key art with an animated 12-spoke spinner,
    /// glowing amber-gold progress bar, and live percentage readout (0%..100%)
    /// anchored in normalized artwork coordinates so alignment holds on every
    /// mobile aspect ratio (16:9 through 21:9).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadingScreenView : MonoBehaviour
    {
        public const string BackgroundResourcePath = "UI/battle_horizons_loading_bg";
        public const string BarResourcePath = "UI/battle_horizons_loading_bar";
        public const string SpinnerResourcePath = "UI/battle_horizons_loading_spinner";

        private const float BarStartU = 0.43848f;
        private const float BarEndU = 0.67969f;
        private const float BarMinV = 0.1375f;
        private const float BarMaxV = 0.1845f;

        private static Sprite _cachedBgSprite;
        private static Sprite _cachedBarSprite;
        private static Sprite _cachedSpinnerSprite;

        [SerializeField] private RectTransform spinnerRect;
        [SerializeField] private RectTransform barFillRect;
        [SerializeField] private Text percentLabel;

        private float _progress;
        private bool _built;

        public float Progress => _progress;
        public string PercentString => percentLabel != null ? percentLabel.text : string.Empty;

        public static LoadingScreenView AttachOrBuild(Canvas existingCanvas = null)
        {
            if (existingCanvas != null)
            {
                var existing = existingCanvas.GetComponent<LoadingScreenView>();
                if (existing != null)
                {
                    existing.EnsureBuilt();
                    return existing;
                }

                var view = existingCanvas.gameObject.AddComponent<LoadingScreenView>();
                view.BuildIntoCanvas(existingCanvas);
                return view;
            }

            var canvasObject = new GameObject(
                "BattleHorizonsLoadingCanvas",
                typeof(Canvas),
                typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var created = canvasObject.AddComponent<LoadingScreenView>();
            created.BuildIntoCanvas(canvas);
            return created;
        }

        public static void LoadSceneAsyncWithScreen(MonoBehaviour runner, string sceneName, float minSeconds = 1.4f)
        {
            if (runner == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            runner.StartCoroutine(RunSceneTransition(sceneName, minSeconds));
        }

        private static IEnumerator RunSceneTransition(string sceneName, float minSeconds)
        {
            var screen = AttachOrBuild();
            screen.SetProgress(0f);

            var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                yield break;
            }

            load.allowSceneActivation = false;
            var duration = Application.isBatchMode ? 0.25f : Mathf.Max(0.5f, minSeconds);
            var elapsed = 0f;

            while (load.progress < 0.9f || elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var timeFrac = Mathf.Clamp01(elapsed / duration);
                var asyncFrac = Mathf.Clamp01(load.progress / 0.9f);
                screen.SetProgress(Mathf.Min(timeFrac, Mathf.Max(asyncFrac, timeFrac * 0.95f)));
                yield return null;
            }

            screen.SetProgress(1f);
            yield return null;
            load.allowSceneActivation = true;
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Update()
        {
            if (spinnerRect != null)
            {
                spinnerRect.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
            }
        }

        public void EnsureBuilt()
        {
            if (_built && barFillRect != null && percentLabel != null)
            {
                return;
            }

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            BuildIntoCanvas(canvas);
        }

        public void BuildIntoCanvas(Canvas canvas)
        {
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 500);

            var existingRoot = canvas.transform.Find("LoadingScreenRoot");
            if (existingRoot != null)
            {
                spinnerRect = existingRoot.Find("ArtworkFrame/LoadingSpinner") as RectTransform;
                barFillRect = existingRoot.Find("ArtworkFrame/LoadingBarFill") as RectTransform;
                var pctTransform = existingRoot.Find("ArtworkFrame/LoadingPercent");
                if (pctTransform != null)
                {
                    percentLabel = pctTransform.GetComponent<Text>();
                }

                if (spinnerRect != null && barFillRect != null && percentLabel != null)
                {
                    _built = true;
                    SetProgress(_progress);
                    return;
                }
            }

            var bgSprite = LoadOrCreateSprite(BackgroundResourcePath, ref _cachedBgSprite);
            var barSprite = LoadOrCreateSprite(BarResourcePath, ref _cachedBarSprite);
            var spinnerSprite = LoadOrCreateSprite(SpinnerResourcePath, ref _cachedSpinnerSprite);

            var root = new GameObject("LoadingScreenRoot", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)root.transform;
            StretchFull(rootRect);
            var solidBg = root.GetComponent<Image>();
            solidBg.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            solidBg.raycastTarget = true;

            // Full-bleed darkened backdrop so ultra-wide (20:9) screens have atmospheric
            // warzone continuity on the outer horizontal margins without cropping the 16:9 HUD.
            if (bgSprite != null)
            {
                var bleed = new GameObject("AtmosphericBackdrop", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
                bleed.transform.SetParent(root.transform, false);
                StretchFull((RectTransform)bleed.transform);
                var bleedImage = bleed.GetComponent<Image>();
                bleedImage.sprite = bgSprite;
                bleedImage.color = new Color(0.28f, 0.29f, 0.32f, 1f);
                bleedImage.raycastTarget = false;
                var bleedFitter = bleed.GetComponent<AspectRatioFitter>();
                bleedFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                bleedFitter.aspectRatio = 16f / 9f;
            }

            // Exact 16:9 artwork frame fitted inside the screen so top title and bottom
            // loading bar / survival tip / version status never clip on any aspect ratio.
            var frame = new GameObject("ArtworkFrame", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            frame.transform.SetParent(root.transform, false);
            StretchFull((RectTransform)frame.transform);
            var frameImage = frame.GetComponent<Image>();
            if (bgSprite != null)
            {
                frameImage.sprite = bgSprite;
                frameImage.color = Color.white;
            }
            else
            {
                frameImage.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            }
            frameImage.raycastTarget = false;

            var frameFitter = frame.GetComponent<AspectRatioFitter>();
            frameFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            frameFitter.aspectRatio = 16f / 9f;

            // 1. Rotating 12-spoke tactical spinner
            var spinnerGo = new GameObject("LoadingSpinner", typeof(RectTransform), typeof(Image));
            spinnerGo.transform.SetParent(frame.transform, false);
            spinnerRect = (RectTransform)spinnerGo.transform;
            spinnerRect.anchorMin = new Vector2(0.26015f, 0.1378f);
            spinnerRect.anchorMax = new Vector2(0.29015f, 0.1912f);
            spinnerRect.offsetMin = Vector2.zero;
            spinnerRect.offsetMax = Vector2.zero;
            var spinnerImage = spinnerGo.GetComponent<Image>();
            spinnerImage.sprite = spinnerSprite;
            spinnerImage.preserveAspect = true;
            spinnerImage.color = new Color(0.92f, 0.94f, 0.97f, 0.95f);
            spinnerImage.raycastTarget = false;

            // 2. Glowing amber-to-white-gold progress bar fill
            var barGo = new GameObject("LoadingBarFill", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(frame.transform, false);
            barFillRect = (RectTransform)barGo.transform;
            barFillRect.anchorMin = new Vector2(BarStartU, BarMinV);
            barFillRect.anchorMax = new Vector2(BarStartU + 0.004f, BarMaxV);
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;
            var barImage = barGo.GetComponent<Image>();
            if (barSprite != null)
            {
                barImage.sprite = barSprite;
            }
            else
            {
                barImage.color = new Color(1.0f, 0.68f, 0.22f, 1f);
            }
            barImage.raycastTarget = false;

            // 3. Live percentage readout (0% .. 100%)
            var pctGo = new GameObject("LoadingPercent", typeof(RectTransform), typeof(Text));
            pctGo.transform.SetParent(frame.transform, false);
            var pctRect = (RectTransform)pctGo.transform;
            pctRect.anchorMin = new Vector2(0.688f, 0.130f);
            pctRect.anchorMax = new Vector2(0.760f, 0.196f);
            pctRect.offsetMin = Vector2.zero;
            pctRect.offsetMax = Vector2.zero;
            percentLabel = pctGo.GetComponent<Text>();
            percentLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            percentLabel.fontStyle = FontStyle.Bold;
            percentLabel.alignment = TextAnchor.MiddleCenter;
            percentLabel.resizeTextForBestFit = true;
            percentLabel.resizeTextMinSize = 14;
            percentLabel.resizeTextMaxSize = 44;
            percentLabel.color = new Color(0.95f, 0.96f, 0.98f, 1f);
            percentLabel.raycastTarget = false;

            _built = true;
            SetProgress(0f);
        }

        public void SetProgress(float normalizedProgress)
        {
            _progress = Mathf.Clamp01(normalizedProgress);

            if (barFillRect != null)
            {
                var visibleProgress = _progress <= 0.001f ? 0.0f : Mathf.Max(0.025f, _progress);
                var rightU = Mathf.Lerp(BarStartU, BarEndU, visibleProgress);
                barFillRect.anchorMin = new Vector2(BarStartU, BarMinV);
                barFillRect.anchorMax = new Vector2(rightU, BarMaxV);
                barFillRect.gameObject.SetActive(_progress > 0.001f);
            }

            if (percentLabel != null)
            {
                var pct = Mathf.RoundToInt(_progress * 100f);
                percentLabel.text = pct + "%";
            }
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite LoadOrCreateSprite(string resourcePath, ref Sprite cache)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                return sprite;
            }

            if (cache != null)
            {
                return cache;
            }

            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                return null;
            }

            cache = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            cache.name = texture.name + "_RuntimeSprite";
            return cache;
        }
    }
}
