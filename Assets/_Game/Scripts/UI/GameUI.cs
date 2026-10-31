using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LastWorld
{
    /// <summary>Dialogue entry point used by NPCs (rendered by GameUI).</summary>
    public static class DialogueSystem
    {
        public static bool Active => GameUI.Instance && GameUI.Instance.DialogueOpen;
        /// <summary>Name of the character speaking the current dialogue line, or null.</summary>
        public static string CurrentSpeaker => Active ? GameUI.Instance.CurrentSpeaker : null;
        public static void Show(List<DialogueLine> lines, Action onDone) => GameUI.Instance?.ShowDialogue(lines, onDone);
    }

    /// <summary>
    /// The whole game interface, built at runtime with uGUI: HUD (minimap, quest tracker, party,
    /// HP/shield, skill & burst, stamina, clock, prompts, toasts, banners, objective marker),
    /// dialogue box with choices, and menus (title, pause/settings, map with fast travel,
    /// inventory, characters, quests).
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        [Header("World map")]
        public Texture2D mapTexture;
        public Vector2 worldMin = Vector2.zero;
        public Vector2 worldSize = new(2048f, 2048f);

        public bool DialogueOpen => dialoguePanel && dialoguePanel.activeSelf;
        public string CurrentSpeaker => dialogueSpeaker ? dialogueSpeaker.text : null;

        Font font;
        RectTransform root;
        PlayerController player;
        PartyManager party;
        PlayerCombat combat;
        DayNightCycle cycle;
        Camera cam;

        // HUD
        RectTransform hud;
        RawImage minimap;
        RectTransform miniPlayer, miniQuest;
        Text questTitle, questStep, clockText, stateText, promptText, helpText, bannerText, nameText;
        Image hpFill, shieldFill, skillFill, burstFill, staminaFill;
        Text skillText, burstText;
        Image skillRing, burstRing;
        CanvasGroup staminaGroup, bannerGroup;
        RectTransform staminaRect, questMarker, promptRoot;
        Text questMarkerDist;
        readonly List<(RectTransform root, Text label, Image hp, Image dot)> partyRows = new();
        RectTransform toastRoot;
        readonly List<(Text text, float t)> toasts = new();
        float bannerTime = 99f;

        // Dialogue
        GameObject dialoguePanel;
        Text dialogueSpeaker, dialogueText;
        RectTransform choiceRoot;
        List<DialogueLine> dialogueLines;
        int dialogueIndex;
        float typeT;
        Action dialogueDone;

        // Menus
        GameObject titlePanel, pausePanel, mapPanel, inventoryPanel, charactersPanel, questsPanel;
        RawImage bigMap;
        RectTransform bigMapMarkers;
        Text inventoryDetail, charactersText, questsDetail;
        RectTransform inventoryList, questsList;
        string selectedItem;
        Quest selectedQuest;

        static readonly Color Panel = new(0.05f, 0.07f, 0.1f, 0.82f);
        static readonly Color Accent = new(0.95f, 0.82f, 0.5f);

        void Awake() => Instance = this;

        void Start()
        {
            player = PlayerController.Instance;
            party = PartyManager.Instance;
            combat = PlayerCombat.Instance;
            cycle = FindAnyObjectByType<DayNightCycle>();
            cam = Camera.main;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            Build();
            if (party) party.Changed += RefreshParty;
            Inventory.ItemAdded += (id, n) => { if (id != "wind_orb") Toast($"+{n}  {Inventory.Name(id)}"); };
            RefreshParty();

            if (SaveSystem.SkipTitle) titlePanel.SetActive(false);
            else
            {
                titlePanel.SetActive(true);
                GameState.UIOpen = true;
            }
        }

        // =============================================================== Static API

        public static void Banner(string text, Color color)
        {
            if (!Instance || !Instance.bannerText) return;
            Instance.bannerText.text = text;
            Instance.bannerText.color = color;
            Instance.bannerTime = 0f;
        }

        public static void Toast(string text)
        {
            if (!Instance || !Instance.toastRoot) return;
            var t = Instance.MakeText(Instance.toastRoot, text, 22, TextAnchor.MiddleLeft, Color.white);
            t.rectTransform.sizeDelta = new Vector2(520f, 32f);
            Instance.toasts.Add((t, 0f));
            if (Instance.toasts.Count > 6)
            {
                Destroy(Instance.toasts[0].text.gameObject);
                Instance.toasts.RemoveAt(0);
            }
        }

        // =============================================================== Update

        void Update()
        {
            var kb = Keyboard.current;
            HandleMenuKeys(kb);
            UpdateHUD();
            UpdateDialogue(kb);
            UpdateToasts();
        }

        void HandleMenuKeys(Keyboard kb)
        {
            if (kb == null || titlePanel.activeSelf) return;
            if (DialogueOpen || GameState.Cinematic) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (AnyMenuOpen()) CloseMenus();
                else Open(pausePanel);
            }
            if (kb.mKey.wasPressedThisFrame) Toggle(mapPanel, RefreshMap);
            if (kb.bKey.wasPressedThisFrame) Toggle(inventoryPanel, RefreshInventory);
            if (kb.cKey.wasPressedThisFrame) Toggle(charactersPanel, RefreshCharacters);
            if (kb.jKey.wasPressedThisFrame) Toggle(questsPanel, RefreshQuests);
            if (kb.hKey.wasPressedThisFrame) helpText.enabled = !helpText.enabled;
        }

        bool AnyMenuOpen() => pausePanel.activeSelf || mapPanel.activeSelf || inventoryPanel.activeSelf || charactersPanel.activeSelf || questsPanel.activeSelf;

        void Toggle(GameObject panel, Action refresh)
        {
            if (panel.activeSelf) CloseMenus();
            else
            {
                CloseMenus();
                refresh?.Invoke();
                Open(panel);
            }
        }

        void Open(GameObject panel)
        {
            panel.SetActive(true);
            GameState.UIOpen = true;
            AudioManager.Play("ui");
        }

        void CloseMenus()
        {
            foreach (var p in new[] { pausePanel, mapPanel, inventoryPanel, charactersPanel, questsPanel }) p.SetActive(false);
            GameState.UIOpen = DialogueOpen || titlePanel.activeSelf;
            AudioManager.Play("ui");
        }

        void UpdateHUD()
        {
            if (!player || !party) return;
            if (!cam) cam = Camera.main;
            var m = party.Active;

            hud.gameObject.SetActive(!titlePanel.activeSelf && !GameState.Cinematic);
            if (cycle) clockText.text = cycle.TimeString;
            stateText.text = player.State switch
            {
                MoveState.Gliding => "Vol plané",
                MoveState.Climbing => "Escalade",
                MoveState.Swimming => "Nage",
                _ => player.IsSprinting ? "Sprint" : "",
            };

            nameText.text = $"{m.name}   Niv. {m.level}";
            hpFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(m.hp / m.MaxHP), 1f, 1f);
            shieldFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(party.Shield / m.MaxHP), 1f, 1f);
            shieldFill.color = Elements.Color(party.ShieldElement) * new Color(1f, 1f, 1f, 0.7f);

            Color ec = Elements.Color(m.element);
            float cd = m.SkillCooldownLeft;
            skillFill.fillAmount = cd > 0f ? 1f - cd / m.skillCooldown : 1f;
            skillFill.color = cd > 0f ? ec * 0.5f : ec;
            skillText.text = cd > 0f ? cd.ToString("0.0") : "E";
            burstFill.fillAmount = m.energy / m.burstEnergy;
            burstFill.color = m.BurstReady ? ec : ec * 0.55f;
            burstText.text = m.BurstReady ? "Q" : $"{Mathf.FloorToInt(m.energy / m.burstEnergy * 100f)}%";
            burstRing.color = m.BurstReady ? Color.Lerp(ec, Color.white, Mathf.PingPong(Time.time * 2f, 0.6f)) : new Color(1f, 1f, 1f, 0.25f);

            // Stamina ring beside the character
            float pct = player.Stamina / player.MaxStamina;
            staminaFill.fillAmount = pct;
            staminaFill.color = pct < 0.25f ? new Color(1f, 0.38f, 0.28f) : new Color(0.62f, 0.95f, 0.45f);
            if (cam)
            {
                Vector3 sp = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 1.2f + cam.transform.right * 0.75f);
                staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, pct < 0.995f && sp.z > 0f ? 1f : 0f, Time.unscaledDeltaTime * 4f);
                staminaRect.position = new Vector3(sp.x, sp.y, 0f);
            }

            // Party rows
            for (int i = 0; i < partyRows.Count && i < party.members.Length; i++)
            {
                var mem = party.members[i];
                var row = partyRows[i];
                row.root.gameObject.SetActive(mem.unlocked);
                if (!mem.unlocked) continue;
                row.hp.rectTransform.localScale = new Vector3(Mathf.Clamp01(mem.hp / mem.MaxHP), 1f, 1f);
                row.hp.color = mem.Down ? Color.gray : new Color(0.55f, 0.9f, 0.45f);
                bool active = i == party.ActiveIndex;
                row.root.anchoredPosition = new Vector2(active ? -40f : -24f, row.root.anchoredPosition.y);
                row.label.color = active ? Accent : Color.white;
                row.label.text = $"{i + 1}  {mem.name}" + (mem.BurstReady ? "  ✦" : "");
            }

            // Interaction prompt
            var it = InteractionSystem.Current;
            promptRoot.gameObject.SetActive(it && !GameState.UIOpen);
            if (it) promptText.text = $"[F]  {it.Prompt}";

            // Quest tracker + marker
            var q = QuestManager.Instance ? QuestManager.Instance.Tracked : null;
            questTitle.text = q != null ? q.title : "";
            questStep.text = q != null ? "◆ " + QuestManager.StepProgress(q) : "";
            bool hasMarker = q != null && QuestManager.TryGetMarker(q, out var markerPos);
            questMarker.gameObject.SetActive(hasMarker && !GameState.UIOpen);
            if (hasMarker && cam)
            {
                QuestManager.TryGetMarker(q, out markerPos);
                Vector3 sp = cam.WorldToScreenPoint(markerPos + Vector3.up * 3f);
                bool behind = sp.z < 0f;
                if (behind) sp *= -1f;
                float margin = 60f;
                sp.x = Mathf.Clamp(sp.x, margin, Screen.width - margin);
                sp.y = Mathf.Clamp(behind ? margin : sp.y, margin, Screen.height - margin);
                questMarker.position = new Vector3(sp.x, sp.y, 0f);
                questMarkerDist.text = $"{Vector3.Distance(player.transform.position, markerPos):0} m";
            }

            // Minimap (north-up, 260 m across)
            if (mapTexture)
            {
                Vector2 uv = WorldToMap(player.transform.position);
                float span = 260f / worldSize.x;
                minimap.uvRect = new Rect(uv.x - span * 0.5f, uv.y - span * 0.5f, span, span);
                miniPlayer.localRotation = Quaternion.Euler(0f, 0f, -player.transform.eulerAngles.y);
                if (hasMarker)
                {
                    QuestManager.TryGetMarker(q, out markerPos);
                    Vector2 d = (WorldToMap(markerPos) - uv) / span;
                    d = Vector2.ClampMagnitude(d, 0.47f);
                    miniQuest.anchoredPosition = d * minimap.rectTransform.sizeDelta.x;
                }
                miniQuest.gameObject.SetActive(hasMarker);
            }

            bannerTime += Time.unscaledDeltaTime;
            bannerGroup.alpha = bannerTime < 0.2f ? bannerTime / 0.2f : Mathf.Clamp01(1f - (bannerTime - 1.8f) / 0.6f);
        }

        Vector2 WorldToMap(Vector3 w) => new((w.x - worldMin.x) / worldSize.x, (w.z - worldMin.y) / worldSize.y);

        void UpdateToasts()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var (text, t) = toasts[i];
                t += Time.unscaledDeltaTime;
                toasts[i] = (text, t);
                var c = text.color;
                c.a = Mathf.Clamp01(1f - (t - 3.5f) / 0.8f);
                text.color = c;
                if (t > 4.3f)
                {
                    Destroy(text.gameObject);
                    toasts.RemoveAt(i);
                }
            }
        }

        void RefreshParty() { }

        // =============================================================== Dialogue

        public void ShowDialogue(List<DialogueLine> lines, Action onDone)
        {
            if (lines == null || lines.Count == 0) { onDone?.Invoke(); return; }
            dialogueLines = lines;
            dialogueIndex = 0;
            dialogueDone = onDone;
            dialoguePanel.SetActive(true);
            GameState.UIOpen = true;
            ShowLine();
        }

        void ShowLine()
        {
            var line = dialogueLines[dialogueIndex];
            dialogueSpeaker.text = line.speaker;
            dialogueText.text = "";
            typeT = 0f;
            foreach (Transform c in choiceRoot) Destroy(c.gameObject);
            if (line.choices != null)
            {
                float y = 0f;
                foreach (var (label, action) in line.choices)
                {
                    var a = action;
                    var b = MakeButton(choiceRoot, label, () =>
                    {
                        a?.Invoke();
                        if (a == null) EndDialogue();
                    }, 460f, 40f);
                    ((RectTransform)b.transform).anchoredPosition = new Vector2(0f, y);
                    y -= 46f;
                }
            }
        }

        void UpdateDialogue(Keyboard kb)
        {
            if (!DialogueOpen) return;
            var line = dialogueLines[dialogueIndex];
            typeT += Time.unscaledDeltaTime * 55f;
            int n = Mathf.Min(line.text.Length, (int)typeT);
            dialogueText.text = line.text.Substring(0, n);
            bool advance = (kb != null && (kb.fKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                           || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && line.choices == null);
            if (advance) AdvanceDialogue();
        }

        /// <summary>Completes the line being typed, or moves to the next one (F / Space / Enter / click).</summary>
        public void AdvanceDialogue()
        {
            if (!DialogueOpen) return;
            var line = dialogueLines[dialogueIndex];
            if (dialogueText.text.Length < line.text.Length) { typeT = line.text.Length; dialogueText.text = line.text; return; }
            if (line.choices != null) return;
            dialogueIndex++;
            if (dialogueIndex >= dialogueLines.Count) EndDialogue();
            else ShowLine();
        }

        /// <summary>Picks a dialogue choice by index (same as clicking its button).</summary>
        public void ChooseDialogueOption(int index)
        {
            if (!DialogueOpen) return;
            var choices = dialogueLines[dialogueIndex].choices;
            if (choices == null || index < 0 || index >= choices.Count) return;
            var action = choices[index].action;
            action?.Invoke();
            if (action == null) EndDialogue();
        }

        /// <summary>Closes the dialogue from outside (a choice action that starts a cinematic), running its completion callback.</summary>
        public void CloseDialogue()
        {
            if (DialogueOpen) EndDialogue();
        }

        void EndDialogue()
        {
            dialoguePanel.SetActive(false);
            GameState.UIOpen = AnyMenuOpen() || titlePanel.activeSelf;
            var done = dialogueDone;
            dialogueDone = null;
            done?.Invoke();
        }

        // =============================================================== Menu content

        void RefreshMap()
        {
            foreach (Transform c in bigMapMarkers) Destroy(c.gameObject);
            var size = bigMap.rectTransform.sizeDelta;
            RectTransform Marker(Vector3 world, Color color, float s)
            {
                var img = new GameObject("M", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(bigMapMarkers, false);
                img.color = color;
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(s, s);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                Vector2 uv = WorldToMap(world);
                rt.anchoredPosition = new Vector2(uv.x * size.x, uv.y * size.y);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                return rt;
            }

            foreach (var w in Waypoint.AllWaypoints)
            {
                var rt = Marker(w.transform.position, w.Activated ? Elements.Color(Element.Anemo) : new Color(0.5f, 0.5f, 0.5f), 22f);
                if (!w.Activated) continue;
                var wp = w;
                var btn = rt.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    CloseMenus();
                    player.Teleport(wp.SpawnPoint);
                    party.RespawnPoint = wp.SpawnPoint;
                    Toast($"Téléporté : {wp.displayName}");
                });
                var label = MakeText(rt, wp.displayName, 16, TextAnchor.MiddleLeft, Color.white);
                label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                label.rectTransform.anchoredPosition = new Vector2(70f, 0f);
                label.rectTransform.sizeDelta = new Vector2(220f, 24f);
            }
            if (QuestManager.Instance && QuestManager.TryGetMarker(QuestManager.Instance.Tracked, out var qp)) Marker(qp, Accent, 18f);
            var pm = Marker(player.transform.position, Color.white, 16f);
            pm.localRotation = Quaternion.Euler(0f, 0f, 45f - player.transform.eulerAngles.y);
        }

        void RefreshInventory()
        {
            foreach (Transform c in inventoryList) Destroy(c.gameObject);
            float y = 0f;
            foreach (var kv in Inventory.All())
            {
                string id = kv.Key;
                Inventory.Defs.TryGetValue(id, out var def);
                var b = MakeButton(inventoryList, $"{Inventory.Name(id)}   ×{kv.Value}", () => SelectItem(id), 520f, 42f);
                ((RectTransform)b.transform).anchoredPosition = new Vector2(0f, y);
                if (def != null) b.GetComponentInChildren<Text>().color = Color.Lerp(def.color, Color.white, 0.4f);
                y -= 48f;
            }
            SelectItem(selectedItem);
        }

        Button useButton;

        void SelectItem(string id)
        {
            selectedItem = id;
            if (id == null || !Inventory.Defs.TryGetValue(id, out var def) || Inventory.Count(id) <= 0)
            {
                inventoryDetail.text = "Sélectionne un objet.";
                useButton.gameObject.SetActive(false);
                return;
            }
            inventoryDetail.text = $"<b>{def.name}</b>\nPossédé : {Inventory.Count(id)}\n\n{def.description}";
            useButton.gameObject.SetActive(def.usable);
        }

        void RefreshCharacters()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var m in party.members)
            {
                if (!m.unlocked)
                {
                    sb.AppendLine($"<color=#777777>???  — pas encore rencontré</color>\n");
                    continue;
                }
                string hex = ColorUtility.ToHtmlStringRGB(Elements.Color(m.element));
                sb.AppendLine($"<size=30><b>{m.name}</b></size>  <color=#{hex}>{Elements.Name(m.element)}</color>   <i>{m.title}</i>");
                sb.AppendLine($"Niveau {m.level}   XP {m.xp}/{m.XPToNext}");
                sb.AppendLine($"PV {Mathf.RoundToInt(m.hp)}/{Mathf.RoundToInt(m.MaxHP)}   ATQ {Mathf.RoundToInt(m.ATK)}   Taux CRIT {m.critRate * 100f:0}%   DGT CRIT {m.critDamage * 100f:0}%");
                sb.AppendLine($"<color=#{hex}>E — {m.skillName}</color> (recharge {m.skillCooldown:0} s)   <color=#{hex}>Q — {m.burstName}</color> ({m.burstEnergy:0} énergie)\n");
            }
            sb.AppendLine($"Leçons d'aventurier : {Inventory.Count("xp_book")}  (utilisables depuis l'inventaire)");
            charactersText.text = sb.ToString();
        }

        void RefreshQuests()
        {
            foreach (Transform c in questsList) Destroy(c.gameObject);
            float y = 0f;
            foreach (var q in QuestManager.Instance.quests)
            {
                if (!QuestManager.Instance.IsAvailable(q)) continue; // later chapters stay secret until unlocked
                var quest = q;
                string label = (q.main ? "★ " : "• ") + q.title + (q.done ? "  (terminée)" : "");
                var b = MakeButton(questsList, label, () => SelectQuest(quest), 520f, 40f);
                ((RectTransform)b.transform).anchoredPosition = new Vector2(0f, y);
                y -= 44f;
            }
            SelectQuest(selectedQuest ?? QuestManager.Instance.Tracked);
        }

        Button trackButton;

        void SelectQuest(Quest q)
        {
            selectedQuest = q;
            if (q == null) { questsDetail.text = ""; return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<size=28><b>{q.title}</b></size>\n");
            sb.AppendLine($"<i>{q.summary}</i>\n");
            for (int i = 0; i < q.steps.Count; i++)
            {
                if (i > q.current && !q.done) break;
                bool done = q.done || i < q.current;
                sb.AppendLine(done ? $"<color=#88aa88>✔ {q.steps[i].text}</color>" : $"<color=#{ColorUtility.ToHtmlStringRGB(Accent)}>◆ {QuestManager.StepProgress(q)}</color>");
            }
            questsDetail.text = sb.ToString();
            trackButton.gameObject.SetActive(!q.done);
        }

        // =============================================================== Construction

        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>()) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
        }

        void Build()
        {
            var canvasGO = new GameObject("UI Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)canvasGO.transform;

            BuildHUD();
            BuildDialogue();
            BuildMenus();
        }

        void BuildHUD()
        {
            hud = Rect(root, "HUD", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Minimap
            var miniFrame = Box(hud, "Minimap", new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(230f, 230f), new Color(0f, 0f, 0f, 0.5f));
            miniFrame.gameObject.AddComponent<RectMask2D>();
            minimap = new GameObject("Map", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            minimap.transform.SetParent(miniFrame, false);
            minimap.texture = mapTexture;
            Stretch(minimap.rectTransform, 4f);
            minimap.raycastTarget = false;
            miniQuest = Box(minimap.rectTransform, "Quest", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f), Accent);
            miniQuest.localRotation = Quaternion.Euler(0f, 0f, 45f);
            miniPlayer = Box(minimap.rectTransform, "Player", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 18f), Color.white);
            MakeText(miniFrame, "N", 18, TextAnchor.UpperCenter, Accent).rectTransform.anchoredPosition = new Vector2(0f, -2f);

            // Quest tracker
            questTitle = MakeText(hud, "", 22, TextAnchor.UpperLeft, Accent);
            Place(questTitle.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -275f), new Vector2(520f, 30f));
            questStep = MakeText(hud, "", 20, TextAnchor.UpperLeft, Color.white);
            Place(questStep.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -305f), new Vector2(560f, 60f));
            questStep.horizontalOverflow = HorizontalWrapMode.Wrap;

            // Clock / state
            clockText = MakeText(hud, "", 28, TextAnchor.UpperRight, Color.white);
            Place(clockText.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(300f, 36f));
            stateText = MakeText(hud, "", 20, TextAnchor.UpperRight, new Color(0.85f, 0.95f, 1f));
            Place(stateText.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -60f), new Vector2(300f, 28f));

            // Party list
            for (int i = 0; i < 4; i++)
            {
                var row = Box(hud, $"Party{i}", new Vector2(1f, 0.5f), new Vector2(-24f, 120f - i * 70f), new Vector2(260f, 60f), new Color(0f, 0f, 0f, 0.45f));
                row.pivot = new Vector2(1f, 0.5f);
                var label = MakeText(row, "", 20, TextAnchor.UpperLeft, Color.white);
                Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(26f, -6f), new Vector2(230f, 26f));
                var hpBg = Box(row, "HPbg", new Vector2(0f, 0f), new Vector2(26f, 10f), new Vector2(220f, 10f), new Color(0f, 0f, 0f, 0.6f));
                var hp = new GameObject("HP", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                hp.transform.SetParent(hpBg, false);
                Stretch(hp.rectTransform, 1f);
                hp.rectTransform.pivot = new Vector2(0f, 0.5f);
                var dot = Box(row, "Elem", new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(12f, 12f), Color.white).GetComponent<Image>();
                dot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                if (party && i < party.members.Length) dot.color = Elements.Color(party.members[i].element);
                partyRows.Add((row, label, hp, dot));
            }

            // Active character HP
            nameText = MakeText(hud, "", 22, TextAnchor.LowerCenter, Color.white);
            Place(nameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(500f, 30f));
            var hpBack = Box(hud, "HPBack", new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(520f, 16f), new Color(0f, 0f, 0f, 0.6f));
            hpFill = new GameObject("HP", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            hpFill.transform.SetParent(hpBack, false);
            Stretch(hpFill.rectTransform, 2f);
            hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            hpFill.color = new Color(0.55f, 0.9f, 0.45f);
            shieldFill = new GameObject("Shield", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            shieldFill.transform.SetParent(hpBack, false);
            Stretch(shieldFill.rectTransform, 2f);
            shieldFill.rectTransform.pivot = new Vector2(0f, 0.5f);

            // Skill & burst
            var ring = RingSprite(128, 0.82f);
            var disc = RingSprite(128, 0f);
            (skillFill, skillText, skillRing) = AbilityIcon(new Vector2(-250f, 70f), 86f, disc, ring);
            (burstFill, burstText, burstRing) = AbilityIcon(new Vector2(-120f, 80f), 110f, disc, ring);

            // Stamina ring
            var st = new GameObject("Stamina", typeof(RectTransform), typeof(CanvasGroup));
            st.transform.SetParent(hud, false);
            staminaRect = (RectTransform)st.transform;
            staminaRect.sizeDelta = new Vector2(64f, 64f);
            staminaGroup = st.GetComponent<CanvasGroup>();
            staminaGroup.blocksRaycasts = false;
            var sbg = Img(staminaRect, ring, new Color(0f, 0f, 0f, 0.45f));
            Stretch(sbg.rectTransform, 0f);
            staminaFill = Img(staminaRect, ring, Color.green);
            Stretch(staminaFill.rectTransform, 0f);
            staminaFill.type = Image.Type.Filled;
            staminaFill.fillMethod = Image.FillMethod.Radial360;
            staminaFill.fillOrigin = (int)Image.Origin360.Top;
            staminaFill.fillClockwise = false;

            // Prompt
            promptRoot = Box(hud, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(330f, -60f), new Vector2(420f, 44f), new Color(0f, 0f, 0f, 0.55f));
            promptText = MakeText(promptRoot, "", 22, TextAnchor.MiddleLeft, Color.white);
            Stretch(promptText.rectTransform, 14f);

            // Objective marker
            questMarker = Box(hud, "Objective", new Vector2(0f, 0f), Vector2.zero, new Vector2(22f, 22f), Accent);
            questMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            questMarkerDist = MakeText(questMarker, "", 18, TextAnchor.UpperCenter, Color.white);
            questMarkerDist.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            questMarkerDist.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            questMarkerDist.rectTransform.sizeDelta = new Vector2(120f, 24f);

            // Toasts & banner
            toastRoot = Rect(hud, "Toasts", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, -20f), new Vector2(560f, 260f));
            var vl = toastRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment = TextAnchor.UpperLeft;
            vl.childControlHeight = vl.childControlWidth = false;
            vl.spacing = 4f;
            var bannerGO = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            bannerGO.transform.SetParent(hud, false);
            bannerGroup = bannerGO.GetComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            Place((RectTransform)bannerGO.transform, new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(1200f, 80f));
            bannerText = MakeText(bannerGO.transform, "", 46, TextAnchor.MiddleCenter, Color.white);
            Stretch(bannerText.rectTransform, 0f);
            bannerText.fontStyle = FontStyle.Bold;

            helpText = MakeText(hud,
                "ZQSD déplacer · Espace sauter/planer · Shift/Clic droit esquiver (maintenir = sprint) · Clic gauche attaquer (maintenir = charge)\n" +
                "E compétence · Q déchaînement · 1-4 changer de perso · F interagir · M carte · B sac · C équipe · J quêtes · Échap menu · H aide",
                16, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.75f));
            Place(helpText.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 14f), new Vector2(1200f, 50f));
        }

        (Image fill, Text label, Image ring) AbilityIcon(Vector2 offsetFromBottomRight, float size, Sprite disc, Sprite ringSprite)
        {
            var r = Rect(hud, "Ability", new Vector2(1f, 0f), new Vector2(1f, 0f), offsetFromBottomRight, new Vector2(size, size));
            var bg = Img(r, disc, new Color(0f, 0f, 0f, 0.5f));
            Stretch(bg.rectTransform, 0f);
            var fill = Img(r, disc, Color.white);
            Stretch(fill.rectTransform, 6f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Bottom;
            var ringImg = Img(r, ringSprite, new Color(1f, 1f, 1f, 0.3f));
            Stretch(ringImg.rectTransform, 0f);
            var t = MakeText(r, "", (int)(size * 0.32f), TextAnchor.MiddleCenter, Color.white);
            Stretch(t.rectTransform, 0f);
            t.fontStyle = FontStyle.Bold;
            return (fill, t, ringImg);
        }

        void BuildDialogue()
        {
            var panel = Box(root, "Dialogue", new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1300f, 220f), Panel);
            dialoguePanel = panel.gameObject;
            dialogueSpeaker = MakeText(panel, "", 28, TextAnchor.UpperLeft, Accent);
            Place(dialogueSpeaker.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(600f, 36f));
            dialogueSpeaker.fontStyle = FontStyle.Bold;
            dialogueText = MakeText(panel, "", 24, TextAnchor.UpperLeft, Color.white);
            Place(dialogueText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -62f), new Vector2(1220f, 140f));
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            choiceRoot = Rect(root, "Choices", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(380f, 480f), new Vector2(460f, 300f));
            choiceRoot.SetParent(panel, true);
            var hint = MakeText(panel, "F / Espace : continuer", 16, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.5f));
            Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-20f, 12f), new Vector2(300f, 24f));
            dialoguePanel.SetActive(false);
        }

        void BuildMenus()
        {
            // Title screen
            var title = FullPanel("Titre", new Color(0.02f, 0.03f, 0.05f, 0.55f));
            titlePanel = title.gameObject;
            var t1 = MakeText(title, "LE DERNIER MONDE", 92, TextAnchor.MiddleCenter, Accent);
            Place(t1.rectTransform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(1400f, 120f));
            t1.fontStyle = FontStyle.Bold;
            var t2 = MakeText(title, "Prologue & Chapitre I — Les Vents Oubliés", 32, TextAnchor.MiddleCenter, Color.white);
            var ver = MakeText(title, $"v{Application.version}", 18, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.5f));
            Place(ver.rectTransform, new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(200f, 26f));
            Place(t2.rectTransform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1000f, 50f));
            float by = -40f;
            void TitleButton(string label, Action a)
            {
                var b = MakeButton(title, label, a, 380f, 56f);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.45f);
                rt.anchoredPosition = new Vector2(0f, by);
                by -= 70f;
            }
            TitleButton("Nouvelle partie", () =>
            {
                titlePanel.SetActive(false);
                GameState.UIOpen = false;
                SaveSystem.NewGame();
                StoryEvents.NewGame();
            });
            if (SaveSystem.HasSave) TitleButton("Continuer", SaveSystem.ContinueGame);
            TitleButton("Quitter", Quit);

            // Pause
            var pause = FullPanel("Pause", new Color(0f, 0f, 0f, 0.6f));
            pausePanel = pause.gameObject;
            var pt = MakeText(pause, "Pause", 56, TextAnchor.MiddleCenter, Accent);
            Place(pt.rectTransform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(600f, 80f));
            float py = 120f;
            void PauseButton(string label, Action a)
            {
                var b = MakeButton(pause, label, a, 380f, 54f);
                ((RectTransform)b.transform).anchoredPosition = new Vector2(-260f, py);
                py -= 66f;
            }
            PauseButton("Reprendre", CloseMenus);
            PauseButton("Sauvegarder", () => { SaveSystem.Save(); Toast("Partie sauvegardée."); });
            PauseButton("Carte (M)", () => Toggle(mapPanel, RefreshMap));
            PauseButton("Quêtes (J)", () => Toggle(questsPanel, RefreshQuests));
            PauseButton("Quitter le jeu", () => { SaveSystem.Save(); Quit(); });
            var cfg = Box(pause, "Settings", new Vector2(0.5f, 0.5f), new Vector2(260f, 20f), new Vector2(480f, 330f), Panel);
            var cfgTitle = MakeText(cfg, "Paramètres", 28, TextAnchor.UpperCenter, Accent);
            Place(cfgTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(400f, 40f));
            var camCtl = FindAnyObjectByType<ThirdPersonCamera>();
            var audio = AudioManager.Instance;
            Slider(cfg, "Sensibilité souris", -80f, 0.03f, 0.4f, camCtl ? camCtl.mouseSensitivity : 0.12f, v => { if (camCtl) camCtl.mouseSensitivity = v; });
            Slider(cfg, "Volume général", -160f, 0f, 1f, audio ? audio.masterVolume : 0.8f, v => { if (audio) audio.masterVolume = v; });
            Slider(cfg, "Musique", -240f, 0f, 1f, audio ? audio.musicVolume : 0.35f, v => { if (audio) audio.musicVolume = v; });

            // Map
            var map = FullPanel("Carte", new Color(0.02f, 0.03f, 0.05f, 0.92f));
            mapPanel = map.gameObject;
            bigMap = new GameObject("BigMap", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            bigMap.transform.SetParent(map, false);
            bigMap.texture = mapTexture;
            Place(bigMap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(940f, 940f));
            bigMapMarkers = Rect(bigMap.rectTransform, "Markers", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mt = MakeText(map, "Carte d'Aerendal — clique sur une Sentinelle activée pour t'y téléporter", 22, TextAnchor.UpperCenter, Accent);
            Place(mt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1200f, 34f));

            // Inventory
            var inv = FullPanel("Sac", new Color(0.02f, 0.03f, 0.05f, 0.9f));
            inventoryPanel = inv.gameObject;
            Header(inv, "Sac");
            inventoryList = Rect(inv, "List", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-330f, -140f), new Vector2(520f, 700f));
            var invDetailBox = Box(inv, "Detail", new Vector2(0.5f, 0.5f), new Vector2(330f, 40f), new Vector2(600f, 520f), Panel);
            inventoryDetail = MakeText(invDetailBox, "", 24, TextAnchor.UpperLeft, Color.white);
            Stretch(inventoryDetail.rectTransform, 24f);
            inventoryDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            inventoryDetail.supportRichText = true;
            useButton = MakeButton(invDetailBox, "Utiliser", () => { Inventory.Use(selectedItem); RefreshInventory(); }, 240f, 50f);
            var ub = (RectTransform)useButton.transform;
            ub.anchorMin = ub.anchorMax = new Vector2(0.5f, 0f);
            ub.anchoredPosition = new Vector2(0f, 50f);

            // Characters
            var chars = FullPanel("Equipe", new Color(0.02f, 0.03f, 0.05f, 0.9f));
            charactersPanel = chars.gameObject;
            Header(chars, "Équipe");
            var charBox = Box(chars, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1500f, 820f), Panel);
            charactersText = MakeText(charBox, "", 22, TextAnchor.UpperLeft, Color.white);
            Stretch(charactersText.rectTransform, 30f);
            charactersText.horizontalOverflow = HorizontalWrapMode.Wrap;
            charactersText.supportRichText = true;

            // Quests
            var quests = FullPanel("Quetes", new Color(0.02f, 0.03f, 0.05f, 0.9f));
            questsPanel = quests.gameObject;
            Header(quests, "Quêtes");
            questsList = Rect(quests, "List", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-330f, -140f), new Vector2(520f, 700f));
            var qBox = Box(quests, "Detail", new Vector2(0.5f, 0.5f), new Vector2(330f, 30f), new Vector2(640f, 620f), Panel);
            questsDetail = MakeText(qBox, "", 22, TextAnchor.UpperLeft, Color.white);
            Stretch(questsDetail.rectTransform, 24f);
            questsDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            questsDetail.supportRichText = true;
            trackButton = MakeButton(qBox, "Suivre cette quête", () => { QuestManager.Instance.Track(selectedQuest); Toast("Quête suivie."); }, 300f, 50f);
            var tb = (RectTransform)trackButton.transform;
            tb.anchorMin = tb.anchorMax = new Vector2(0.5f, 0f);
            tb.anchoredPosition = new Vector2(0f, 46f);

            foreach (var p in new[] { pausePanel, mapPanel, inventoryPanel, charactersPanel, questsPanel }) p.SetActive(false);
        }

        void Header(RectTransform panel, string text)
        {
            var t = MakeText(panel, text, 48, TextAnchor.UpperCenter, Accent);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(800f, 64f));
            var hint = MakeText(panel, "Échap pour fermer", 18, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.5f));
            Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(300f, 30f));
        }

        void Slider(RectTransform parent, string label, float y, float min, float max, float value, Action<float> onChange)
        {
            var t = MakeText(parent, label, 20, TextAnchor.MiddleLeft, Color.white);
            Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(-40f, y), new Vector2(360f, 30f));
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, new Vector2(0.5f, 1f), new Vector2(0f, y - 32f), new Vector2(400f, 20f));
            var bg = Box(rt, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 8f), new Color(1f, 1f, 1f, 0.2f));
            var fillArea = Rect(rt, "Fill Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fill = Box(fillArea, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f), Accent);
            fill.anchorMin = new Vector2(0f, 0.3f);
            fill.anchorMax = new Vector2(1f, 0.7f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var handleArea = Rect(rt, "Handle Area", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = Box(handleArea, "Handle", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(20f, 20f), Color.white);
            var s = go.GetComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = handle.GetComponent<Image>();
            s.minValue = min;
            s.maxValue = max;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =============================================================== uGUI helpers

        RectTransform FullPanel(string name, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(root, false);
            img.color = color;
            Stretch(img.rectTransform, 0f);
            return img.rectTransform;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            if (anchorMin == anchorMax)
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }
            else
            {
                rt.offsetMin = pos;
                rt.offsetMax = size;
            }
            return rt;
        }

        static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            img.raycastTarget = false;
            Place(img.rectTransform, anchor, pos, size);
            return img.rectTransform;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static Image Img(Transform parent, Sprite sprite, Color color)
        {
            var img = new GameObject("Img", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text MakeText(Transform parent, string text, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.6f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        Button MakeButton(Transform parent, string label, Action onClick, float w, float h)
        {
            var img = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = new Color(0.12f, 0.16f, 0.22f, 0.92f);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            var b = img.GetComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(0.3f, 0.36f, 0.45f);
            colors.pressedColor = new Color(0.55f, 0.48f, 0.3f);
            b.colors = colors;
            b.onClick.AddListener(() => { AudioManager.Play("ui"); onClick?.Invoke(); });
            var t = MakeText(rt, label, 22, TextAnchor.MiddleCenter, Color.white);
            Stretch(t.rectTransform, 8f);
            return b;
        }

        static Sprite RingSprite(int size, float innerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f, inner = c * innerRatio;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(c - d) * (innerRatio > 0f ? Mathf.Clamp01(d - inner) : 1f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
