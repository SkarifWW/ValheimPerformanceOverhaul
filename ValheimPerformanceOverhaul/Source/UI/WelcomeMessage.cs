using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimPerformanceOverhaul.UI
{
    public static class WelcomeMessage
    {
        private static bool _shown = false;
        private static readonly string PREFS_KEY = "VPO_HideWelcome_v" + Plugin.PluginVersion;

        [HarmonyPatch(typeof(FejdStartup), "Start")]
        [HarmonyPostfix]
        public static void ShowWelcomePanel()
        {
            if (_shown) return;
            if (PlayerPrefs.GetInt(PREFS_KEY, 0) == 1) return;

            _shown = true;

            var canvasGO = new GameObject("WelcomeCanvas_VPO");
            canvasGO.layer = 5;

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(canvasGO);

            var bg = MakeRect("Background", canvasGO.transform, new Color(0.05f, 0.05f, 0.05f, 0.96f));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(550, 480);
            bgRt.anchoredPosition = Vector2.zero;

            var header = MakeRect("Header", bg.transform, new Color(0.1f, 0.1f, 0.1f, 1f));
            Stretch(header, new Vector2(0, 0.86f), Vector2.one);
            MakeText("Title", header.transform, "ValheimPerformanceOverhaul",
                22, new Color(1f, 0.85f, 0.4f), FontStyle.Bold);

            var bodyTxt = MakeText("Body", bg.transform,
                "Hi! Thank you for using my mod.\n" +
                "The mod is in beta, so I'd love to hear any feedback:\n" +
                "bugs, suggestions, logs - anything.\n\n" +
                "Привет! Спасибо за использование моего мода.\n" +
                "Мод находится в бете, поэтому я буду рад любым отзывам:\n" +
                "багам, предложениям, логам - чему угодно.\n\n" +
                "My Discord account / Мой аккаунт Discord:",
                15, Color.white);
            bodyTxt.lineSpacing = 1.2f;
            bodyTxt.alignment = TextAnchor.UpperCenter;
            SetAnchors(bodyTxt.gameObject, new Vector2(0, 0.35f), new Vector2(1, 0.86f), new Vector2(20, 0), new Vector2(-20, -15));

            var linkGO = MakeRect("Link", bg.transform, Color.clear);
            SetAnchors(linkGO, new Vector2(0, 0.25f), new Vector2(1, 0.35f), new Vector2(20, 0), new Vector2(-20, 0));
            MakeText("LinkText", linkGO.transform,
                "https://discord.com/users/1084209564653727804 (skarif_q)", 15, new Color(0.35f, 0.7f, 1f));

            var ulRt = MakeRect("Underline", linkGO.transform, new Color(0.35f, 0.7f, 1f)).GetComponent<RectTransform>();
            ulRt.anchorMin = new Vector2(0.05f, 0); ulRt.anchorMax = new Vector2(0.95f, 0);
            ulRt.sizeDelta = new Vector2(0, 1.5f); ulRt.anchoredPosition = new Vector2(0, 3f);

            MakeButton(linkGO, linkGO.GetComponent<Image>(), new Color(0.35f, 0.7f, 1f, 0.15f), new Color(0.35f, 0.7f, 1f, 0.3f),
                () => Application.OpenURL("https://discord.com/users/1084209564653727804"));

            MakeSeparator("Sep1", bg.transform, 0.22f);
            MakeSeparator("Sep2", bg.transform, 0.14f);

            bool dontShow = false;
            var labelTxt = MakeText("Label", bg.transform, "Don't show anymore / Больше не показывать", 13, new Color(0.75f, 0.75f, 0.75f));
            SetPivotAnchor(labelTxt.gameObject, new Vector2(0.5f, 0f), new Vector2(280, 30), new Vector2(20f, 85f));

            var boxGO = MakeRect("Box", bg.transform, new Color(0.25f, 0.25f, 0.25f, 1f));
            SetPivotAnchor(boxGO, new Vector2(0.5f, 0f), new Vector2(18, 18), new Vector2(-135f, 85f));

            var tickGO = MakeText("Tick", boxGO.transform, "v", 13, new Color(0.35f, 0.7f, 1f), FontStyle.Bold).gameObject;
            tickGO.SetActive(false);

            var checkHit = MakeRect("CheckHit", bg.transform, Color.clear);
            SetPivotAnchor(checkHit, new Vector2(0.5f, 0f), new Vector2(320, 30), new Vector2(0f, 85f));
            var boxImg = boxGO.GetComponent<Image>();
            MakeButton(checkHit, checkHit.GetComponent<Image>(), new Color(1, 1, 1, 0.05f), new Color(1, 1, 1, 0.1f), () =>
            {
                dontShow = !dontShow;
                tickGO.SetActive(dontShow);
                boxImg.color = dontShow ? new Color(0.1f, 0.25f, 0.45f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
            });

            var closeGO = MakeRect("Close", bg.transform, new Color(0.65f, 0.15f, 0.15f, 1f));
            SetPivotAnchor(closeGO, new Vector2(0.5f, 0f), new Vector2(140, 34), new Vector2(0, 20f));
            MakeText("CloseText", closeGO.transform, "Close / Закрыть", 15, Color.white);
            MakeButton(closeGO, closeGO.GetComponent<Image>(), new Color(0.85f, 0.25f, 0.25f, 1f), new Color(0.45f, 0.08f, 0.08f, 1f), () =>
            {
                if (dontShow) PlayerPrefs.SetInt(PREFS_KEY, 1);
                Object.Destroy(canvasGO);
            });
        }

        private static GameObject MakeRect(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static Text MakeText(string name, Transform parent, string text, int size, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = GetFont();
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            Stretch(go, Vector2.zero, Vector2.one);
            return t;
        }

        private static void MakeButton(GameObject go, Image target, Color highlight, Color pressed, UnityEngine.Events.UnityAction action)
        {
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = target;
            var c = btn.colors;
            c.normalColor = target.color;
            c.highlightedColor = highlight;
            c.pressedColor = pressed;
            btn.colors = c;
            btn.onClick.AddListener(action);
        }

        private static void MakeSeparator(string name, Transform parent, float anchorY)
        {
            var go = MakeRect(name, parent, new Color(1f, 1f, 1f, 0.12f));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, anchorY); rt.anchorMax = new Vector2(1, anchorY);
            rt.sizeDelta = new Vector2(0, 1);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void Stretch(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(GameObject go, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        private static void SetPivotAnchor(GameObject go, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        private static Font GetFont()
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (f != null && f.name.Contains("Averia")) return f;
            }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}