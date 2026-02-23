using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ValheimPerformanceOverhaul.UI
{
    public static class WelcomeMessage
    {
        private static bool _shown = false;

        [HarmonyPatch(typeof(FejdStartup), "Start")]
        [HarmonyPostfix]
        public static void ShowWelcomePanel()
        {
            if (_shown) return;
            _shown = true;

            if (PlayerPrefs.GetInt("VPO_HideWelcome", 0) == 1) return;

            // ── Canvas ──────────────────────────────────────────────
            var canvasGO = new GameObject("WelcomeCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Object.DontDestroyOnLoad(canvasGO);

            // ── Фоновая панель: 500x340 ──────────────────────────────
            var bg = MakeGO("Background", canvasGO.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(500, 340);
            bgRt.anchoredPosition = Vector2.zero;

            // ── Заголовок ────────────────────────────────────────────
            var header = MakeGO("Header", bg.transform);
            header.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);
            Stretch(header, new Vector2(0, 0.82f), new Vector2(1, 1f));

            var titleGO = MakeGO("Title", header.transform);
            var titleTxt = titleGO.AddComponent<Text>();
            titleTxt.text = "ValheimPerformanceOverhaul - Beta";
            titleTxt.font = GetFont();
            titleTxt.fontSize = 20;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = new Color(1f, 0.85f, 0.4f);
            titleTxt.alignment = TextAnchor.MiddleCenter;
            Stretch(titleGO, Vector2.zero, Vector2.one);

            // ── Тело сообщения ───────────────────────────────────────
            var bodyGO = MakeGO("Body", bg.transform);
            var bodyTxt = bodyGO.AddComponent<Text>();
            bodyTxt.text =
                "Hi! Thank you for using my mod.\n\n" +
                "The mod is in beta, so I'd love to hear any feedback:\n" +
                "bugs, suggestions, logs - anything.\n\n" +
                "My Steam account:";
            bodyTxt.font = GetFont();
            bodyTxt.fontSize = 15;
            bodyTxt.color = Color.white;
            bodyTxt.alignment = TextAnchor.UpperCenter;
            bodyTxt.lineSpacing = 1.3f;
            var bodyRt = bodyGO.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0, 0.38f);
            bodyRt.anchorMax = new Vector2(1, 0.82f);
            bodyRt.offsetMin = new Vector2(20, 0);
            bodyRt.offsetMax = new Vector2(-20, -8);

            // ── Ссылка ───────────────────────────────────────────────
            var linkGO = MakeGO("Link", bg.transform);
            var linkBg = linkGO.AddComponent<Image>();
            linkBg.color = Color.clear;
            var linkRt = linkGO.GetComponent<RectTransform>();
            linkRt.anchorMin = new Vector2(0, 0.27f);
            linkRt.anchorMax = new Vector2(1, 0.38f);
            linkRt.offsetMin = new Vector2(20, 0);
            linkRt.offsetMax = new Vector2(-20, 0);

            var linkTxtGO = MakeGO("LinkText", linkGO.transform);
            var linkTxt = linkTxtGO.AddComponent<Text>();
            linkTxt.text = "https://steamcommunity.com/id/Skarif_W/";
            linkTxt.font = GetFont();
            linkTxt.fontSize = 14;
            linkTxt.color = new Color(0.35f, 0.7f, 1f);
            linkTxt.alignment = TextAnchor.MiddleCenter;
            Stretch(linkTxtGO, Vector2.zero, Vector2.one);

            // Подчёркивание ссылки
            var ulGO = MakeGO("Underline", linkGO.transform);
            ulGO.AddComponent<Image>().color = new Color(0.35f, 0.7f, 1f);
            var ulRt = ulGO.GetComponent<RectTransform>();
            ulRt.anchorMin = new Vector2(0.05f, 0f);
            ulRt.anchorMax = new Vector2(0.95f, 0f);
            ulRt.sizeDelta = new Vector2(0, 1.5f);
            ulRt.anchoredPosition = new Vector2(0, 3f);

            var linkBtn = linkGO.AddComponent<Button>();
            linkBtn.targetGraphic = linkBg;
            var lc = linkBtn.colors;
            lc.normalColor = Color.clear;
            lc.highlightedColor = new Color(0.35f, 0.7f, 1f, 0.15f);
            lc.pressedColor = new Color(0.35f, 0.7f, 1f, 0.3f);
            linkBtn.colors = lc;
            linkBtn.onClick.AddListener(() =>
                Application.OpenURL("https://steamcommunity.com/id/Skarif_W/"));

            // ── Разделитель над чекбоксом ────────────────────────────
            var sep1 = MakeGO("Sep1", bg.transform);
            sep1.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            var sep1Rt = sep1.GetComponent<RectTransform>();
            sep1Rt.anchorMin = new Vector2(0, 0.25f);
            sep1Rt.anchorMax = new Vector2(1, 0.25f);
            sep1Rt.sizeDelta = new Vector2(0, 1);

            // ── Чекбокс "Don't show anymore" ─────────────────────────
            bool dontShow = false;

            // Надпись — по центру панели, чуть правее
            var labelGO = MakeGO("Label", bg.transform);
            var labelTxt = labelGO.AddComponent<Text>();
            labelTxt.text = "Don't show anymore";
            labelTxt.font = GetFont();
            labelTxt.fontSize = 13;
            labelTxt.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelTxt.verticalOverflow = VerticalWrapMode.Overflow;
            var labelRt = labelGO.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0f);
            labelRt.sizeDelta = new Vector2(180, 30);
            labelRt.anchoredPosition = new Vector2(10f, 70f);

            // Квадратик — строго слева от надписи
            var boxGO = MakeGO("Box", bg.transform);
            var boxImg = boxGO.AddComponent<Image>();
            boxImg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            var boxRt = boxGO.GetComponent<RectTransform>();
            boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0f);
            boxRt.sizeDelta = new Vector2(18, 18);
            // надпись шириной 180, её левый край = центр - 90 + 10 = -80
            // квадратик ставим на -80 - 9 (половина квадратика) - 4 (зазор) = -93
            boxRt.anchoredPosition = new Vector2(-83f, 70f);

            // Галочка
            var tickGO = MakeGO("Tick", boxGO.transform);
            var tickTxt = tickGO.AddComponent<Text>();
            tickTxt.text = "v";
            tickTxt.font = GetFont();
            tickTxt.fontSize = 13;
            tickTxt.fontStyle = FontStyle.Bold;
            tickTxt.color = new Color(0.35f, 0.7f, 1f);
            tickTxt.alignment = TextAnchor.MiddleCenter;
            Stretch(tickGO, Vector2.zero, Vector2.one);
            tickGO.SetActive(false);

            // Невидимая кнопка поверх обоих элементов
            var checkHitGO = MakeGO("CheckHit", bg.transform);
            var checkHitImg = checkHitGO.AddComponent<Image>();
            checkHitImg.color = Color.clear;
            var checkHitRt = checkHitGO.GetComponent<RectTransform>();
            checkHitRt.anchorMin = checkHitRt.anchorMax = new Vector2(0.5f, 0f);
            checkHitRt.sizeDelta = new Vector2(210, 30);
            checkHitRt.anchoredPosition = new Vector2(-2f, 70f);

            var checkBtn = checkHitGO.AddComponent<Button>();
            checkBtn.targetGraphic = checkHitImg;
            var bcc = checkBtn.colors;
            bcc.normalColor = Color.clear;
            bcc.highlightedColor = new Color(1f, 1f, 1f, 0.05f);
            bcc.pressedColor = new Color(1f, 1f, 1f, 0.1f);
            checkBtn.colors = bcc;
            checkBtn.onClick.AddListener(() =>
            {
                dontShow = !dontShow;
                tickGO.SetActive(dontShow);
                boxImg.color = dontShow
                    ? new Color(0.1f, 0.25f, 0.45f, 1f)
                    : new Color(0.25f, 0.25f, 0.25f, 1f);
            });

            // ── Разделитель над кнопкой Close ────────────────────────
            var sep2 = MakeGO("Sep2", bg.transform);
            sep2.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            var sep2Rt = sep2.GetComponent<RectTransform>();
            sep2Rt.anchorMin = new Vector2(0, 0.16f);
            sep2Rt.anchorMax = new Vector2(1, 0.16f);
            sep2Rt.sizeDelta = new Vector2(0, 1);

            // ── Кнопка Close ─────────────────────────────────────────
            var closeGO = MakeGO("Close", bg.transform);
            var closeImg = closeGO.AddComponent<Image>();
            closeImg.color = new Color(0.65f, 0.15f, 0.15f, 1f);
            var closeRt = closeGO.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.sizeDelta = new Vector2(120, 34);
            closeRt.anchoredPosition = new Vector2(0, 30f);

            var closeTxtGO = MakeGO("CloseText", closeGO.transform);
            var closeTxt = closeTxtGO.AddComponent<Text>();
            closeTxt.text = "Close";
            closeTxt.font = GetFont();
            closeTxt.fontSize = 15;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            Stretch(closeTxtGO, Vector2.zero, Vector2.one);

            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            var cc = closeBtn.colors;
            cc.normalColor = new Color(0.65f, 0.15f, 0.15f, 1f);
            cc.highlightedColor = new Color(0.85f, 0.25f, 0.25f, 1f);
            cc.pressedColor = new Color(0.45f, 0.08f, 0.08f, 1f);
            closeBtn.colors = cc;
            closeBtn.onClick.AddListener(() =>
            {
                if (dontShow)
                    PlayerPrefs.SetInt("VPO_HideWelcome", 1);
                Object.Destroy(canvasGO);
            });
        }

        // ── Вспомогательные методы ────────────────────────────────────

        private static Font GetFont()
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                if (f != null) return f;
            return Font.CreateDynamicFontFromOSFont("Arial", 14);
        }

        private static GameObject MakeGO(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void Stretch(GameObject go, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}