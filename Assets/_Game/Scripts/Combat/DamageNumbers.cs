using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastWorld
{
    /// <summary>Floating damage / heal / reaction numbers drawn on a pooled overlay canvas.</summary>
    public class DamageNumbers : MonoBehaviour
    {
        static DamageNumbers instance;

        class Entry
        {
            public Text text;
            public Vector3 world;
            public Vector2 drift;
            public float t, life;
            public bool big;
        }

        readonly List<Entry> active = new();
        readonly Stack<Text> pool = new();
        Font font;
        Transform root;

        void Awake()
        {
            instance = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Damage Canvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root = go.transform;
        }

        public static void Show(DamageInfo info)
        {
            if (!instance) return;
            Color c = Elements.Color(info.element);
            instance.Spawn(Mathf.RoundToInt(info.amount).ToString(), info.point, c, info.crit ? 44 : 32, info.crit);
            if (info.reaction != Reaction.None && !info.fromReaction)
                instance.Spawn(Elements.Name(info.reaction), info.point + Vector3.up * 0.45f, Elements.Color(info.reaction), 30, true);
        }

        public static void ShowText(string s, Vector3 world, Color color, int size = 30)
        {
            if (instance) instance.Spawn(s, world, color, size, false);
        }

        void Spawn(string s, Vector3 world, Color color, int size, bool big)
        {
            var t = pool.Count > 0 ? pool.Pop() : Create();
            t.gameObject.SetActive(true);
            t.text = s;
            t.color = color;
            t.fontSize = size;
            active.Add(new Entry
            {
                text = t,
                world = world,
                drift = new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1f)),
                life = big ? 1.1f : 0.9f,
                big = big,
            });
        }

        Text Create()
        {
            var go = new GameObject("Num", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(root, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.75f);
            o.effectDistance = new Vector2(2f, -2f);
            ((RectTransform)go.transform).sizeDelta = new Vector2(300f, 60f);
            return t;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var e = active[i];
                e.t += Time.unscaledDeltaTime;
                float k = e.t / e.life;
                if (k >= 1f)
                {
                    e.text.gameObject.SetActive(false);
                    pool.Push(e.text);
                    active.RemoveAt(i);
                    continue;
                }
                Vector3 sp = cam.WorldToScreenPoint(e.world + Vector3.up * (k * 0.8f));
                if (sp.z < 0f)
                {
                    e.text.enabled = false;
                    continue;
                }
                e.text.enabled = true;
                e.text.rectTransform.position = (Vector2)sp + e.drift * (k * 50f);
                float pop = e.big ? 1f + Mathf.Max(0f, 0.15f - e.t) * 3f : 1f;
                e.text.rectTransform.localScale = Vector3.one * pop;
                var col = e.text.color;
                col.a = 1f - k * k * k;
                e.text.color = col;
            }
        }
    }
}
