using BattleHorizons.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BattleHorizons.UI
{
    /// <summary>
    /// Runtime-built menu with the title, quality selector and scene entry points.
    ///
    /// Built from code for the same reason as the touch controls: it is a handful
    /// of labels and buttons, and a uGUI prefab would need its own .meta churn
    /// without making anything easier to review.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private void Start()
        {
            BuildUi();
            GameLog.Info("menu", "main menu ready");
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("MainMenuCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var background = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(canvasObject.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.13f, 0.16f, 1f);

            CreateLabel(canvasObject.transform, "Title", "BATTLE HORIZONS: LAST SURVIVE",
                new Vector2(0.5f, 0.80f), 64, new Color(1f, 0.85f, 0.35f));
            CreateLabel(canvasObject.transform, "Subtitle", "Siran Island - Unity 6 URP",
                new Vector2(0.5f, 0.73f), 30, new Color(0.75f, 0.80f, 0.85f));

            CreateButton(canvasObject.transform, "EnterSiranIsland", "ENTER SIRAN ISLAND",
                new Vector2(0.5f, 0.55f), () => LoadScene(GameBootstrap.SceneNames.SiranIsland));
            CreateButton(canvasObject.transform, "PlayerTest", "PLAYER CONTROLLER TEST",
                new Vector2(0.5f, 0.44f), () => LoadScene(GameBootstrap.SceneNames.PlayerTest));
            CreateButton(canvasObject.transform, "ToggleQuality", QualityButtonLabel(),
                new Vector2(0.5f, 0.33f), ToggleQuality);

            CreateLabel(canvasObject.transform, "Quality",
                string.Format("Active profile: {0}   (device detect: {1})",
                    QualityProfiles.Active, QualityProfiles.DetectFromDevice()),
                new Vector2(0.5f, 0.24f), 22, new Color(0.65f, 0.70f, 0.75f));
        }

        private static string QualityButtonLabel() =>
            QualityProfiles.Active == QualityProfile.High ? "QUALITY: HIGH (tap to switch)" : "QUALITY: LOW (tap to switch)";

        private void ToggleQuality()
        {
            var next = QualityProfiles.Active == QualityProfile.High ? QualityProfile.Low : QualityProfile.High;
            QualityProfiles.SetOverride(QualityProfiles.QualityIndex(next));
            QualityProfiles.Apply(next);
            GameLog.Info("menu", "quality switched to " + next);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private static void LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                GameLog.Error("menu", "scene '" + sceneName +
                    "' is not in EditorBuildSettings — run Battle Horizons > Project Setup > Configure");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private static void CreateLabel(Transform parent, string name, string text, Vector2 anchor,
            int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1400f, 90f);
            var label = go.GetComponent<UnityEngine.UI.Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.raycastTarget = false;
        }

        private static void CreateButton(Transform parent, string name, string text, Vector2 anchor,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(640f, 84f);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.18f, 0.42f, 0.50f, 0.95f);
            var button = go.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            labelObject.transform.SetParent(go.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<UnityEngine.UI.Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 32;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
        }
    }
}
