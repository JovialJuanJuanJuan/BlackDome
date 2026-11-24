using System;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
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
        Delete,
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
    const float OpenFade = 0.16f;
    const float CloseFade = 0.20f;
    const float ConfirmFade = 0.14f;
    const float PauseHintFade = 0.6f;
    const float PauseHintReturn = 0.3f;
    const float PauseHintHide = 0.22f;
    const float ConfirmGuardTime = 0.25f;

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
    // Hover ink stays inside the monochrome palette: a hovered box lifts its number and its
    // accent bar from grey to a lighter grey, never to a colour.
    static readonly Color HoverGrey = new Color(0.86f, 0.86f, 0.87f, 1f);
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
        public Button deleteTab;
        public int slot;
        public bool hasSave;
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
        public Button button;
        public Vector2 home;
        public float slide;
        public SkewedPanel edge;
        public SkewedPanel sheen;
        public Graphic fillTint;
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
        get { return mode != Mode.Closed || closing; }
    }

    Canvas canvas;
    Canvas pauseHintCanvas;
    CanvasGroup pauseHintGroup;
    bool pauseHintRevealed;
    float pauseHintReveal;
    RectTransform windowRect;
    CanvasGroup rootGroup;
    bool closing;
    float closeElapsed;
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
    CanvasGroup confirmGroup;
    RectTransform confirmBox;

    Canvas screenFadeCanvas;
    CanvasGroup screenFadeGroup;
    float screenFadeAlpha;
    float screenFadeTarget;
    float screenFadeSpeed;
    float confirmFade;
    bool confirmOpen;
    bool confirmHidesCanvas;

    Button backButton;
    TMP_Text backLabel;
    Button deleteButton;
    Button continueButton;
    Button pauseResumeButton;
    Button confirmCancelButton;
    TMP_Text pauseHintLabel;
    float confirmOpenedAt;
    Mode deleteReturnMode = Mode.Load;

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
    bool quitPrompt;
    bool quitAfterFade;

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

    /// <summary>
    /// Asks before the game closes. The title menu's Quit button calls this instead of quitting
    /// outright; the question is the same confirmation card the save screens use.
    /// </summary>
    public static void ConfirmQuit()
    {
        SaveGameManager.EnsureExists();

        if (Instance != null)
            Instance.OpenQuitPrompt();
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
        if (closing)
            return;

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
            case Mode.Delete:
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

        // A second close while the first is still fading must not snap the screen back to full
        // and run the fade again.
        if (closing && next == Mode.Closed)
            return;

        Mode previous = mode;
        mode = next;

        closing = false;
        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
        }

        if (next == Mode.Closed)
        {
            StopPauseMusic();
            BeginClose();
            return;
        }

        canvas.enabled = true;

        bool slots = next == Mode.Save || next == Mode.Load || next == Mode.Delete;

        windowRect.gameObject.SetActive(next == Mode.Pause || slots);
        pausePanel.SetActive(next == Mode.Pause);
        slotsPanel.SetActive(slots);
        loadingPanel.SetActive(next == Mode.Loading);

        if (next != Mode.Loading)
            HideConfirm();

        if (backButton != null)
            backButton.gameObject.SetActive(slots);

        // In delete mode the plate says what it does: it leaves the delete list.
        if (backLabel != null)
            backLabel.text = next == Mode.Delete ? "EXIT DELETE" : "BACK";

        // The delete plate belongs to the two lists, never to delete mode itself, and only shows
        // while there is a save that could be removed.
        RefreshDeleteButton();
        RefreshContinueButton();

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
        else if (next == Mode.Delete)
        {
            titleText.text = "DELETE SAVE";
            subtitleText.text = "ESC TO STEP BACK";
            footerHint.text = "Choose a box to delete its save. This cannot be undone.";
        }

        // The pause theme belongs to the pause flow, never to the title menu's load screen.
        if (next == Mode.Loading)
            StopPauseMusic();
        else if (openedFromPause)
            StartPauseMusic();

        if (previous != next && next != Mode.Loading)
            BeginEnter();

        if (next != Mode.Loading)
            SelectDefaultFocus();
    }

    void BackPressed()
    {
        if (confirmPanel != null && confirmPanel.activeSelf)
        {
            HideConfirm();
            return;
        }

        if (mode == Mode.Delete)
        {
            // Delete mode leaves the boxes in their delete state (empty ones disabled and
            // labelled NO DATA), so the list it returns to is rebuilt instead of inherited:
            // an empty save box has to be selectable again.
            Mode back = deleteReturnMode;
            RefreshCards(back);
            SetMode(back);
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
        if (mode == Mode.Delete)
        {
            ShowDeleteConfirm(slot);
            return;
        }

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

        RefreshCards(mode == Mode.Save || mode == Mode.Load ? mode : Mode.Delete);
        RefreshDeleteButton();
        RefreshContinueButton();
    }

    /// <summary>
    /// A list with nothing saved in it has nothing to delete, so the plate stays away until a
    /// save exists. Refreshed whenever a list is shown or a save is written.
    /// </summary>
    void RefreshDeleteButton()
    {
        if (deleteButton == null)
            return;

        bool listScreen = mode == Mode.Save || mode == Mode.Load;
        deleteButton.gameObject.SetActive(listScreen && AnySaveExists());
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
            bool damaged = !filled && SaveSlots.HasSave(slot);

            card.hasSave = filled || damaged;

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
            card.emptyLabel.text = damaged
                ? "SAVE DAMAGED"
                : (listMode == Mode.Save ? "EMPTY SLOT" : "NO DATA");

            // A damaged file cannot be loaded, but it can still be overwritten or deleted, which
            // are the two ways to get rid of it.
            card.button.interactable = listMode == Mode.Save
                || filled
                || (damaged && listMode == Mode.Delete);
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
        closing = false;
        canvas.enabled = false;
        ClearCardTextures();
        StopPauseMusic();
    }

    // ------------------------------------------------------------------ screen fade

    /// <summary>
    /// Fades the whole screen to black. It lives on the manager rather than on a menu canvas, so it
    /// outlives the scene it covers and can bring the next scene in from black.
    /// </summary>
    public void FadeScreenOut(float duration)
    {
        EnsureBuilt();
        BeginScreenFade(1f, duration);
    }

    /// <summary>Lifts the black back off the screen.</summary>
    public void FadeScreenIn(float duration)
    {
        EnsureBuilt();
        BeginScreenFade(0f, duration);
    }

    /// <summary>True while the screen is between black and clear.</summary>
    public bool IsScreenFading
    {
        get { return !Mathf.Approximately(screenFadeAlpha, screenFadeTarget); }
    }

    void BeginScreenFade(float target, float duration)
    {
        if (screenFadeGroup == null)
            return;

        screenFadeTarget = Mathf.Clamp01(target);
        screenFadeSpeed = duration <= 0f ? 0f : 1f / duration;

        if (duration <= 0f)
            screenFadeAlpha = screenFadeTarget;

        ApplyScreenFade();
    }

    /// <summary>Runs on unscaled time, so a paused game still fades.</summary>
    void UpdateScreenFade()
    {
        if (screenFadeGroup == null || screenFadeSpeed <= 0f)
            return;

        if (Mathf.Approximately(screenFadeAlpha, screenFadeTarget))
            return;

        screenFadeAlpha = Mathf.MoveTowards(
            screenFadeAlpha,
            screenFadeTarget,
            Time.unscaledDeltaTime * screenFadeSpeed);

        ApplyScreenFade();
    }

    void ApplyScreenFade()
    {
        if (screenFadeGroup == null)
            return;

        screenFadeGroup.alpha = screenFadeAlpha;
        screenFadeGroup.blocksRaycasts = screenFadeAlpha > 0.001f;

        // The canvas stays on for a fade in even at alpha 0, so its first steps are drawn.
        bool visible = screenFadeAlpha > 0.001f || screenFadeTarget > 0f;

        if (screenFadeCanvas != null && screenFadeCanvas.enabled != visible)
            screenFadeCanvas.enabled = visible;
    }

    void BuildScreenFade()
    {
        GameObject canvasObject = new GameObject(
            "ScreenFadeCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        screenFadeCanvas = canvasObject.GetComponent<Canvas>();
        screenFadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above the menu and its loading screen, so a fade covers everything either of them shows.
        screenFadeCanvas.sortingOrder = 6000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateRect("Root", canvasObject.transform);
        Stretch(root, 0f, 0f, 0f, 0f);
        AddImage(root, Color.black, true);

        screenFadeGroup = root.gameObject.AddComponent<CanvasGroup>();
        screenFadeGroup.alpha = 0f;
        screenFadeGroup.blocksRaycasts = false;
        screenFadeGroup.interactable = false;

        screenFadeCanvas.enabled = false;
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

    /// <summary>
    /// Puts the quit question over the title menu. The menu proper stays closed: only the prompt
    /// and its dark backdrop are switched on, and the plates behind it are not reachable.
    /// </summary>
    void OpenQuitPrompt()
    {
        EnsureBuilt();

        quitPrompt = true;
        closing = false;
        enterStart = -1f;
        mode = Mode.Closed;

        canvas.enabled = true;

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
        }

        windowRect.gameObject.SetActive(false);
        pausePanel.SetActive(false);
        slotsPanel.SetActive(false);
        loadingPanel.SetActive(false);

        if (backButton != null)
            backButton.gameObject.SetActive(false);

        if (deleteButton != null)
            deleteButton.gameObject.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        ShowConfirm("Quit Black Dome? The game will close.", "QUIT", QuitNow);
    }

    void QuitNow()
    {
        // The pause theme leaves with the screen; the scene's own music is faded by the manager
        // during the black.
        StopPauseMusic();

        // The game waits for the card's fade-out, so quitting ends on the same motion every other
        // answer gets instead of cutting on the click.
        quitAfterFade = true;
    }

    void ShowConfirm(string message, string confirmLabel, Action onConfirm)
    {
        EnsureBuilt();

        if (confirmPanel == null)
            return;

        confirmMessage.text = message;
        confirmYesLabel.text = confirmLabel;
        confirmAction = onConfirm;

        // A fresh card starts transparent and a hair small, then eases to full like the menu's
        // own window. Reopening it mid fade-out simply turns the fade around.
        if (!confirmPanel.activeSelf)
        {
            confirmFade = 0f;

            if (confirmGroup != null)
                confirmGroup.alpha = 0f;

            if (confirmBox != null)
                confirmBox.localScale = Vector3.one * 0.985f;
        }

        confirmOpen = true;
        confirmOpenedAt = Time.unscaledTime;

        if (confirmGroup != null)
            confirmGroup.blocksRaycasts = true;

        confirmPanel.SetActive(true);
        confirmPanel.transform.SetAsLastSibling();

        // The safe answer holds the keyboard and gamepad focus; the destructive plate has to be
        // navigated to on purpose.
        Select(confirmCancelButton != null ? confirmCancelButton.gameObject : null);
    }

    /// <summary>
    /// Fades the confirmation card in and out on unscaled time. The card's word is dropped from
    /// the action immediately when it starts closing, so a second click cannot fire it twice.
    /// </summary>
    void UpdateConfirm()
    {
        if (confirmPanel == null || !confirmPanel.activeSelf)
            return;

        if (confirmOpen)
        {
            confirmFade = Mathf.MoveTowards(confirmFade, 1f, Time.unscaledDeltaTime / ConfirmFade);
            float eased = 1f - Mathf.Pow(1f - confirmFade, 3f);

            if (confirmGroup != null)
                confirmGroup.alpha = eased;

            if (confirmBox != null)
                confirmBox.localScale = Vector3.one * Mathf.LerpUnclamped(0.985f, 1f, eased);

            return;
        }

        confirmFade = Mathf.MoveTowards(confirmFade, 0f, Time.unscaledDeltaTime / ConfirmFade);

        if (confirmGroup != null)
            confirmGroup.alpha = confirmFade;

        if (confirmFade > 0f)
            return;

        confirmPanel.SetActive(false);
        SelectDefaultFocus();

        // The quit question is the one confirmation with no screen behind it, so the overlay that
        // carried it is put away once it has finished fading.
        if (confirmHidesCanvas)
        {
            confirmHidesCanvas = false;

            if (canvas != null && mode == Mode.Closed)
                canvas.enabled = false;
        }

        if (quitAfterFade)
        {
            quitAfterFade = false;

            // The manager takes it from here: it fades the screen and the music out before the game
            // closes, so the quit does not cut on the confirm card's last frame.
            if (SaveGameManager.Instance != null)
                SaveGameManager.Instance.QuitGame();
            else
                Application.Quit();
        }
    }

    void HideConfirm()
    {
        // The card fades out under its own steam, so it is only switched off once that has played.
        if (confirmPanel != null && confirmPanel.activeSelf)
        {
            confirmOpen = false;

            if (confirmGroup != null)
                confirmGroup.blocksRaycasts = false;

            if (quitPrompt)
            {
                quitPrompt = false;
                confirmHidesCanvas = true;
            }
        }
        else if (quitPrompt)
        {
            quitPrompt = false;

            if (canvas != null && mode == Mode.Closed)
                canvas.enabled = false;
        }

        confirmAction = null;
    }

    void ConfirmPressed()
    {
        // A click in the card's first quarter second cannot be an answer to a question the
        // player has not read yet, so a double click cannot delete a save or quit the game.
        if (Time.unscaledTime - confirmOpenedAt < ConfirmGuardTime)
            return;

        Action action = confirmAction;
        HideConfirm();

        if (action != null)
            action();
    }

    void Update()
    {
        UpdateScreenFade();
        UpdateToast();
        UpdateConfirm();
        UpdatePauseHint();
        UpdateEnter();
        UpdateClose();
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
        closing = false;

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
        }

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

        if (rootGroup != null)
            rootGroup.alpha = windowEased;

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

    /// <summary>
    /// Closing is the entrance run backwards: the whole canvas, backdrop included, fades to
    /// nothing before it is switched off, so the menu never pops out of existence.
    /// </summary>
    void BeginClose()
    {
        if (canvas == null || closing)
            return;

        closing = true;
        closeElapsed = 0f;

        // A close can cut an entrance short, so the entrance is stopped before it keeps writing
        // the window's alpha under the fade.
        enterStart = -1f;

        if (rootGroup != null)
            rootGroup.blocksRaycasts = false;
    }

    void UpdateClose()
    {
        if (!closing || canvas == null)
            return;

        closeElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(closeElapsed / CloseFade);

        if (rootGroup != null)
            rootGroup.alpha = 1f - progress;

        if (progress < 1f)
            return;

        closing = false;
        canvas.enabled = false;
        ClearCardTextures();
        HideConfirm();

        if (rootGroup != null)
        {
            rootGroup.alpha = 1f;
            rootGroup.blocksRaycasts = true;
        }
    }

    void TickPlates()
    {
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < plates.Count; i++)
        {
            PlateFx fx = plates[i];
            if (fx == null)
                continue;

            bool selected = fx.button != null && IsSelected(fx.button.gameObject);
            float hoverTarget = fx.interactable && (fx.hovered || selected) ? 1f : 0f;
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
        bool deleting = mode == Mode.Delete;

        for (int i = 0; i < cards.Count; i++)
        {
            SlotCard card = cards[i];

            // Selection is what the keyboard and gamepad move; it lights a box exactly the way
            // the pointer does, so both ways of driving the menu look the same.
            bool selected = card.button != null && IsSelected(card.button.gameObject);
            float hoverTarget = card.interactable && (card.hovered || selected) ? 1f : 0f;
            float pressTarget = card.interactable && card.pressed ? 1f : 0f;

            card.hover = Mathf.MoveTowards(card.hover, hoverTarget, deltaTime * HoverSpeed);
            card.press = Mathf.MoveTowards(card.press, pressTarget, deltaTime * PressSpeed);

            // In delete mode every filled box breathes a little, one after another down the grid,
            // so the boxes that can be removed read as live targets before the pointer reaches
            // them. Hover still takes the box all the way to its bright edge.
            float breathe = deleting && card.interactable
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f - i * 0.35f)
                : 0f;

            Color edge = card.interactable
                ? Color.Lerp(CardEdgeIdle, CardEdgeHover, Mathf.Max(card.hover, breathe * 0.30f))
                : CardEdgeDisabled;
            card.edge.color = edge;

            card.sheen.color = new Color(1f, 1f, 1f, 0.06f * card.hover);

            Color accent = HoverGrey;
            accent.a = 0.85f * Mathf.Max(card.hover, breathe * 0.35f);
            card.accent.color = accent;

            card.number.color = card.interactable
                ? Color.Lerp(NumberIdle, HoverGrey, Mathf.Max(card.hover, breathe * 0.25f))
                : NumberDisabled;

            card.rect.localScale = Vector3.one * (1f - 0.008f * card.press);

            bool showDeleteTab = card.deleteTab != null
                && card.hasSave
                && card.interactable
                && !deleting
                && card.hovered;

            if (card.deleteTab != null && card.deleteTab.gameObject.activeSelf != showDeleteTab)
                card.deleteTab.gameObject.SetActive(showDeleteTab);
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

        bool show = !IsOpen
            && SaveGameManager.Instance != null
            && SaveGameManager.Instance.CanPause;

        // The tab names the button the player actually holds: a pad reports its start button,
        // everything else uses escape.
        if (pauseHintLabel != null)
        {
            string label = GamepadConnected() ? "OPTIONS" : "MENU";

            if (pauseHintLabel.text != label)
                pauseHintLabel.text = label;
        }

        float deltaTime = Time.unscaledDeltaTime;

        if (show)
        {
            // The first appearance is the slow one; after that the player knows the tab is there
            // and only needs it back.
            float duration = pauseHintRevealed ? PauseHintReturn : PauseHintFade;
            pauseHintRevealed = true;

            pauseHintReveal = Mathf.MoveTowards(
                pauseHintReveal,
                1f,
                deltaTime / Mathf.Max(duration, 0.0001f));
        }
        else
        {
            // The tab leaves with the menu it belongs to instead of being cut off the instant the
            // pause screen appears.
            pauseHintReveal = Mathf.MoveTowards(
                pauseHintReveal,
                0f,
                deltaTime / Mathf.Max(PauseHintHide, 0.0001f));
        }

        if (pauseHintGroup != null)
            pauseHintGroup.alpha = 1f - Mathf.Pow(1f - pauseHintReveal, 3f);

        bool visible = show || pauseHintReveal > 0.001f;

        if (pauseHintCanvas.enabled != visible)
            pauseHintCanvas.enabled = visible;
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

        // The tab fades in the first time a chapter offers it, so it arrives rather than being
        // switched on. The group holds that alpha; the canvas stays the on/off switch.
        pauseHintGroup = canvasObject.AddComponent<CanvasGroup>();
        pauseHintGroup.alpha = 0f;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = CreateRect("Root", canvasObject.transform);
        Stretch(root, 0f, 0f, 0f, 0f);

        // A small tab in the left corner, with real breathing room from the screen edge so it
        // does not crowd the frame or look pasted onto it.
        // A corner tab rather than a floating button: two pause bars, then the word, then open
        // space, all on the menu's own slanted plate with a thin white cut edge. The state runs
        // through the same plate effect the menu plates use, so it hovers and dips like them.
        RectTransform rect = CreateRect("PauseButton", root);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(176f, 46f);
        rect.anchoredPosition = new Vector2(26f, -26f);

        Vector2 plateSize = rect.sizeDelta;

        SkewedPanel edge = CreateSkew(
            rect,
            "Edge",
            plateSize + new Vector2(6f, 6f),
            Vector2.zero,
            PlateEdgeIdle,
            PlateSlant);

        SkewedPanel face = CreateSkew(rect, "Face", plateSize, Vector2.zero, PlateFace, PlateSlant);
        face.raycastTarget = true;

        SkewedPanel sheen = CreateSkew(
            rect,
            "Sheen",
            plateSize,
            Vector2.zero,
            new Color(1f, 1f, 1f, 0f),
            PlateSlant);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            if (SaveGameManager.Instance != null)
                SaveGameManager.Instance.PauseGame();
        });

        for (int i = 0; i < 2; i++)
        {
            RectTransform barRect = CreateRect("Bar" + i, rect);
            barRect.anchorMin = new Vector2(0f, 0.5f);
            barRect.anchorMax = new Vector2(0f, 0.5f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(5f, 15f);
            barRect.anchoredPosition = new Vector2(26f + i * 10f, 0f);

            SkewedPanel bar = barRect.gameObject.AddComponent<SkewedPanel>();
            bar.Slant = 2f;
            bar.color = Ink;
            bar.raycastTarget = false;
        }

        RectTransform labelRect = CreateRect("Label", rect);
        Stretch(labelRect, 52f, 0f, 12f, 0f);

        TMP_Text label = AddText(
            labelRect,
            "MENU",
            13f,
            InvertedIdle,
            TextAlignmentOptions.Left,
            BoldFont,
            FontStyles.Bold);
        label.characterSpacing = 6f;
        pauseHintLabel = label;

        var fx = new PlateFx
        {
            rect = rect,
            button = button,
            home = rect.anchoredPosition,
            slide = 0f,
            edge = edge,
            sheen = sheen,
            label = label,
            labelIdle = InvertedIdle,
            labelHover = Color.white,
            edgeIdle = PlateEdgeIdle,
            edgeHover = PlateEdgeHover,
            pressScale = 0.98f,
        };

        plates.Add(fx);
        HookPlate(button, fx);

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
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();

        RectTransform backdrop = CreateRect("Backdrop", root);
        Stretch(backdrop, 0f, 0f, 0f, 0f);
        AddImage(backdrop, BackdropColor, true);

        BuildWindow(root);
        BuildConfirm(root);
        BuildLoading(root);
        BuildToast(root);
        BuildScreenFade();

        canvas.enabled = false;
    }

    void BuildWindow(Transform root)
    {
        windowRect = CreateRect("Window", root);
        SetCentered(windowRect, new Vector2(WindowWidth, WindowHeight), Vector2.zero);

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

        // Quitting from here makes five rows, so the stack is re-spaced to fit them all.
        pauseResumeButton = MakeMenuPlate(panel, "ResumeButton", "RESUME", new Vector2(0f, 208f), new Vector2(640f, 84f), ResumePressed);
        MakeMenuPlate(panel, "SaveButton", "SAVE GAME", new Vector2(0f, 104f), new Vector2(640f, 84f), OpenSave);
        MakeMenuPlate(panel, "LoadButton", "LOAD GAME", new Vector2(0f, 0f), new Vector2(640f, 84f), OpenPauseLoad);
        MakeMenuPlate(panel, "MenuButton", "RETURN TO MAIN MENU", new Vector2(0f, -104f), new Vector2(640f, 84f), ReturnToMenuPressed);
        MakeMenuPlate(panel, "QuitButton", "QUIT GAME", new Vector2(0f, -208f), new Vector2(640f, 84f), QuitFromPausePressed);
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
        hintRect.offsetMax = new Vector2(-700f, 14f);

        footerHint = AddText(hintRect, string.Empty, 15.5f, Faint, TextAlignmentOptions.Left, RegularFont, FontStyles.Normal);

        RectTransform backRect = CreateRect("BackButton", footer);
        backRect.anchorMin = new Vector2(1f, 0.5f);
        backRect.anchorMax = new Vector2(1f, 0.5f);
        backRect.pivot = new Vector2(1f, 0.5f);
        backRect.sizeDelta = new Vector2(212f, 54f);
        backRect.anchoredPosition = new Vector2(-8f, 0f);

        backButton = CreateInvertedPlate(backRect, "BACK", BackPressed, 17f, PlateSlant);
        backLabel = backButton.GetComponentInChildren<TMP_Text>();

        // The delete plate sits beside BACK and only on the two lists, so the delete screen itself
        // never offers a way to delete again.
        RectTransform deleteRect = CreateRect("DeleteButton", footer);
        deleteRect.anchorMin = new Vector2(1f, 0.5f);
        deleteRect.anchorMax = new Vector2(1f, 0.5f);
        deleteRect.pivot = new Vector2(1f, 0.5f);
        deleteRect.sizeDelta = new Vector2(230f, 52f);
        deleteRect.anchoredPosition = new Vector2(-234f, 0f);

        deleteButton = CreateDarkPlate(deleteRect, "DELETE SAVE", OpenDelete, 15f, Muted, 6f);

        // CONTINUE leads the cluster: one press resumes the newest save, autosave included.
        RectTransform continueRect = CreateRect("ContinueButton", footer);
        continueRect.anchorMin = new Vector2(1f, 0.5f);
        continueRect.anchorMax = new Vector2(1f, 0.5f);
        continueRect.pivot = new Vector2(1f, 0.5f);
        continueRect.sizeDelta = new Vector2(200f, 52f);
        continueRect.anchoredPosition = new Vector2(-478f, 0f);

        continueButton = CreateDarkPlate(continueRect, "CONTINUE", ContinuePressed, 15f, Ink, 6f);
    }

    SlotCard BuildCard(Transform parent, int slot)
    {
        var card = new SlotCard();

        RectTransform rect = CreateRect("Slot" + slot.ToString("00"), parent);
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        card.rect = rect;
        card.slot = slot;
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
            new Color(HoverGrey.r, HoverGrey.g, HoverGrey.b, 0f),
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

        // A compact delete tag under the slot number, shown only while the pointer is on the box
        // so a written save can be removed without walking the delete list. Keyboard and gamepad
        // keep using the list, since neither can hover.
        RectTransform deleteRect = CreateRect("DeleteTab", rect);
        deleteRect.anchorMin = new Vector2(0f, 0f);
        deleteRect.anchorMax = new Vector2(0f, 0f);
        deleteRect.pivot = new Vector2(0f, 0f);
        deleteRect.sizeDelta = new Vector2(66f, 22f);
        deleteRect.anchoredPosition = new Vector2(30f, 10f);

        card.deleteTab = CreateDarkPlate(deleteRect, "DEL", () => ShowDeleteConfirm(capturedSlot), 9f, Muted, 4f);
        card.deleteTab.gameObject.SetActive(false);

        return card;
    }

    void BuildConfirm(Transform root)
    {
        RectTransform overlay = CreateRect("Confirm", root);
        Stretch(overlay, 0f, 0f, 0f, 0f);
        AddImage(overlay, Overlay, true);
        confirmPanel = overlay.gameObject;

        // The whole card fades as one group while the box itself does the menu's small pop, so the
        // dim behind it fades too but does not scale away from the screen edges.
        confirmGroup = overlay.gameObject.AddComponent<CanvasGroup>();

        RectTransform box = CreateRect("ConfirmBox", overlay);
        SetCentered(box, new Vector2(860f, 380f), Vector2.zero);
        confirmBox = box;

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

        // The two plates are centred as one pair, shear included, so the answer sits on the box's
        // middle line instead of drifting to the left.
        const float yesWidth = 250f;
        const float yesHeight = 56f;
        const float cancelWidth = 220f;
        const float cancelHeight = 54f;
        const float gap = 22f;
        const float plateY = -132f;

        float pairLeft = -(yesWidth + gap + cancelWidth) * 0.5f - PlateSlant * 0.5f;

        RectTransform yesRect = CreateRect("ConfirmYes", box);
        SetCentered(
            yesRect,
            new Vector2(yesWidth, yesHeight),
            new Vector2(pairLeft + yesWidth * 0.5f, plateY));

        Button yes = CreateInvertedPlate(yesRect, "CONFIRM", ConfirmPressed, 18f, PlateSlant);
        confirmYesLabel = yes.GetComponentInChildren<TMP_Text>();

        RectTransform cancelRect = CreateRect("ConfirmCancel", box);
        SetCentered(
            cancelRect,
            new Vector2(cancelWidth, cancelHeight),
            new Vector2(pairLeft + yesWidth + gap + cancelWidth * 0.5f, plateY));

        confirmCancelButton = CreateDarkPlate(cancelRect, "CANCEL", HideConfirm, 18f, Ink, 0f);

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

    /// <summary>
    /// Swaps the slot grid into its delete list. The plate lives in the footer of the save and
    /// load screens; BACK returns to whichever of them it was opened from.
    /// </summary>
    void OpenDelete()
    {
        if (!AnySaveExists())
        {
            ShowToast("NO SAVES TO DELETE");
            return;
        }

        deleteReturnMode = mode == Mode.Save ? Mode.Save : Mode.Load;
        RefreshCards(Mode.Delete);
        SetMode(Mode.Delete);
    }

    void ShowDeleteConfirm(int slot)
    {
        if (!SaveSlots.HasSave(slot))
        {
            ShowToast("SLOT " + slot.ToString("00") + " IS EMPTY");
            return;
        }

        ShowConfirm(
            "Delete the save in slot " + slot.ToString("00") + "? This cannot be undone.",
            "DELETE",
            () => DeleteSlot(slot));
    }

    void DeleteSlot(int slot)
    {
        if (!SaveSlots.Delete(slot))
        {
            ShowToast("COULD NOT DELETE SLOT " + slot.ToString("00"));
            return;
        }

        // A per-box delete happens on a list screen; a delete-list delete happens in delete mode.
        Mode listMode = mode == Mode.Save || mode == Mode.Load ? mode : Mode.Delete;
        RefreshCards(listMode);
        RefreshDeleteButton();
        RefreshContinueButton();
        ShowToast("SLOT " + slot.ToString("00") + " DELETED");
    }

    /// <summary>Resumes the newest save, autosave included, without opening a box.</summary>
    void ContinuePressed()
    {
        int newest = SaveSlots.NewestSlot();

        if (newest < 0)
        {
            ShowToast("NO SAVE TO CONTINUE");
            return;
        }

        if (SaveGameManager.Instance != null)
            SaveGameManager.Instance.LoadFromSlot(newest);
    }

    /// <summary>CONTINUE belongs to the load list, and only while there is something to resume.</summary>
    void RefreshContinueButton()
    {
        if (continueButton == null)
            return;

        continueButton.gameObject.SetActive(mode == Mode.Load && SaveSlots.NewestSlot() >= 0);
    }

    static bool AnySaveExists()
    {
        for (int slot = 1; slot <= SaveSlots.SlotCount; slot++)
        {
            if (SaveSlots.HasSave(slot))
                return true;
        }

        return false;
    }

    void ReturnToMenuPressed()
    {
        ShowConfirm(
            "Return to the main menu? Anything since your last save will be lost.",
            "MAIN MENU",
            () => SaveGameManager.Instance.ReturnToMainMenu());
    }

    /// <summary>
    /// Quits from under the pause menu. It asks first, and it stays in pause mode while it asks,
    /// so cancelling leaves the pause screen exactly where it was.
    /// </summary>
    void QuitFromPausePressed()
    {
        ShowConfirm(
            "Quit Black Dome? Anything since your last save will be lost.",
            "QUIT",
            QuitNow);
    }

    // ------------------------------------------------------------------ ui helpers

    /// <summary>
    /// Gives the keyboard and gamepad somewhere to start, so a screen can be walked the moment it
    /// opens rather than waiting for the pointer to wake it up.
    /// </summary>
    void SelectDefaultFocus()
    {
        if (EventSystem.current == null)
            return;

        if (mode == Mode.Pause && pauseResumeButton != null)
        {
            Select(pauseResumeButton.gameObject);
            return;
        }

        if (mode == Mode.Save || mode == Mode.Load || mode == Mode.Delete)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].button != null && cards[i].button.IsInteractable())
                {
                    Select(cards[i].button.gameObject);
                    return;
                }
            }
        }
    }

    /// <summary>The box or plate the keyboard and gamepad are currently on.</summary>
    static bool IsSelected(GameObject target)
    {
        return target != null
            && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == target;
    }

    static void Select(GameObject target)
    {
        if (target == null || EventSystem.current == null)
            return;

        if (EventSystem.current.currentSelectedGameObject != target)
            EventSystem.current.SetSelectedGameObject(target);
    }

    static bool GamepadConnected()
    {
#if ENABLE_INPUT_SYSTEM
        return Gamepad.current != null;
#else
        return false;
#endif
    }

    Button MakeMenuPlate(
        Transform parent,
        string name,
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
            textRect.offsetMin = new Vector2(36f, 0f);
            textRect.offsetMax = new Vector2(-24f, 0f);
        }

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
            button = button,
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

    Button CreateInvertedPlate(
        RectTransform rect,
        string label,
        Action onClick,
        float fontSize,
        float slant = 0f)
    {
        // A solid white plate with black type: the inverted button Persona uses for its
        // confirmations, and the loudest thing on the screen. A slant shears it into the same
        // slab as the menu plates, so it can stand beside one without the edges clashing.
        Graphic fill;

        if (slant > 0f)
        {
            Vector2 size = rect.rect.size;
            if (size.x < 2f || size.y < 2f)
                size = rect.sizeDelta;

            SkewedPanel panel = CreateSkew(rect, "Face", size, Vector2.zero, InvertedIdle, slant);
            panel.raycastTarget = true;
            fill = panel;
        }
        else
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = InvertedIdle;
            image.raycastTarget = true;
            fill = image;
        }

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
            button = button,
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
            Select(button.gameObject);
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
            Select(button.gameObject);
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
