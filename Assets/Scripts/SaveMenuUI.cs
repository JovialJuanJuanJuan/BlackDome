using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The visual-novel save and load screens, plus the in-game pause menu.
///
/// Everything is built in code on a single overlay canvas that survives scene loads, so no scene
/// has to be edited and no prefab has to be dragged in: the screens appear the first time the
/// player presses Escape or Load Game.
///
/// The screens are cut from slanted slabs, the way the Persona menus are: a black poster with a
/// white cut edge and a slate shadow, a white label block with black type, a rotated headline
/// and a grid of skewed boxes. Each box carries a big slot number, a tilted
/// screenshot and a hairline that snaps white under the pointer. The same twelve boxes are used
/// for saving and for loading; a filled box shows the scene, the date, the line that was on
/// screen and a thumbnail of the moment itself.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaveMenuUI : MonoBehaviour
{
    enum Mode
    {
        Closed,
        Pause,
        Save,
        Load,
        Loading
    }

    // ------------------------------------------------------------------ layout

    const float WindowWidth = 1700f;
    const float WindowHeight = 940f;
    const float CardWidth = 508f;
    const float CardHeight = 150f;
    const float ThumbWidth = 192f;
    const float ThumbHeight = 108f;
    const int SlotColumns = 3;
    const float WindowSlant = 40f;
    const float CardSlant = 16f;
    const float PlateSlant = 14f;

    // ------------------------------------------------------------------ materials

    const float HoverSpeed = 9f;
    const float PressSpeed = 20f;
    const float PauseMusicLevel = 0.50f;
    const float PauseMusicFadeIn = 0.90f;
    const float PauseMusicFadeOut = 0.55f;

    static readonly Color BackdropColor = new Color(0.002f, 0.002f, 0.003f, 0.94f);
    static readonly Color WindowFace = new Color(0.012f, 0.012f, 0.013f, 1f);
    static readonly Color WindowEdge = new Color(0.93f, 0.93f, 0.94f, 0.92f);
    static readonly Color WindowShadow = new Color(0.13f, 0.13f, 0.14f, 0.55f);
    static readonly Color CardFace = new Color(0.038f, 0.038f, 0.039f, 1f);
    static readonly Color CardEdgeIdle = new Color(1f, 1f, 1f, 0.28f);
    static readonly Color CardEdgeHover = new Color(1f, 1f, 1f, 0.95f);
    static readonly Color CardEdgeDisabled = new Color(1f, 1f, 1f, 0.09f);
    static readonly Color PlateFace = new Color(0.042f, 0.042f, 0.043f, 1f);
    static readonly Color PlateEdgeIdle = new Color(1f, 1f, 1f, 0.30f);
    static readonly Color PlateEdgeHover = new Color(1f, 1f, 1f, 0.95f);
    static readonly Color InvertedIdle = new Color(0.92f, 0.92f, 0.93f, 1f);
    static readonly Color InvertedHover = new Color(0.80f, 0.80f, 0.81f, 1f);
    static readonly Color Ink = new Color(0.975f, 0.975f, 0.975f, 1f);
    static readonly Color Body = new Color(0.74f, 0.74f, 0.74f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.62f, 0.62f, 1f);
    static readonly Color Faint = new Color(0.46f, 0.46f, 0.46f, 1f);
    static readonly Color Ghost = new Color(0.31f, 0.31f, 0.31f, 1f);
    static readonly Color NumberIdle = new Color(0.64f, 0.64f, 0.64f, 1f);
    static readonly Color NumberDisabled = new Color(0.28f, 0.28f, 0.28f, 1f);
    static readonly Color Steel = new Color(0.56f, 0.70f, 0.84f, 1f);
    static readonly Color SteelDim = new Color(0.44f, 0.55f, 0.66f, 1f);
    static readonly Color TagFace = new Color(0.94f, 0.94f, 0.95f, 1f);
    static readonly Color TagInk = new Color(0.05f, 0.05f, 0.05f, 1f);
    static readonly Color ThumbWell = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color FrameFace = new Color(0.88f, 0.88f, 0.89f, 1f);
    static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);

    sealed class SlotCard
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Button button;
        public SkewedPanel edge;
        public SkewedPanel sheen;
        public SkewedPanel accent;
        public TMP_Text number;
        public TMP_Text mini;
        public GameObject thumbFrame;
        public RawImage thumbnail;
        public TMP_Text sceneLabel;
        public TMP_Text dateLabel;
        public TMP_Text previewLabel;
        public TMP_Text emptyLabel;
        public bool hovered;
        public bool pressed;
        public bool interactable = true;
        public float hover;
        public float press;
    }

    /// <summary>The hover and press state of one menu plate.</summary>
    sealed class PlateFx
    {
        public RectTransform rect;
        public Vector2 home;
        public float slide;
        public SkewedPanel edge;
        public SkewedPanel sheen;
        public Image fillTint;
        public Color fillIdle;
        public Color fillHover;
        public TMP_Text label;
        public Color labelIdle;
        public Color labelHover;
        public Color edgeIdle;
        public Color edgeHover;
        public float pressScale = 0.985f;
        public bool interactable = true;
        public bool hovered;
        public bool pressed;
        public float hover;
        public float press;
    }

    // ------------------------------------------------------------------ type

    static TMP_FontAsset boldFont;
    static TMP_FontAsset regularFont;

    static TMP_FontAsset BoldFont
    {
        get
        {
            if (boldFont == null || boldFont == TMP_Settings.defaultFontAsset)
                boldFont = LoadFont("Fonts/neuemontreal-bold SDF", "neuemontreal-bold");

            return boldFont;
        }
    }

    static TMP_FontAsset RegularFont
    {
        get
        {
            if (regularFont == null || regularFont == TMP_Settings.defaultFontAsset)
                regularFont = LoadFont("Fonts/neuemontreal-regular SDF", "neuemontreal-regular");

            return regularFont;
        }
    }

    /// <summary>
    /// The game's own face, loaded from Resources so the screens are built without a scene
    /// reference. If the Resources copy is not indexed yet, a font the scene already loaded is
    /// borrowed instead, so the menu never falls back to TextMeshPro's default type.
    /// </summary>
    static TMP_FontAsset LoadFont(string resourcePath, string assetNamePrefix)
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(resourcePath);
        if (font != null)
            return font;

        TMP_FontAsset[] loaded = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null
                && loaded[i].name.StartsWith(assetNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return loaded[i];
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    // ------------------------------------------------------------------ state

    public static SaveMenuUI Instance { get; private set; }

    public bool IsOpen
    {
        get { return mode != Mode.Closed; }
    }

    Canvas canvas;
    Canvas pauseHintCanvas;
    RectTransform windowRect;
    CanvasGroup windowGroup;
    GameObject pausePanel;
    GameObject slotsPanel;
    GameObject loadingPanel;
    GameObject confirmPanel;

    TMP_Text titleText;
    TMP_Text subtitleText;
    TMP_Text footerHint;
    TMP_Text loadingTitle;
    TMP_Text loadingProgress;
    TMP_Text toastText;
    TMP_Text confirmMessage;
    TMP_Text confirmYesLabel;

    Button backButton;

    readonly List<SlotCard> cards = new List<SlotCard>();
    readonly List<PlateFx> plates = new List<PlateFx>();
    readonly Texture2D[] cardTextures = new Texture2D[SaveSlots.SlotCount];

    AudioSource hoverAudio;
    AudioClip hoverClip;
    bool hoverClipLoaded;
    float lastHoverSoundTime = float.NegativeInfinity;

    AudioSource clickAudio;
    AudioClip clickClip;
    bool clickClipLoaded;
    float lastClickSoundTime = float.NegativeInfinity;

    AudioSource pauseMusicAudio;
    AudioClip pauseMusicClip;
    bool pauseMusicClipLoaded;
    float pauseMusicVolume;
    float pauseMusicTarget;

    Mode mode = Mode.Closed;
    bool openedFromPause;
    Action confirmAction;
    float toastUntil;

    float enterStart = -1f;

    void Awake()
    {
        Instance = this;
        BuildPauseHint();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        ClearCardTextures();

        if (hoverAudio != null)
            Destroy(hoverAudio.gameObject);

        if (clickAudio != null)
            Destroy(clickAudio.gameObject);

        if (pauseMusicAudio != null)
            Destroy(pauseMusicAudio.gameObject);
    }

    // ------------------------------------------------------------------ opening

    /// <summary>Opens the load screen from the title menu. Nothing is paused there.</summary>
    public static void OpenLoadFromMenu()
    {
        SaveGameManager.EnsureExists();

        if (Instance != null)
            Instance.OpenLoad(false);
    }

    public void OpenPause()
    {
        EnsureBuilt();
        openedFromPause = true;
        SetMode(Mode.Pause);
    }

    public void OpenSave()
    {
        EnsureBuilt();
        openedFromPause = true;
        RefreshCards(Mode.Save);
        SetMode(Mode.Save);
    }

    public void OpenLoad(bool fromPause)
    {
        EnsureBuilt();
        openedFromPause = fromPause;
        RefreshCards(Mode.Load);
        SetMode(Mode.Load);
    }

    public void CloseMenu()
    {
        if (canvas == null)
            return;

        SetMode(Mode.Closed);
        HideConfirm();
    }

    public void HandleEscape()
    {
        if (confirmPanel != null && confirmPanel.activeSelf)
        {
            HideConfirm();
            return;
        }

        switch (mode)
        {
            case Mode.Loading:
                return;

            case Mode.Pause:
                SaveGameManager.Instance.ResumeGame();
                return;

            case Mode.Save:
            case Mode.Load:
                BackPressed();
                return;

            case Mode.Closed:
                if (SaveGameManager.Instance != null)
                    SaveGameManager.Instance.PauseGame();
                return;
        }
    }

    // ------------------------------------------------------------------ screens

    void SetMode(Mode next)
    {
        if (canvas == null)
            return;

        Mode previous = mode;
        mode = next;

        if (next == Mode.Closed)
        {
            StopPauseMusic();
            canvas.enabled = false;
            ClearCardTextures();
            return;
        }

        canvas.enabled = true;

        bool slots = next == Mode.Save || next == Mode.Load;

        windowRect.gameObject.SetActive(next == Mode.Pause || slots);
        pausePanel.SetActive(next == Mode.Pause);
        slotsPanel.SetActive(slots);
        loadingPanel.SetActive(next == Mode.Loading);

        if (next != Mode.Loading)
            HideConfirm();

        if (backButton != null)
            backButton.gameObject.SetActive(slots);

        if (next == Mode.Pause)
        {
            titleText.text = "PAUSED";
            subtitleText.text = "ESC TO RESUME";
            footerHint.text = string.Empty;
        }
        else if (next == Mode.Save)
        {
            titleText.text = "SAVE GAME";
            subtitleText.text = "ESC TO STEP BACK";
            footerHint.text = "Choose a box to save your place.";
        }
        else if (next == Mode.Load)
        {
            titleText.text = "LOAD GAME";
            subtitleText.text = "ESC TO STEP BACK";
            footerHint.text = "Choose a box to continue from there.";
        }

        // The pause theme belongs to the pause flow, never to the title menu's load screen.
        if (next == Mode.Loading)
            StopPauseMusic();
        else if (openedFromPause)
            StartPauseMusic();

        if (previous != next && next != Mode.Loading)
            BeginEnter();
    }

    void BackPressed()
    {
        if (confirmPanel != null && confirmPanel.activeSelf)
        {
            HideConfirm();
            return;
        }

        if (mode == Mode.Save || mode == Mode.Load)
        {
            if (openedFromPause)
            {
                SetMode(Mode.Pause);
            }
            else
            {
                CloseMenu();
            }

            return;
        }

        if (mode == Mode.Pause && SaveGameManager.Instance != null)
            SaveGameManager.Instance.ResumeGame();
    }

    // ------------------------------------------------------------------ slots

    void OnSlotClicked(int slot)
    {
        if (mode == Mode.Save)
        {
            if (SaveSlots.HasSave(slot))
            {
                ShowConfirm(
                    "Overwrite slot " + slot.ToString("00") + "? The save already in it will be lost.",
                    "OVERWRITE",
                    () => SaveGameManager.Instance.SaveToSlot(slot));
            }
            else
            {
                SaveGameManager.Instance.SaveToSlot(slot);
            }

            return;
        }

        if (mode == Mode.Load)
        {
            if (!SaveSlots.HasSave(slot))
            {
                ShowToast("SLOT " + slot.ToString("00") + " IS EMPTY");
                return;
            }

            SaveGameManager.Instance.LoadFromSlot(slot);
        }
    }

    public void RefreshSlots()
    {
        if (canvas == null)
            return;

        RefreshCards(mode == Mode.Save ? Mode.Save : Mode.Load);
    }

    void RefreshCards(Mode listMode)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            int slot = i + 1;
            SlotCard card = cards[i];

            DestroyCardTexture(i);

            SaveSlotData data = SaveSlots.Read(slot);
            bool filled = data != null;

            if (filled)
            {
                cardTextures[i] = SaveSlots.ReadThumbnail(slot);
                card.thumbnail.texture = cardTextures[i];
                card.thumbnail.enabled = cardTextures[i] != null;

                card.sceneLabel.text = string.IsNullOrEmpty(data.sceneTitle)
                    ? SaveSlots.TitleForScene(data.sceneName)
                    : data.sceneTitle;
                card.dateLabel.text = FormatDate(data.savedAtUtc);
                card.previewLabel.text = FormatPreview(data);
            }
            else
            {
                card.thumbnail.texture = null;
                card.thumbnail.enabled = false;
                card.sceneLabel.text = string.Empty;
                card.dateLabel.text = string.Empty;
                card.previewLabel.text = string.Empty;
            }

            card.thumbFrame.SetActive(filled);
            card.sceneLabel.gameObject.SetActive(filled);
            card.dateLabel.gameObject.SetActive(filled);
            card.previewLabel.gameObject.SetActive(filled);

            card.emptyLabel.gameObject.SetActive(!filled);
            card.emptyLabel.text = listMode == Mode.Save ? "EMPTY SLOT" : "NO DATA";

            card.button.interactable = listMode == Mode.Save || filled;
            card.interactable = card.button.interactable;

            if (!card.interactable)
            {
                card.hovered = false;
                card.pressed = false;
            }
        }
    }

    static string FormatPreview(SaveSlotData data)
    {
        if (data == null || string.IsNullOrEmpty(data.preview))
            return "\u2014";

        if (string.IsNullOrEmpty(data.speaker))
            return data.preview;

        return data.speaker + ": " + data.preview;
    }

    static string FormatDate(string iso)
    {
        DateTime parsed;
        if (!DateTime.TryParse(
                iso,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out parsed))
        {
            return string.Empty;
        }

        return parsed.ToLocalTime().ToString("d MMM yyyy  HH:mm");
    }

    void ClearCardTextures()
    {
        for (int i = 0; i < cardTextures.Length; i++)
            DestroyCardTexture(i);
    }

    void DestroyCardTexture(int index)
    {
        if (index < 0 || index >= cardTextures.Length)
            return;

        Texture2D texture = cardTextures[index];
        cardTextures[index] = null;

        if (texture != null)
            Destroy(texture);

        if (index < cards.Count && cards[index].thumbnail != null)
            cards[index].thumbnail.texture = null;
    }

    // ------------------------------------------------------------------ capture / loading

    public void PrepareForCapture()
    {
        if (canvas != null)
            canvas.enabled = false;

        // The corner MENU plate is part of our interface too, so it stays out of the thumbnail.
        if (pauseHintCanvas != null)
            pauseHintCanvas.enabled = false;
    }

    public void EndCapture()
    {
        if (canvas != null && mode != Mode.Closed)
            canvas.enabled = true;
    }

    public void ShowLoading(string sceneTitle)
    {
        EnsureBuilt();

        loadingTitle.text = string.IsNullOrEmpty(sceneTitle) ? "LOADING" : sceneTitle.ToUpperInvariant();
        loadingProgress.text = "RESTORING";
        SetMode(Mode.Loading);
        loadingPanel.transform.SetAsLastSibling();
    }

    public void SetLoadingProgress(int shown, int total)
    {
        if (loadingProgress == null)
            return;

        loadingProgress.text = total > 0
            ? "RESTORING  " + Mathf.Min(shown, total).ToString("000") + " / " + total.ToString("000")
            : "RESTORING";
    }

    public void HideLoading()
    {
        if (canvas == null)
            return;

        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        mode = Mode.Closed;
        canvas.enabled = false;
        ClearCardTextures();
        StopPauseMusic();
    }

    public void ShowToast(string message)
    {
        if (canvas == null || mode == Mode.Closed || toastText == null)
            return;

        toastText.text = message;
        toastText.color = Ink;
        toastUntil = Time.unscaledTime + 1.9f;
        toastText.transform.SetAsLastSibling();
    }

    // ------------------------------------------------------------------ confirm

    void ShowConfirm(string message, string confirmLabel, Action onConfirm)
    {
        EnsureBuilt();

        if (confirmPanel == null)
            return;

        confirmMessage.text = message;
        confirmYesLabel.text = confirmLabel;
        confirmAction = onConfirm;

        confirmPanel.SetActive(true);
        confirmPanel.transform.SetAsLastSibling();
    }

    void HideConfirm()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        confirmAction = null;
    }

    void ConfirmPressed()
    {
        Action action = confirmAction;
        HideConfirm();

        if (action != null)
            action();
    }

    void Update()
    {
        UpdateToast();
        UpdatePauseHint();
        UpdateEnter();
        UpdatePauseMusic(Time.unscaledDeltaTime);
        TickPlates();
        TickCards();
    }

    // ------------------------------------------------------------------ motion

    /// <summary>
    /// The Persona snap: the poster pops in and the twelve boxes follow it in a fast stagger.
    /// Every value runs on unscaled time, because the game may be paused.
    /// </summary>
    void BeginEnter()
    {
        enterStart = Time.unscaledTime;

        if (windowGroup != null)
            windowGroup.alpha = 0f;

        for (int i = 0; i < cards.Count; i++)
            cards[i].group.alpha = 0f;
    }

    void UpdateEnter()
    {
        if (enterStart < 0f)
            return;

        float elapsed = Time.unscaledTime - enterStart;
        float windowProgress = Mathf.Clamp01(elapsed / 0.16f);
        float windowEased = 1f - Mathf.Pow(1f - windowProgress, 3f);

        if (windowGroup != null)
            windowGroup.alpha = windowEased;

        if (windowRect != null)
            windowRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.982f, 1f, windowEased);

        bool done = windowProgress >= 1f;

        for (int i = 0; i < cards.Count; i++)
        {
            float progress = Mathf.Clamp01((elapsed - i * 0.022f) / 0.20f);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            cards[i].group.alpha = eased;

            if (progress < 1f)
                done = false;
        }

        if (done)
            enterStart = -1f;
    }

    void TickPlates()
    {
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < plates.Count; i++)
        {
            PlateFx fx = plates[i];
            if (fx == null)
                continue;

            float hoverTarget = fx.interactable && fx.hovered ? 1f : 0f;
            float pressTarget = fx.interactable && fx.pressed ? 1f : 0f;

            fx.hover = Mathf.MoveTowards(fx.hover, hoverTarget, deltaTime * HoverSpeed);
            fx.press = Mathf.MoveTowards(fx.press, pressTarget, deltaTime * PressSpeed);

            if (fx.rect != null)
            {
                fx.rect.anchoredPosition = fx.home + new Vector2(fx.slide * fx.hover, 0f);
                fx.rect.localScale = Vector3.one * (1f - (1f - fx.pressScale) * fx.press);
            }

            if (fx.edge != null)
                fx.edge.color = Color.Lerp(fx.edgeIdle, fx.edgeHover, fx.hover);

            if (fx.sheen != null)
                fx.sheen.color = new Color(1f, 1f, 1f, 0.07f * fx.hover + 0.02f * fx.press);

            if (fx.fillTint != null)
                fx.fillTint.color = Color.Lerp(fx.fillIdle, fx.fillHover, fx.hover);

            if (fx.label != null)
                fx.label.color = Color.Lerp(fx.labelIdle, fx.labelHover, fx.hover);
        }
    }

    void TickCards()
    {
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < cards.Count; i++)
        {
            SlotCard card = cards[i];

            float hoverTarget = card.interactable && card.hovered ? 1f : 0f;
            float pressTarget = card.interactable && card.pressed ? 1f : 0f;

            card.hover = Mathf.MoveTowards(card.hover, hoverTarget, deltaTime * HoverSpeed);
            card.press = Mathf.MoveTowards(card.press, pressTarget, deltaTime * PressSpeed);

            Color edge = card.interactable
                ? Color.Lerp(CardEdgeIdle, CardEdgeHover, card.hover)
                : CardEdgeDisabled;
            card.edge.color = edge;

            card.sheen.color = new Color(1f, 1f, 1f, 0.06f * card.hover);

            Color accent = Steel;
            accent.a = 0.85f * card.hover;
            card.accent.color = accent;

            card.number.color = card.interactable
                ? Color.Lerp(NumberIdle, Steel, card.hover)
                : NumberDisabled;

            card.rect.localScale = Vector3.one * (1f - 0.008f * card.press);
        }
    }

    void UpdateToast()
    {
        if (toastText == null)
            return;

        float remaining = toastUntil - Time.unscaledTime;
        Color color = Ink;
        color.a = Mathf.Clamp01(remaining / 0.4f);

        if (toastText.color != color)
            toastText.color = color;
    }

    /// <summary>
    /// The small MENU plate is only offered while a chapter is being played and no screen of
    /// ours is open, so it never sits on top of the title menu or of the pause menu itself.
    /// </summary>
    void UpdatePauseHint()
    {
        if (pauseHintCanvas == null)
            return;

        bool show = mode == Mode.Closed
            && SaveGameManager.Instance != null
            && SaveGameManager.Instance.CanPause;

        if (pauseHintCanvas.enabled != show)
            pauseHintCanvas.enabled = show;
    }

    // ------------------------------------------------------------------ construction

    void BuildPauseHint()
    {
        GameObject canvasObject = new GameObject(
            "PauseHintCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        pauseHintCanvas = canvasObject.GetComponent<Canvas>();
        pauseHintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        pauseHintCanvas.sortingOrder = 4000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateRect("Root", canvasObject.transform);
        Stretch(root, 0f, 0f, 0f, 0f);

        // A small tab in the left corner, with real breathing room from the screen edge so it
        // does not crowd the frame or look pasted onto it.
        RectTransform rect = CreateRect("PauseButton", root);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(150f, 50f);
        rect.anchoredPosition = new Vector2(34f, -34f);

        Button button = CreateDarkPlate(
            rect,
            "MENU",
            () =>
            {
                if (SaveGameManager.Instance != null)
                    SaveGameManager.Instance.PauseGame();
            },
            13f,
            Muted,
            0f);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.characterSpacing = 5f;
            label.alignment = TextAlignmentOptions.Center;

            // The slab leans right, so its visual centre sits half the slant to the right of
            // the rect. Nudging the label the same amount keeps it centred in the plate.
            RectTransform labelRect = label.rectTransform;
            labelRect.offsetMin = new Vector2(PlateSlant * 0.5f, 0f);
            labelRect.offsetMax = new Vector2(PlateSlant * 0.5f, 0f);
        }

        pauseHintCanvas.enabled = false;
    }

    void EnsureBuilt()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "SaveMenuCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateRect("Root", canvasObject.transform);
        Stretch(root, 0f, 0f, 0f, 0f);

        RectTransform backdrop = CreateRect("Backdrop", root);
        Stretch(backdrop, 0f, 0f, 0f, 0f);
        AddImage(backdrop, BackdropColor, true);

        BuildWindow(root);
        BuildConfirm(root);
        BuildLoading(root);
        BuildToast(root);

        canvas.enabled = false;
    }

    void BuildWindow(Transform root)
    {
        windowRect = CreateRect("Window", root);
        SetCentered(windowRect, new Vector2(WindowWidth, WindowHeight), Vector2.zero);

        windowGroup = windowRect.gameObject.AddComponent<CanvasGroup>();

        // Slate shadow, a white cut edge, then the black poster face: three slanted slabs.
        CreateSkew(
            windowRect,
            "Shadow",
            new Vector2(WindowWidth + 18f, WindowHeight + 18f),
            new Vector2(14f, -14f),
            WindowShadow,
            WindowSlant);

        CreateSkew(
            windowRect,
            "Edge",
            new Vector2(WindowWidth + 6f, WindowHeight + 6f),
            Vector2.zero,
            WindowEdge,
            WindowSlant);

        SkewedPanel face = CreateSkew(
            windowRect,
            "Face",
            new Vector2(WindowWidth, WindowHeight),
            Vector2.zero,
            WindowFace,
            WindowSlant);
        face.raycastTarget = true;

        RectTransform titleRect = CreateRect("Title", windowRect);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.sizeDelta = new Vector2(1000f, 92f);
        titleRect.anchoredPosition = new Vector2(64f, -30f);

        titleText = AddText(titleRect, "PAUSED", 76f, Ink, TextAlignmentOptions.TopLeft, BoldFont, FontStyles.Bold);
        titleText.characterSpacing = -1f;
        titleRect.localRotation = Quaternion.Euler(0f, 0f, 2.2f);

        RectTransform tagRect = CreateRect("HeaderTag", windowRect);
        tagRect.anchorMin = new Vector2(0f, 1f);
        tagRect.anchorMax = new Vector2(0f, 1f);
        tagRect.pivot = new Vector2(0f, 1f);
        tagRect.sizeDelta = new Vector2(330f, 42f);
        tagRect.anchoredPosition = new Vector2(66f, -138f);

        SkewedPanel tag = tagRect.gameObject.AddComponent<SkewedPanel>();
        tag.Slant = 18f;
        tag.color = TagFace;
        tag.raycastTarget = false;

        RectTransform tagLabelRect = CreateRect("Label", tagRect);
        Stretch(tagLabelRect, 18f, 0f, 0f, 0f);

        TMP_Text tagLabel = AddText(tagLabelRect, "SELECT A SLOT", 15f, TagInk, TextAlignmentOptions.Center, BoldFont, FontStyles.Bold);
        tagLabel.characterSpacing = 6f;

        RectTransform hintRect = CreateRect("Hint", windowRect);
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(1f, 1f);
        hintRect.sizeDelta = new Vector2(700f, 24f);
        hintRect.anchoredPosition = new Vector2(-64f, -42f);

        subtitleText = AddText(hintRect, "ESC TO RESUME", 12.5f, Faint, TextAlignmentOptions.TopRight, RegularFont, FontStyles.Normal);
        subtitleText.characterSpacing = 6f;

        RectTransform ruleRect = CreateRect("AccentRule", windowRect);
        ruleRect.anchorMin = new Vector2(0.5f, 1f);
        ruleRect.anchorMax = new Vector2(0.5f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.sizeDelta = new Vector2(WindowWidth - 128f, 3f);
        ruleRect.anchoredPosition = new Vector2(0f, -192f);

        SkewedPanel rule = ruleRect.gameObject.AddComponent<SkewedPanel>();
        rule.Slant = 30f;
        rule.color = new Color(1f, 1f, 1f, 0.14f);
        rule.raycastTarget = false;

        RectTransform body = CreateRect("Body", windowRect);
        Stretch(body, 64f, 86f, 64f, 212f);

        BuildPausePanel(body);
        BuildSlotsPanel(body);
        BuildFooter(windowRect);
    }

    void BuildPausePanel(RectTransform body)
    {
        RectTransform panel = CreateRect("PausePanel", body);
        Stretch(panel, 0f, 0f, 0f, 0f);
        pausePanel = panel.gameObject;

        MakeMenuPlate(panel, "ResumeButton", "01", "RESUME", new Vector2(0f, 156f), new Vector2(640f, 84f), ResumePressed);
        MakeMenuPlate(panel, "SaveButton", "02", "SAVE GAME", new Vector2(0f, 52f), new Vector2(640f, 84f), OpenSave);
        MakeMenuPlate(panel, "LoadButton", "03", "LOAD GAME", new Vector2(0f, -52f), new Vector2(640f, 84f), OpenPauseLoad);
        MakeMenuPlate(panel, "MenuButton", "04", "RETURN TO MAIN MENU", new Vector2(0f, -156f), new Vector2(640f, 84f), ReturnToMenuPressed);
    }

    void BuildSlotsPanel(RectTransform body)
    {
        RectTransform panel = CreateRect("SlotsPanel", body);
        Stretch(panel, 0f, 0f, 0f, 0f);
        slotsPanel = panel.gameObject;

        RectTransform gridRect = CreateRect("SlotGrid", panel);
        Stretch(gridRect, 0f, 0f, 0f, 0f);

        GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(24f, 10f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = SlotColumns;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < SaveSlots.SlotCount; i++)
            cards.Add(BuildCard(gridRect, i + 1));
    }

    void BuildFooter(RectTransform parent)
    {
        RectTransform footer = CreateRect("Footer", parent);
        footer.anchorMin = new Vector2(0f, 0f);
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(-128f, 86f);
        footer.anchoredPosition = Vector2.zero;

        RectTransform hintRect = CreateRect("FooterHint", footer);
        hintRect.anchorMin = new Vector2(0f, 0.5f);
        hintRect.anchorMax = new Vector2(1f, 0.5f);
        hintRect.pivot = new Vector2(0f, 0.5f);
        hintRect.offsetMin = new Vector2(8f, -14f);
        hintRect.offsetMax = new Vector2(-250f, 14f);

        footerHint = AddText(hintRect, string.Empty, 15.5f, Faint, TextAlignmentOptions.Left, RegularFont, FontStyles.Normal);

        RectTransform backRect = CreateRect("BackButton", footer);
        backRect.anchorMin = new Vector2(1f, 0.5f);
        backRect.anchorMax = new Vector2(1f, 0.5f);
        backRect.pivot = new Vector2(1f, 0.5f);
        backRect.sizeDelta = new Vector2(212f, 54f);
        backRect.anchoredPosition = new Vector2(-8f, 0f);

        backButton = CreateInvertedPlate(backRect, "BACK", BackPressed, 17f);
    }

    SlotCard BuildCard(Transform parent, int slot)
    {
        var card = new SlotCard();

        RectTransform rect = CreateRect("Slot" + slot.ToString("00"), parent);
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        card.rect = rect;
        card.group = rect.gameObject.AddComponent<CanvasGroup>();

        card.edge = CreateSkew(
            rect,
            "Edge",
            new Vector2(CardWidth + 6f, CardHeight + 6f),
            Vector2.zero,
            CardEdgeIdle,
            CardSlant);

        SkewedPanel face = CreateSkew(
            rect,
            "Face",
            new Vector2(CardWidth, CardHeight),
            Vector2.zero,
            CardFace,
            CardSlant);
        face.raycastTarget = true;

        card.sheen = CreateSkew(
            rect,
            "Sheen",
            new Vector2(CardWidth, CardHeight),
            Vector2.zero,
            new Color(1f, 1f, 1f, 0f),
            CardSlant);

        card.accent = CreateSkew(
            rect,
            "Accent",
            new Vector2(5f, 112f),
            new Vector2(20f, 0f),
            new Color(Steel.r, Steel.g, Steel.b, 0f),
            CardSlant);

        card.button = rect.gameObject.AddComponent<Button>();
        card.button.targetGraphic = face;
        card.button.transition = Selectable.Transition.None;

        int capturedSlot = slot;
        card.button.onClick.AddListener(() => OnSlotClicked(capturedSlot));
        HookCard(card.button, card);

        // The big slot number is the loudest thing on the box, the way the Persona menus
        // number their entries.
        RectTransform numberRect = CreateRect("Number", rect);
        numberRect.anchorMin = new Vector2(0f, 0.5f);
        numberRect.anchorMax = new Vector2(0f, 0.5f);
        numberRect.pivot = new Vector2(0f, 0.5f);
        numberRect.sizeDelta = new Vector2(72f, 58f);
        numberRect.anchoredPosition = new Vector2(30f, 6f);
        card.number = AddText(numberRect, slot.ToString("00"), 46f, NumberIdle, TextAlignmentOptions.Center, BoldFont, FontStyles.Bold);

        RectTransform miniRect = CreateRect("SlotMini", rect);
        miniRect.anchorMin = new Vector2(0f, 1f);
        miniRect.anchorMax = new Vector2(0f, 1f);
        miniRect.pivot = new Vector2(0f, 1f);
        miniRect.sizeDelta = new Vector2(72f, 14f);
        miniRect.anchoredPosition = new Vector2(30f, -14f);
        card.mini = AddText(miniRect, "SLOT", 9f, Ghost, TextAlignmentOptions.Center, RegularFont, FontStyles.Normal);
        card.mini.characterSpacing = 6f;

        // The screenshot is pinned like a photo in a scrapbook: a white print, tilted a hair.
        RectTransform frameRect = CreateRect("ThumbFrame", rect);
        frameRect.anchorMin = new Vector2(0f, 0.5f);
        frameRect.anchorMax = new Vector2(0f, 0.5f);
        frameRect.pivot = new Vector2(0f, 0.5f);
        frameRect.sizeDelta = new Vector2(ThumbWidth + 6f, ThumbHeight + 6f);
        frameRect.anchoredPosition = new Vector2(300f, 0f);
        frameRect.localRotation = Quaternion.Euler(0f, 0f, -2f);
        card.thumbFrame = frameRect.gameObject;

        Image print = AddImage(frameRect, FrameFace, false);
        print.raycastTarget = false;

        RectTransform thumbRect = CreateRect("Thumbnail", frameRect);
        Stretch(thumbRect, 3f, 3f, 3f, 3f);

        card.thumbnail = thumbRect.gameObject.AddComponent<RawImage>();
        card.thumbnail.raycastTarget = false;
        card.thumbnail.color = Color.white;
        card.thumbnail.enabled = false;
        card.thumbnail.texture = null;

        RectTransform sceneRect = CreateRect("SceneLabel", rect);
        sceneRect.anchorMin = new Vector2(0f, 1f);
        sceneRect.anchorMax = new Vector2(0f, 1f);
        sceneRect.pivot = new Vector2(0f, 1f);
        sceneRect.sizeDelta = new Vector2(178f, 26f);
        sceneRect.anchoredPosition = new Vector2(114f, -26f);
        card.sceneLabel = AddText(sceneRect, string.Empty, 18f, Ink, TextAlignmentOptions.TopLeft, BoldFont, FontStyles.Bold);

        RectTransform dateRect = CreateRect("DateLabel", rect);
        dateRect.anchorMin = new Vector2(0f, 1f);
        dateRect.anchorMax = new Vector2(0f, 1f);
        dateRect.pivot = new Vector2(0f, 1f);
        dateRect.sizeDelta = new Vector2(178f, 16f);
        dateRect.anchoredPosition = new Vector2(114f, -54f);
        card.dateLabel = AddText(dateRect, string.Empty, 11f, Faint, TextAlignmentOptions.TopLeft, RegularFont, FontStyles.Normal);

        RectTransform previewRect = CreateRect("PreviewLabel", rect);
        previewRect.anchorMin = new Vector2(0f, 0f);
        previewRect.anchorMax = new Vector2(0f, 0f);
        previewRect.pivot = new Vector2(0f, 0f);
        previewRect.sizeDelta = new Vector2(178f, 46f);
        previewRect.anchoredPosition = new Vector2(114f, 12f);
        card.previewLabel = AddText(previewRect, string.Empty, 13f, Body, TextAlignmentOptions.TopLeft, RegularFont, FontStyles.Italic);
        card.previewLabel.textWrappingMode = TextWrappingModes.Normal;
        card.previewLabel.overflowMode = TextOverflowModes.Ellipsis;
        card.previewLabel.lineSpacing = 3f;

        RectTransform emptyRect = CreateRect("EmptyLabel", rect);
        emptyRect.anchorMin = new Vector2(0f, 0.5f);
        emptyRect.anchorMax = new Vector2(0f, 0.5f);
        emptyRect.pivot = new Vector2(0f, 0.5f);
        emptyRect.sizeDelta = new Vector2(178f, 24f);
        emptyRect.anchoredPosition = new Vector2(114f, 0f);
        card.emptyLabel = AddText(emptyRect, "EMPTY SLOT", 14f, Ghost, TextAlignmentOptions.Left, RegularFont, FontStyles.Normal);
        card.emptyLabel.characterSpacing = 5f;

        return card;
    }

    void BuildConfirm(Transform root)
    {
        RectTransform overlay = CreateRect("Confirm", root);
        Stretch(overlay, 0f, 0f, 0f, 0f);
        AddImage(overlay, Overlay, true);
        confirmPanel = overlay.gameObject;

        RectTransform box = CreateRect("ConfirmBox", overlay);
        SetCentered(box, new Vector2(860f, 380f), Vector2.zero);

        CreateSkew(
            box,
            "Edge",
            new Vector2(866f, 386f),
            Vector2.zero,
            WindowEdge,
            WindowSlant);
        CreateSkew(
            box,
            "Face",
            new Vector2(860f, 380f),
            Vector2.zero,
            WindowFace,
            WindowSlant).raycastTarget = true;

        RectTransform tagRect = CreateRect("Tag", box);
        tagRect.anchorMin = new Vector2(0f, 1f);
        tagRect.anchorMax = new Vector2(0f, 1f);
        tagRect.pivot = new Vector2(0f, 1f);
        tagRect.sizeDelta = new Vector2(310f, 40f);
        tagRect.anchoredPosition = new Vector2(56f, -40f);

        SkewedPanel tag = tagRect.gameObject.AddComponent<SkewedPanel>();
        tag.Slant = 16f;
        tag.color = TagFace;
        tag.raycastTarget = false;

        RectTransform tagLabelRect = CreateRect("Label", tagRect);
        Stretch(tagLabelRect, 16f, 0f, 0f, 0f);

        TMP_Text tagLabel = AddText(tagLabelRect, "ARE YOU SURE?", 14f, TagInk, TextAlignmentOptions.Center, BoldFont, FontStyles.Bold);
        tagLabel.characterSpacing = 5f;

        RectTransform message = CreateRect("Message", box);
        message.anchorMin = new Vector2(0f, 1f);
        message.anchorMax = new Vector2(0f, 1f);
        message.pivot = new Vector2(0f, 1f);
        message.sizeDelta = new Vector2(756f, 132f);
        message.anchoredPosition = new Vector2(56f, -104f);

        confirmMessage = AddText(message, string.Empty, 21f, Ink, TextAlignmentOptions.TopLeft, RegularFont, FontStyles.Normal);
        confirmMessage.textWrappingMode = TextWrappingModes.Normal;
        confirmMessage.overflowMode = TextOverflowModes.Overflow;
        confirmMessage.lineSpacing = 6f;

        RectTransform yesRect = CreateRect("ConfirmYes", box);
        SetCentered(yesRect, new Vector2(250f, 56f), new Vector2(-186f, -132f));

        Button yes = CreateInvertedPlate(yesRect, "CONFIRM", ConfirmPressed, 18f);
        confirmYesLabel = yes.GetComponentInChildren<TMP_Text>();

        RectTransform cancelRect = CreateRect("ConfirmCancel", box);
        SetCentered(cancelRect, new Vector2(220f, 54f), new Vector2(140f, -132f));

        CreateDarkPlate(cancelRect, "CANCEL", HideConfirm, 18f, Ink, 0f);

        confirmPanel.SetActive(false);
    }

    void BuildLoading(Transform root)
    {
        RectTransform overlay = CreateRect("Loading", root);
        Stretch(overlay, 0f, 0f, 0f, 0f);
        AddImage(overlay, new Color(0.004f, 0.004f, 0.005f, 1f), true);
        loadingPanel = overlay.gameObject;

        RectTransform bandRect = CreateRect("Band", overlay);
        SetCentered(bandRect, new Vector2(1240f, 120f), new Vector2(0f, 40f));

        SkewedPanel band = bandRect.gameObject.AddComponent<SkewedPanel>();
        band.Slant = 46f;
        band.color = TagFace;
        band.raycastTarget = false;

        RectTransform titleRect = CreateRect("Title", bandRect);
        Stretch(titleRect, 30f, 0f, 0f, 0f);
        loadingTitle = AddText(titleRect, "LOADING", 44f, TagInk, TextAlignmentOptions.Center, BoldFont, FontStyles.Bold);
        loadingTitle.characterSpacing = 7f;

        RectTransform progressRect = CreateRect("Progress", overlay);
        SetCentered(progressRect, new Vector2(1400f, 30f), new Vector2(0f, -58f));
        loadingProgress = AddText(progressRect, "RESTORING", 16f, Ink, TextAlignmentOptions.Center, RegularFont, FontStyles.Normal);
        loadingProgress.characterSpacing = 6f;

        RectTransform noteRect = CreateRect("Note", overlay);
        SetCentered(noteRect, new Vector2(1400f, 26f), new Vector2(0f, -94f));
        AddText(noteRect, "Replaying the scene back to your place.", 14f, Ghost, TextAlignmentOptions.Center, RegularFont, FontStyles.Normal);

        for (int i = 0; i < 3; i++)
        {
            RectTransform line = CreateRect("SpeedLine" + i, overlay);
            SetCentered(
                line,
                new Vector2(360f + i * 90f, 3f),
                new Vector2(i % 2 == 0 ? -620f : 620f, -20f + i * 46f));
            line.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -18f : 18f);

            SkewedPanel bar = line.gameObject.AddComponent<SkewedPanel>();
            bar.Slant = 0f;
            bar.color = new Color(1f, 1f, 1f, 0.10f - i * 0.02f);
            bar.raycastTarget = false;
        }

        loadingPanel.SetActive(false);
    }

    void BuildToast(Transform root)
    {
        RectTransform toastRect = CreateRect("Toast", root);
        toastRect.anchorMin = new Vector2(0.5f, 0f);
        toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.sizeDelta = new Vector2(900f, 36f);
        toastRect.anchoredPosition = new Vector2(0f, 24f);

        toastText = AddText(toastRect, string.Empty, 17f, Ink, TextAlignmentOptions.Center, RegularFont, FontStyles.Normal);
        toastText.characterSpacing = 5f;

        Color clear = Ink;
        clear.a = 0f;
        toastText.color = clear;
    }

    // ------------------------------------------------------------------ pause actions

    void ResumePressed()
    {
        if (SaveGameManager.Instance != null)
            SaveGameManager.Instance.ResumeGame();
    }

    void OpenPauseLoad()
    {
        OpenLoad(true);
    }

    void ReturnToMenuPressed()
    {
        ShowConfirm(
            "Return to the main menu? Anything since your last save will be lost.",
            "MAIN MENU",
            () => SaveGameManager.Instance.ReturnToMainMenu());
    }

    // ------------------------------------------------------------------ ui helpers

    Button MakeMenuPlate(
        Transform parent,
        string name,
        string index,
        string label,
        Vector2 position,
        Vector2 size,
        Action onClick)
    {
        RectTransform rect = CreateRect(name, parent);
        SetCentered(rect, size, position);

        Button button = CreateDarkPlate(rect, label, onClick, 23f, Ink, 8f);

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.alignment = TextAlignmentOptions.Left;

            RectTransform textRect = text.rectTransform;
            textRect.offsetMin = new Vector2(68f, 0f);
            textRect.offsetMax = new Vector2(-24f, 0f);
        }

        RectTransform indexRect = CreateRect("Index", rect);
        indexRect.anchorMin = new Vector2(0f, 0.5f);
        indexRect.anchorMax = new Vector2(0f, 0.5f);
        indexRect.pivot = new Vector2(0f, 0.5f);
        indexRect.sizeDelta = new Vector2(34f, 20f);
        indexRect.anchoredPosition = new Vector2(28f, 0f);

        TMP_Text indexText = AddText(indexRect, index, 13f, SteelDim, TextAlignmentOptions.Left, BoldFont, FontStyles.Bold);
        indexText.characterSpacing = 3f;

        // A thin steel tick between the number and the label, like a ruled index.
        RectTransform tick = CreateRect("Tick", rect);
        tick.anchorMin = new Vector2(0f, 0.5f);
        tick.anchorMax = new Vector2(0f, 0.5f);
        tick.pivot = new Vector2(0f, 0.5f);
        tick.sizeDelta = new Vector2(2f, 26f);
        tick.anchoredPosition = new Vector2(58f, 0f);

        SkewedPanel bar = tick.gameObject.AddComponent<SkewedPanel>();
        bar.Slant = 2f;
        bar.color = new Color(Steel.r, Steel.g, Steel.b, 0.5f);
        bar.raycastTarget = false;

        return button;
    }

    Button CreateDarkPlate(
        RectTransform rect,
        string label,
        Action onClick,
        float fontSize,
        Color labelColor,
        float slide)
    {
        Vector2 size = rect.rect.size;
        if (size.x < 2f || size.y < 2f)
            size = rect.sizeDelta;

        SkewedPanel edge = CreateSkew(
            rect,
            "Edge",
            size + new Vector2(6f, 6f),
            Vector2.zero,
            PlateEdgeIdle,
            PlateSlant);

        SkewedPanel face = CreateSkew(rect, "Face", size, Vector2.zero, PlateFace, PlateSlant);
        face.raycastTarget = true;

        SkewedPanel sheen = CreateSkew(
            rect,
            "Sheen",
            size,
            Vector2.zero,
            new Color(1f, 1f, 1f, 0f),
            PlateSlant);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.transition = Selectable.Transition.None;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        RectTransform labelRect = CreateRect("Label", rect);
        Stretch(labelRect, 0f, 0f, 0f, 0f);

        TMP_Text text = AddText(labelRect, label, fontSize, labelColor, TextAlignmentOptions.Center, BoldFont, FontStyles.Bold);
        text.characterSpacing = 4f;

        var fx = new PlateFx
        {
            rect = rect,
            home = rect.anchoredPosition,
            slide = slide,
            edge = edge,
            sheen = sheen,
            label = text,
            labelIdle = labelColor,
            labelHover = Color.white,
            edgeIdle = PlateEdgeIdle,
            edgeHover = PlateEdgeHover,
            pressScale = 0.985f,
        };

        plates.Add(fx);
        HookPlate(button, fx);

        return button;
    }

    Button CreateInvertedPlate(RectTransform rect, string label, Action onClick, float fontSize)
    {
        // A solid white plate with black type: the inverted button Persona uses for its
        // confirmations, and the loudest thing on the screen.
        Image fill = rect.gameObject.AddComponent<Image>();
        fill.color = InvertedIdle;
        fill.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        button.transition = Selectable.Transition.None;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        RectTransform labelRect = CreateRect("Label", rect);
        Stretch(labelRect, 0f, 0f, 0f, 0f);

        TMP_Text text = AddText(labelRect, label, fontSize, TagInk, TextAlignmentOptions.Center, BoldFont, FontStyles.Bold);
        text.characterSpacing = 4f;

        var fx = new PlateFx
        {
            rect = rect,
            home = rect.anchoredPosition,
            slide = 0f,
            fillTint = fill,
            fillIdle = InvertedIdle,
            fillHover = InvertedHover,
            label = text,
            labelIdle = TagInk,
            labelHover = TagInk,
            edgeIdle = PlateEdgeIdle,
            edgeHover = PlateEdgeHover,
            pressScale = 0.975f,
        };

        plates.Add(fx);
        HookPlate(button, fx);

        return button;
    }

    void HookPlate(Button button, PlateFx fx)
    {
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            if (!button.IsInteractable())
                return;

            fx.hovered = true;
            PlayHoverSound();
        });

        AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            fx.hovered = false;
            fx.pressed = false;
        });

        AddTrigger(trigger, EventTriggerType.PointerDown, () =>
        {
            if (!button.IsInteractable())
                return;

            fx.pressed = true;
            PlayClickSound();
        });

        AddTrigger(trigger, EventTriggerType.PointerUp, () =>
        {
            fx.pressed = false;
        });
    }

    void HookCard(Button button, SlotCard card)
    {
        EventTrigger trigger = button.gameObject.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
        {
            if (!button.IsInteractable())
                return;

            card.hovered = true;
            PlayHoverSound();
        });

        AddTrigger(trigger, EventTriggerType.PointerExit, () =>
        {
            card.hovered = false;
            card.pressed = false;
        });

        AddTrigger(trigger, EventTriggerType.PointerDown, () =>
        {
            if (!button.IsInteractable())
                return;

            card.pressed = true;
            PlayClickSound();
        });

        AddTrigger(trigger, EventTriggerType.PointerUp, () =>
        {
            card.pressed = false;
        });
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    /// <summary>The same soft blip the dialogue plates play, so the menu belongs to the game.</summary>
    void PlayHoverSound()
    {
        if (!hoverClipLoaded)
        {
            hoverClip = Resources.Load<AudioClip>("Audio/hoverbutton");
            hoverClipLoaded = true;
        }

        if (hoverClip == null || Time.unscaledTime - lastHoverSoundTime < 0.055f)
            return;

        if (hoverAudio == null)
        {
            GameObject host = new GameObject("HoverAudio");
            host.transform.SetParent(transform, false);

            hoverAudio = host.AddComponent<AudioSource>();
            hoverAudio.playOnAwake = false;
            hoverAudio.loop = false;
            hoverAudio.spatialBlend = 0f;
            hoverAudio.volume = 0.32f;

            // The pause screen stops the listener, which would otherwise swallow every blip
            // made while the menu is open. The menu's own sources play through that pause.
            hoverAudio.ignoreListenerPause = true;
        }

        lastHoverSoundTime = Time.unscaledTime;
        hoverAudio.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
        hoverAudio.PlayOneShot(hoverClip, 0.32f);
    }

    /// <summary>
    /// The dialogue's own click, played when a box or a plate is pressed. It is kept on its own
    /// source, set to ignore the listener pause, so pressing a button in the paused menu still
    /// answers without unpausing the scene or letting clicks stack.
    /// </summary>
    void PlayClickSound()
    {
        if (!clickClipLoaded)
        {
            clickClip = Resources.Load<AudioClip>("Audio/button_click");
            clickClipLoaded = true;
        }

        if (clickClip == null || Time.unscaledTime - lastClickSoundTime < 0.04f)
            return;

        if (clickAudio == null)
        {
            GameObject host = new GameObject("ClickAudio");
            host.transform.SetParent(transform, false);

            clickAudio = host.AddComponent<AudioSource>();
            clickAudio.playOnAwake = false;
            clickAudio.loop = false;
            clickAudio.spatialBlend = 0f;
            clickAudio.volume = 0.5f;
            clickAudio.ignoreListenerPause = true;
        }

        lastClickSoundTime = Time.unscaledTime;
        clickAudio.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
        clickAudio.PlayOneShot(clickClip, 0.5f);
    }

    /// <summary>
    /// The pause theme. It rides over the paused listener, fades in when a paused game opens
    /// the menu and fades back out when the menu closes; the fade runs on unscaled time so it
    /// keeps moving at timeScale 0.
    /// </summary>
    void StartPauseMusic()
    {
        if (!pauseMusicClipLoaded)
        {
            pauseMusicClip = Resources.Load<AudioClip>("Audio/pausemenu_bgm");
            pauseMusicClipLoaded = true;

            if (pauseMusicClip == null)
                Debug.LogWarning("SaveMenuUI: no pause theme found at Resources/Audio/pausemenu_bgm.");
        }

        if (pauseMusicClip == null)
            return;

        if (pauseMusicAudio == null)
        {
            GameObject host = new GameObject("PauseMusic");
            host.transform.SetParent(transform, false);

            pauseMusicAudio = host.AddComponent<AudioSource>();
            pauseMusicAudio.playOnAwake = false;
            pauseMusicAudio.loop = true;
            pauseMusicAudio.spatialBlend = 0f;
            pauseMusicAudio.ignoreListenerPause = true;
            pauseMusicAudio.clip = pauseMusicClip;
            pauseMusicAudio.volume = 0f;
        }

        if (!pauseMusicAudio.isPlaying)
        {
            pauseMusicAudio.volume = pauseMusicVolume;
            pauseMusicAudio.Play();
        }

        pauseMusicTarget = PauseMusicLevel;
    }

    void StopPauseMusic()
    {
        pauseMusicTarget = 0f;
    }

    void UpdatePauseMusic(float deltaTime)
    {
        if (pauseMusicAudio == null)
            return;

        float fadeTime = pauseMusicTarget > pauseMusicVolume
            ? PauseMusicFadeIn
            : PauseMusicFadeOut;
        float speed = PauseMusicLevel / Mathf.Max(0.01f, fadeTime);

        pauseMusicVolume = Mathf.MoveTowards(pauseMusicVolume, pauseMusicTarget, speed * deltaTime);
        pauseMusicAudio.volume = pauseMusicVolume;

        if (pauseMusicTarget <= 0f && pauseMusicVolume <= 0.001f && pauseMusicAudio.isPlaying)
            pauseMusicAudio.Stop();
    }

    static SkewedPanel CreateSkew(
        Transform parent,
        string name,
        Vector2 size,
        Vector2 position,
        Color color,
        float slant)
    {
        RectTransform rect = CreateRect(name, parent);
        SetCentered(rect, size, position);

        SkewedPanel panel = rect.gameObject.AddComponent<SkewedPanel>();
        panel.Slant = slant;
        panel.color = color;
        panel.raycastTarget = false;
        return panel;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    static Image AddImage(RectTransform rect, Color color, bool raycast)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    static TextMeshProUGUI AddText(
        RectTransform rect,
        string content,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment,
        TMP_FontAsset font,
        FontStyles style)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
            text.font = font;

        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void SetCentered(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
