// Assets/GAME/Scripts/Story/Runtime/Interaction/StoryInteractionAutoUIBootstrapper.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Story.Interaction
{
    public static class StoryInteractionAutoUIBootstrapper
    {
        private const string CanvasName = "StoryInteractionCanvas";

        public static StoryInteractionPromptUI CreatePromptUI()
        {
            Canvas canvas = GetOrCreateStoryCanvas();

            GameObject panel = CreatePanel("StoryInteractionPrompt", canvas.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(420f, 54f));
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, 96f);

            Image background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.72f);
            CanvasGroup group = panel.AddComponent<CanvasGroup>();

            TMP_Text text = CreateText("PromptText", panel.transform, 24, TextAlignmentOptions.Center);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 6f);
            textRect.offsetMax = new Vector2(-16f, -6f);
            text.text = "[E] 사용하기";

            StoryInteractionPromptUI ui = panel.AddComponent<StoryInteractionPromptUI>();
            ui.Configure(group, panel, text);
            return ui;
        }

        public static StoryInteractionConfirmUI CreateConfirmUI()
        {
            Canvas canvas = GetOrCreateStoryCanvas();

            GameObject panel = CreatePanel("StoryInteractionConfirm", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 190f));
            Image background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.82f);
            CanvasGroup group = panel.AddComponent<CanvasGroup>();

            TMP_Text message = CreateText("MessageText", panel.transform, 24, TextAlignmentOptions.Center);
            RectTransform messageRect = message.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0f, 0.42f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(24f, 8f);
            messageRect.offsetMax = new Vector2(-24f, -16f);
            message.text = "사용할까요?";

            Button yes = CreateButton("YesButton", panel.transform, "예", new Vector2(-82f, 32f));
            Button no = CreateButton("NoButton", panel.transform, "아니오", new Vector2(82f, 32f));

            StoryInteractionConfirmUI ui = panel.AddComponent<StoryInteractionConfirmUI>();
            ui.Configure(group, panel, message, yes, no);
            return ui;
        }

        public static Canvas GetOrCreateStoryCanvas()
        {
            GameObject existing = GameObject.Find(CanvasName);
            if (existing != null && existing.TryGetComponent(out Canvas existingCanvas))
            {
                return existingCanvas;
            }

            GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(canvasObject);
            return canvas;
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            return panel;
        }

        private static TMP_Text CreateText(string name, Transform parent, int fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition)
        {
            GameObject buttonObject = CreatePanel(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(132f, 44f));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.88f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.82f, 0.95f, 1f);
            button.colors = colors;

            TMP_Text text = CreateText("Label", buttonObject.transform, 20, TextAlignmentOptions.Center);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.color = Color.black;
            text.text = label;

            return button;
        }
    }
}
