using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastWorld
{
    /// <summary>
    /// Builds the in-game HUD at runtime: stamina wheel beside the character, clock,
    /// movement state and a controls reminder (H to toggle).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public PlayerController player;
        public DayNightCycle dayNight;

        Camera cam;
        CanvasGroup staminaGroup;
        RectTransform staminaRect;
        Image staminaFill;
        Text clockText, stateText, helpText;

        static readonly Color StaminaColor = new(0.62f, 0.95f, 0.45f);
        static readonly Color StaminaLowColor = new(1f, 0.38f, 0.28f);

        const string HelpLines =
            "ZQSD : se déplacer     Shift / Clic droit : sprint     Ctrl : marcher\n" +
            "Espace : sauter  (en l'air : planer)     Contre une paroi : escalader     X : lâcher prise\n" +
            "Alt : libérer la souris     Molette : zoom     T : accélérer le temps     H : masquer l'aide";

        void Start()
        {
            if (!player) player = FindAnyObjectByType<PlayerController>();
            if (!dayNight) dayNight = FindAnyObjectByType<DayNightCycle>();
            cam = Camera.main;
            BuildUI();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) helpText.enabled = !helpText.enabled;
            if (dayNight) clockText.text = dayNight.TimeString;
            if (!player || !cam) return;

            stateText.text = StateLabel(player);

            float pct = player.Stamina / player.MaxStamina;
            staminaFill.fillAmount = pct;
            staminaFill.color = pct < 0.25f ? StaminaLowColor : StaminaColor;

            Vector3 world = player.transform.position + Vector3.up * 1.2f + cam.transform.right * 0.75f;
            Vector3 screen = cam.WorldToScreenPoint(world);
            bool show = pct < 0.995f && screen.z > 0f;
            staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, show ? 1f : 0f, Time.deltaTime * 4f);
            staminaRect.position = new Vector3(screen.x, screen.y, 0f);
        }

        static string StateLabel(PlayerController p) => p.State switch
        {
            MoveState.Grounded => p.IsSprinting ? "Sprint" : "",
            MoveState.Airborne => "",
            MoveState.Gliding => "Vol plané",
            MoveState.Climbing => "Escalade",
            MoveState.Swimming => "Nage",
            _ => "",
        };

        void BuildUI()
        {
            var canvasGO = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform root = canvasGO.transform;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite ring = CreateRingSprite(128, 0.72f);

            var staminaGO = new GameObject("Stamina", typeof(RectTransform), typeof(CanvasGroup));
            staminaGO.transform.SetParent(root, false);
            staminaRect = (RectTransform)staminaGO.transform;
            staminaRect.sizeDelta = new Vector2(64f, 64f);
            staminaGroup = staminaGO.GetComponent<CanvasGroup>();
            staminaGroup.alpha = 0f;
            staminaGroup.blocksRaycasts = false;

            var bg = MakeImage(staminaRect, "Fond", ring);
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            staminaFill = MakeImage(staminaRect, "Jauge", ring);
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Radial360;
            staminaFill.fillOrigin = (int)Image.Origin360.Top;
            staminaFill.fillClockwise = false;

            clockText = MakeText(root, "Horloge", font, 30, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(-32f, -24f), new Vector2(300f, 40f));
            stateText = MakeText(root, "Etat", font, 22, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(-32f, -64f), new Vector2(300f, 30f));
            helpText = MakeText(root, "Aide", font, 18, TextAnchor.LowerLeft,
                new Vector2(0f, 0f), new Vector2(28f, 22f), new Vector2(1100f, 90f));
            helpText.text = HelpLines;
        }

        static Image MakeImage(RectTransform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Text MakeText(Transform parent, string name, Font font, int size, TextAnchor alignment,
            Vector2 anchor, Vector2 offset, Vector2 dims)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = dims;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = alignment;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        static Sprite CreateRingSprite(int size, float innerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            float inner = c * innerRatio;
            var center = new Vector2(c, c);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(c - d) * Mathf.Clamp01(d - inner);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
