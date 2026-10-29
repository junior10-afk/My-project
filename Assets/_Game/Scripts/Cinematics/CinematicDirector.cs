using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace LastWorld
{
    /// <summary>
    /// Plays the story cinematics. Each cinematic is a child GameObject (named by its id, e.g.
    /// "CIN_02_Resonance") holding a PlayableDirector + Timeline, its Cinemachine shot cameras,
    /// marks and actors (built by the CinematicBuilder editor tool). While one plays, the orbit
    /// camera hands over to a CinemachineBrain, gameplay input and enemy AI are frozen, the HUD is
    /// hidden and letterbox bars, subtitles and fades are drawn on a dedicated overlay.
    /// Hold Escape/Space to skip.
    /// </summary>
    public class CinematicDirector : MonoBehaviour
    {
        public static CinematicDirector Instance { get; private set; }
        public static bool Playing => Instance && Instance.current;

        const float SkipHoldTime = 1f;

        PlayableDirector current;
        Transform currentRoot;
        Action onDone;
        readonly Queue<string> queue = new();
        CinemachineBrain brain;
        ThirdPersonCamera orbit;

        Image fade, barTop, barBottom, skipFill;
        Text subtitle, speaker;
        CanvasGroup skipGroup;
        float bars, skipHold;

        static readonly Color Accent = new(0.95f, 0.82f, 0.5f);

        void Awake() => Instance = this;

        void Start()
        {
            BuildOverlay();
            foreach (var d in GetComponentsInChildren<PlayableDirector>(true))
            {
                d.playOnAwake = false;
                d.gameObject.SetActive(false);
            }
        }

        // ================================================================ Public API

        public static bool Has(string id) => Instance && Instance.transform.Find(id);

        /// <summary>Id of the cinematic playing right now, or null.</summary>
        public static string CurrentId => Instance && Instance.currentRoot ? Instance.currentRoot.name : null;

        /// <summary>Skips the current cinematic (same as holding Escape).</summary>
        public static void SkipCurrent()
        {
            if (Instance) Instance.Skip();
        }

        /// <summary>Plays a cinematic, then calls <paramref name="done"/> (also called right away if it does not exist).</summary>
        public static void Play(string id, Action done = null) => PlaySequence(new[] { id }, done);

        /// <summary>Plays several cinematics back to back.</summary>
        public static void PlaySequence(IEnumerable<string> ids, Action done = null)
        {
            if (!Instance) { done?.Invoke(); return; }
            Instance.queue.Clear();
            foreach (var id in ids) if (Has(id)) Instance.queue.Enqueue(id);
            Instance.onDone = done;
            Instance.Next();
        }

        public static void SetSubtitle(string who, string text)
        {
            if (!Instance || !Instance.subtitle) return;
            Instance.speaker.text = who ?? "";
            Instance.subtitle.text = text ?? "";
        }

        /// <summary>Clears the subtitle only if it still shows <paramref name="text"/> (clips may overlap at boundaries).</summary>
        public static void ClearSubtitle(string text)
        {
            if (Instance && Instance.subtitle && Instance.subtitle.text == text) SetSubtitle(null, null);
        }

        public static void SetFade(Color color)
        {
            if (Instance && Instance.fade) Instance.fade.color = color;
        }

        /// <summary>Executes a timeline action ("verb:argument[:argument]").</summary>
        public static void Fire(string action)
        {
            if (Instance && !string.IsNullOrEmpty(action)) Instance.Execute(action);
        }

        // ================================================================ Playback

        void Next()
        {
            if (queue.Count == 0)
            {
                EndAll();
                return;
            }
            StartCinematic(transform.Find(queue.Dequeue()));
        }

        void StartCinematic(Transform root)
        {
            CloseCurrent();
            root.gameObject.SetActive(true);
            currentRoot = root;
            current = root.GetComponent<PlayableDirector>();

            var cam = Camera.main;
            if (cam)
            {
                orbit = cam.GetComponent<ThirdPersonCamera>();
                if (orbit) orbit.enabled = false;
                brain = cam.GetComponent<CinemachineBrain>();
                if (!brain) brain = cam.gameObject.AddComponent<CinemachineBrain>();
                brain.enabled = true;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                foreach (var output in current.playableAsset.outputs)
                    if (output.sourceObject is CinemachineTrack) current.SetGenericBinding(output.sourceObject, brain);
            }

            if (GameState.Instance) GameState.Instance.cinematic = true;
            SetSubtitle(null, null);
            skipHold = 0f;
            current.stopped += OnStopped;
            current.time = 0;
            current.Play();
        }

        void OnStopped(PlayableDirector d)
        {
            if (d != current) return;
            Next();
        }

        void CloseCurrent()
        {
            if (!current) return;
            current.stopped -= OnStopped;
            if (current.state == PlayState.Playing) current.Stop();
            currentRoot.gameObject.SetActive(false);
            current = null;
            currentRoot = null;
        }

        void EndAll()
        {
            CloseCurrent();
            if (brain) brain.enabled = false;
            if (orbit) orbit.enabled = true;
            var pc = PlayerController.Instance;
            if (pc && pc.visual) pc.visual.gameObject.SetActive(true);
            if (GameState.Instance) GameState.Instance.cinematic = false;
            SetSubtitle(null, null);
            SetFade(Color.clear);
            var done = onDone;
            onDone = null;
            done?.Invoke();
        }

        /// <summary>Jumps to the end, firing the remaining essential actions (actor placement etc.).</summary>
        void Skip()
        {
            if (!current || current.playableAsset is not TimelineAsset tl) return;
            double now = current.time;
            foreach (var track in tl.GetOutputTracks().OfType<CinematicActionTrack>())
                foreach (var clip in track.GetClips())
                    if (clip.start >= now && clip.asset is CinematicActionClip a && a.essential)
                        Execute(a.action);
            current.Stop(); // raises stopped → Next()
        }

        void Update()
        {
            float target = Playing ? 1f : 0f;
            bars = Mathf.MoveTowards(bars, target, Time.unscaledDeltaTime * 2.5f);
            if (barTop)
            {
                float h = Mathf.SmoothStep(0f, 0.11f, bars);
                barTop.rectTransform.anchorMin = new Vector2(0f, 1f - h);
                barBottom.rectTransform.anchorMax = new Vector2(1f, h);
            }

            if (!Playing) { if (skipGroup) skipGroup.alpha = 0f; return; }
            var kb = Keyboard.current;
            bool held = kb != null && (kb.escapeKey.isPressed || kb.spaceKey.isPressed)
                        || Gamepad.current != null && Gamepad.current.startButton.isPressed;
            skipHold = held ? skipHold + Time.unscaledDeltaTime : Mathf.MoveTowards(skipHold, 0f, Time.unscaledDeltaTime * 2f);
            skipGroup.alpha = Mathf.Clamp01(skipHold * 4f);
            skipFill.fillAmount = skipHold / SkipHoldTime;
            if (skipHold >= SkipHoldTime)
            {
                skipHold = 0f;
                Skip();
            }
        }

        // ================================================================ Actions

        Transform Mark(string name)
        {
            if (!currentRoot) return null;
            return currentRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        void Execute(string action)
        {
            var p = action.Split(':');
            string arg = p.Length > 1 ? p[1] : "";
            var m = Mark(arg);
            switch (p[0])
            {
                case "place": // move the player onto a mark (marks sit on the ground, the player pivot is 1 m up)
                    var pc = PlayerController.Instance;
                    if (pc && m)
                    {
                        pc.Teleport(m.position + Vector3.up);
                        pc.transform.rotation = Quaternion.Euler(0f, m.eulerAngles.y, 0f);
                    }
                    break;
                case "show":
                    if (m) m.gameObject.SetActive(true);
                    break;
                case "hide":
                    if (m) m.gameObject.SetActive(false);
                    break;
                case "burst":
                    if (m)
                    {
                        var c = p.Length > 2 && Enum.TryParse(p[2], out Element e) ? Elements.Color(e) : Elements.Color(Element.Anemo);
                        CombatFX.Burst(m.position, c, 4f, 90);
                        CombatFX.Ring(m.position, 10f, c, 0.8f);
                    }
                    break;
                case "player": // "player:hide" / "player:show" toggles the hero's visual
                    var pv = PlayerController.Instance;
                    if (pv && pv.visual) pv.visual.gameObject.SetActive(arg != "hide");
                    break;
                case "sound":
                    AudioManager.Play(arg);
                    break;
                case "banner":
                    GameUI.Banner(arg, Accent);
                    break;
            }
        }

        // ================================================================ Overlay

        void BuildOverlay()
        {
            var canvasGo = new GameObject("Overlay cinématique", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            barTop = MakeImage(root, "Bande haut", Color.black, new Vector2(0f, 1f), Vector2.one);
            barBottom = MakeImage(root, "Bande bas", Color.black, Vector2.zero, new Vector2(1f, 0f));

            speaker = MakeText(root, font, 26, Accent, new Vector2(0.5f, 0.155f));
            speaker.fontStyle = FontStyle.Bold;
            subtitle = MakeText(root, font, 32, Color.white, new Vector2(0.5f, 0.1f));
            subtitle.rectTransform.sizeDelta = new Vector2(1500f, 90f);

            fade = MakeImage(root, "Fondu", Color.clear, Vector2.zero, Vector2.one);
            fade.raycastTarget = false;

            var skip = new GameObject("Passer", typeof(RectTransform), typeof(CanvasGroup));
            skip.transform.SetParent(root, false);
            skipGroup = skip.GetComponent<CanvasGroup>();
            skipGroup.alpha = 0f;
            var srt = (RectTransform)skip.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1f, 0f);
            srt.anchoredPosition = new Vector2(-170f, 60f);
            srt.sizeDelta = new Vector2(300f, 40f);
            skipFill = MakeImage(srt, "Jauge", new Color(1f, 1f, 1f, 0.8f), Vector2.zero, new Vector2(1f, 0.08f));
            skipFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            skipFill.type = Image.Type.Filled;
            skipFill.fillMethod = Image.FillMethod.Horizontal;
            var label = MakeText(srt, font, 20, new Color(1f, 1f, 1f, 0.85f), new Vector2(0.5f, 0.6f));
            label.text = "Maintenir Échap : passer";
            label.rectTransform.sizeDelta = new Vector2(300f, 30f);
        }

        static Image MakeImage(RectTransform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        static Text MakeText(RectTransform parent, Font font, int size, Color color, Vector2 anchor)
        {
            var t = new GameObject("Texte", typeof(RectTransform), typeof(Text), typeof(Shadow)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(1200f, 40f);
            t.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            return t;
        }
    }
}
