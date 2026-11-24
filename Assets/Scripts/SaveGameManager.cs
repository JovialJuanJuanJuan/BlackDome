using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runs the save system for the whole game: the Escape pause menu, saving to a slot, loading a
/// slot back to the exact line it was saved on, and returning to the title menu.
///
/// A save records the scene, how many dialogue lines had played and which choices were made.
/// Loading reloads that scene and then fast-forwards it by clicking Next (and the same choice
/// buttons) at high speed, so the pictures, expressions, music and fades end up exactly as the
/// player left them instead of restarting the scene from the top.
///
/// It installs itself, so no scene needs a component added to it.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaveGameManager : MonoBehaviour
{
    public static SaveGameManager Instance { get; private set; }

    // A loaded scene is replayed far faster than it plays. The limits below keep a broken or
    // changed scene from holding the player on the loading screen forever.
    const float RestoreTimeScale = 200f;
    const float RestoreIdleLimit = 1.5f;
    const float RestoreHardLimit = 60f;
    const float FirstLineLimit = 10f;

    // Every scene hand-over goes through black: the outgoing picture and its music wind down, then
    // the incoming scene is brought up, so neither end of a load ever shows a frozen frame.
    const float ReturnFadeOut = 0.7f;
    const float ReturnFadeIn = 0.8f;
    const float LoadFadeOut = 0.5f;
    const float LoadFadeIn = 0.7f;
    const float QuitFade = 0.8f;

    // Scenes 0 to 2 are the splash screen, the title menu and the credits.
    const int FirstGameplayBuildIndex = 3;

    SaveMenuUI ui;

    SaveSlotData pendingRestore;
    bool isRestoring;

    bool paused;
    float timeScaleBeforePause = 1f;
    bool saving;
    bool returningToMenu;
    bool loadingSave;
    bool fadeInOnSceneLoad;
    bool quitting;

    readonly List<string> choiceRoute = new List<string>();
    string lastChoiceName;
    float lastChoiceTime = float.NegativeInfinity;

    Button nextButton;
    MethodInfo nextHandler;
    MonoBehaviour nextTarget;
    bool handlerSearched;
    bool waitingForNextButtonToClear;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        var host = new GameObject("SaveGameManager");
        DontDestroyOnLoad(host);
        host.AddComponent<SaveGameManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ui = gameObject.AddComponent<SaveMenuUI>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        // When a chapter is played directly from the editor, its scene was already loaded
        // before this manager existed, so the scene-loaded hook never ran for it.
        if (!IsGameplayScene())
            return;

        TextCreator.ResetSceneLineCount();
        WriteAutosave();
        StartCoroutine(WatchChoiceButtons());
    }

    void Update()
    {
        if ((EscapePressed() || MenuButtonPressed()) && ui != null)
            ui.HandleEscape();
    }

    /// <summary>The pad's start/options button toggles the menu the same way Escape does.</summary>
    static bool MenuButtonPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            return true;
#endif
        return false;
    }

    static bool EscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif
        return false;
    }

    static bool IsGameplayScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.buildIndex >= FirstGameplayBuildIndex
            && scene.buildIndex < SceneManager.sceneCountInBuildSettings;
    }

    // ------------------------------------------------------------------ pause

    /// <summary>True when the pause menu can be opened right now (a chapter is playing, not loading).</summary>
    public bool CanPause
    {
        get { return !isRestoring && IsGameplayScene(); }
    }

    public void PauseGame()
    {
        if (ui == null || ui.IsOpen || !CanPause)
            return;

        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        paused = true;

        Time.timeScale = 0f;
        AudioListener.pause = true;

        ui.OpenPause();
    }

    public void ResumeGame()
    {
        if (ui != null)
            ui.CloseMenu();

        EndPause();
    }

    void EndPause()
    {
        if (paused)
        {
            Time.timeScale = timeScaleBeforePause;
            paused = false;
        }

        AudioListener.pause = false;
    }

    // ------------------------------------------------------------------ saving

    public void SaveToSlot(int slot)
    {
        if (isRestoring || saving || slot < 1 || slot > SaveSlots.SlotCount)
            return;

        saving = true;
        StartCoroutine(SaveRoutine(slot));
    }

    /// <summary>
    /// Records the start of the chapter that is now playing. It has no screenshot and no dialogue
    /// preview because it is written before the scene has drawn a frame; it only needs the scene
    /// and the route, and an entry with no lines simply replays the scene from the top.
    /// </summary>
    void WriteAutosave()
    {
        if (!IsGameplayScene())
            return;

        Scene scene = SceneManager.GetActiveScene();

        var data = new SaveSlotData
        {
            slot = SaveSlots.AutosaveSlot,
            sceneBuildIndex = scene.buildIndex,
            sceneName = scene.name,
            sceneTitle = SaveSlots.TitleForScene(scene.name),
            lineCount = TextCreator.SceneLineCount,
            choiceRoute = string.Join("|", choiceRoute.ToArray()),
            savedAtUtc = DateTime.UtcNow.ToString("o"),
        };

        SaveSlots.Write(data, null);
    }

    IEnumerator SaveRoutine(int slot)
    {
        // The menu is hidden for one frame so the thumbnail shows the scene, not the menu.
        ui.PrepareForCapture();
        yield return new WaitForEndOfFrame();

        Texture2D screenshot = null;

        try
        {
            screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        }
        catch (Exception error)
        {
            Debug.LogWarning("SaveLoad: could not capture the screen for a thumbnail - " + error.Message);
        }

        ui.EndCapture();

        Scene scene = SceneManager.GetActiveScene();

        var data = new SaveSlotData
        {
            slot = slot,
            sceneBuildIndex = scene.buildIndex,
            sceneName = scene.name,
            sceneTitle = SaveSlots.TitleForScene(scene.name),
            lineCount = TextCreator.SceneLineCount,
            choiceRoute = string.Join("|", choiceRoute.ToArray()),
            savedAtUtc = DateTime.UtcNow.ToString("o"),
        };

        ReadDialogue(out string speaker, out string preview);
        data.speaker = speaker;
        data.preview = preview;

        Texture2D thumbnail = null;

        if (screenshot != null)
        {
            try
            {
                thumbnail = SaveSlots.CreateThumbnail(screenshot);
            }
            catch (Exception error)
            {
                Debug.LogWarning("SaveLoad: could not build the save thumbnail - " + error.Message);
            }

            Destroy(screenshot);
        }

        bool written = SaveSlots.Write(data, thumbnail);

        if (thumbnail != null)
            Destroy(thumbnail);

        saving = false;
        ui.RefreshSlots();
        ui.ShowToast(written
            ? "SAVED TO SLOT " + slot.ToString("00")
            : "COULD NOT WRITE SLOT " + slot.ToString("00"));
    }

    /// <summary>Reads the line and speaker currently on the dialogue card, for the slot preview.</summary>
    static void ReadDialogue(out string speaker, out string preview)
    {
        speaker = string.Empty;
        preview = string.Empty;

        TMP_Text body = null;
        TMP_Text namePlate = null;

        TMP_Text[] labels = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null)
                continue;

            if (body == null && label.name == "SpeakText")
                body = label;
            else if (namePlate == null && label.name == "CharName")
                namePlate = label;
        }

        if (namePlate != null)
            speaker = Flatten(namePlate.text, 40);

        if (body != null)
            preview = Flatten(body.text, 120);

        if (preview.Length == 0 && !string.IsNullOrEmpty(TextCreator.LatestLine))
            preview = Flatten(DialogueText.Body(TextCreator.LatestLine), 120);
    }

    static string Flatten(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string flat = value.Replace("\r", " ").Replace("\n", " ").Trim();

        while (flat.Contains("  "))
            flat = flat.Replace("  ", " ");

        if (flat.Length <= maxLength)
            return flat;

        return flat.Substring(0, maxLength - 1).TrimEnd() + "\u2026";
    }

    // ------------------------------------------------------------------ loading

    public void LoadFromSlot(int slot)
    {
        SaveSlotData data = SaveSlots.Read(slot);

        if (data == null)
        {
            ui.ShowToast(SaveSlots.HasSave(slot)
                ? "SLOT " + slot.ToString("00") + " IS DAMAGED"
                : "SLOT " + slot.ToString("00") + " IS EMPTY");
            return;
        }

        if (loadingSave || isRestoring)
            return;

        StartCoroutine(LoadFromSlotRoutine(data));
    }

    IEnumerator LoadFromSlotRoutine(SaveSlotData data)
    {
        loadingSave = true;
        pendingRestore = data;

        // From the title menu the menu and its music fade out first, because the scene behind the
        // boxes is still alive and would otherwise be cut. Loading from the pause menu already
        // happens behind the loading screen, so it goes straight there.
        bool fromTitle = !IsGameplayScene();

        if (fromTitle)
        {
            FadeSceneMusic(LoadFadeOut);

            if (ui != null)
                ui.FadeScreenOut(LoadFadeOut);

            yield return new WaitForSecondsRealtime(LoadFadeOut);
        }

        // The loading screen goes up before the scene starts loading, so the pause menu does not
        // flash back to the frozen scene on the way out.
        if (ui != null)
            ui.ShowLoading(data.sceneTitle);

        EndPause();
        Time.timeScale = 1f;

        yield return LoadSceneRoutine(data);

        loadingSave = false;
    }

    IEnumerator LoadSceneRoutine(SaveSlotData data)
    {
        if (data.sceneBuildIndex < 0 || data.sceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            pendingRestore = null;
            Debug.LogWarning("SaveLoad: the save points at a scene that is not in the build list - " + data.sceneName);

            if (ui != null)
            {
                ui.HideLoading();
                ui.FadeScreenIn(LoadFadeIn);
            }

            yield break;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(data.sceneBuildIndex);

        while (load != null && !load.isDone)
            yield return null;
    }

    public void ReturnToMainMenu()
    {
        if (returningToMenu)
            return;

        StartCoroutine(ReturnToMainMenuRoutine());
    }

    IEnumerator ReturnToMainMenuRoutine()
    {
        returningToMenu = true;

        if (ui != null)
            ui.CloseMenu();

        // The pause is lifted before the fade so the music and the last picture of the chapter
        // move together instead of sitting frozen under the black.
        EndPause();
        Time.timeScale = 1f;

        FadeSceneMusic(ReturnFadeOut);

        if (ui != null)
            ui.FadeScreenOut(ReturnFadeOut);

        yield return new WaitForSecondsRealtime(ReturnFadeOut);

        pendingRestore = null;

        int menuIndex = FindSceneIndex("MainMenu");

        if (menuIndex < 0)
        {
            Debug.LogWarning("SaveLoad: no scene named MainMenu is in the build list.");

            if (ui != null)
                ui.FadeScreenIn(ReturnFadeIn);

            returningToMenu = false;
            yield break;
        }

        // The black stays up across the load and is only lifted once the menu is standing.
        fadeInOnSceneLoad = true;

        AsyncOperation load = SceneManager.LoadSceneAsync(menuIndex);

        while (load != null && !load.isDone)
            yield return null;

        returningToMenu = false;
    }

    /// <summary>
    /// Winds the scene's music down before the scene is replaced. The fader belongs to the scene
    /// that is leaving, so this has to run while that scene is still loaded.
    /// </summary>
    static void FadeSceneMusic(float duration)
    {
        BackgroundMusicFader[] faders = FindObjectsByType<BackgroundMusicFader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < faders.Length; i++)
        {
            if (faders[i] != null)
                faders[i].FadeOutOver(duration);
        }
    }

    static void RestoreSceneMusic()
    {
        BackgroundMusicFader[] faders = FindObjectsByType<BackgroundMusicFader>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < faders.Length; i++)
        {
            if (faders[i] != null)
                faders[i].FadeIn();
        }
    }

    /// <summary>
    /// Closes the game on a fade: the music winds down and the screen goes black before the process
    /// ends, so quitting is a transition like every other one the menus make.
    /// </summary>
    public void QuitGame()
    {
        if (quitting)
            return;

        StartCoroutine(QuitRoutine());
    }

    IEnumerator QuitRoutine()
    {
        quitting = true;

        FadeSceneMusic(QuitFade);

        if (ui != null)
            ui.FadeScreenOut(QuitFade);

        yield return new WaitForSecondsRealtime(QuitFade);

        Application.Quit();

        // The editor never closes on Application.Quit, so play mode is given its screen and music
        // back rather than being left sitting on a black screen.
        if (Application.isEditor)
        {
            RestoreSceneMusic();

            if (ui != null)
                ui.FadeScreenIn(ReturnFadeIn);
        }

        quitting = false;
    }

    static int FindSceneIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (string.Equals(name, sceneName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    // ------------------------------------------------------------------ scene hooks

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TextCreator.ResetSceneLineCount();

        // Coming back to the title menu through black: the menu's own opening fade would stack
        // under ours, so it steps aside and the black is lifted here instead.
        if (fadeInOnSceneLoad)
        {
            fadeInOnSceneLoad = false;

            MainMenu menu = FindFirstObjectByType<MainMenu>();

            if (menu != null)
                menu.SkipFadeIn();

            if (ui != null)
                ui.FadeScreenIn(ReturnFadeIn);
        }

        choiceRoute.Clear();
        lastChoiceName = null;
        lastChoiceTime = float.NegativeInfinity;

        nextButton = null;
        nextHandler = null;
        nextTarget = null;
        handlerSearched = false;
        waitingForNextButtonToClear = false;

        SaveSlotData restore = pendingRestore;
        pendingRestore = null;

        if (restore != null && restore.sceneBuildIndex == scene.buildIndex && restore.lineCount > 0)
        {
            StartCoroutine(RestoreRoutine(restore));
        }
        else if (restore != null)
        {
            // Nothing to replay (a scene with no dialogue lines), so just show its opening.
            Time.timeScale = 1f;
            AudioListener.pause = false;

            if (ui != null)
            {
                ui.HideLoading();
                ui.FadeScreenIn(LoadFadeIn);
            }
        }
        else
        {
            // A chapter was entered without a save to replay, so mark its opening for CONTINUE.
            WriteAutosave();
        }

        StartCoroutine(WatchChoiceButtons());
    }

    /// <summary>
    /// Remembers which choice the player picks, so a load can walk the same route back through
    /// the branches it needs to reach the saved line.
    /// </summary>
    IEnumerator WatchChoiceButtons()
    {
        // Wait for the scene's own scripts to run their Start and hide their choice buttons.
        yield return null;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
                continue;

            string name = button.name;

            if (name.IndexOf("choice", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (name.IndexOf("sound", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            Button captured = button;
            button.onClick.AddListener(() => RecordChoice(captured.name));
        }
    }

    void RecordChoice(string buttonName)
    {
        if (isRestoring)
            return;

        // A double click must not put the same choice in the route twice.
        if (buttonName == lastChoiceName && Time.unscaledTime - lastChoiceTime < 0.75f)
            return;

        lastChoiceName = buttonName;
        lastChoiceTime = Time.unscaledTime;
        choiceRoute.Add(buttonName);
    }

    // ------------------------------------------------------------------ replaying a save

    IEnumerator RestoreRoutine(SaveSlotData data)
    {
        isRestoring = true;

        ui.ShowLoading(data.sceneTitle);
        ui.SetLoadingProgress(0, data.lineCount);

        float previousScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        bool previousAudioPause = AudioListener.pause;
        int previousVSync = QualitySettings.vSyncCount;
        int previousTargetRate = Application.targetFrameRate;

        Time.timeScale = RestoreTimeScale;
        AudioListener.pause = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 240;
        TextCreator.InstantMode = true;

        // Let the scene's Start methods and their first coroutine steps run.
        yield return null;

        float startTime = Time.realtimeSinceStartup;

        while (TextCreator.SceneLineCount == 0 && Time.realtimeSinceStartup - startTime < FirstLineLimit)
            yield return null;

        if (TextCreator.SceneLineCount == 0)
            Debug.LogWarning("SaveLoad: the scene never started its dialogue, so it loaded from the top.");

        string[] route = string.IsNullOrEmpty(data.choiceRoute)
            ? Array.Empty<string>()
            : data.choiceRoute.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

        int routeIndex = 0;
        float idleTime = 0f;
        int frames = 0;

        while (TextCreator.SceneLineCount < data.lineCount)
        {
            if (SceneManager.GetActiveScene().buildIndex != data.sceneBuildIndex)
                break;

            if (Time.realtimeSinceStartup - startTime > RestoreHardLimit)
            {
                Debug.LogWarning("SaveLoad: replaying the scene took too long and stopped early.");
                break;
            }

            if (AdvanceIfPossible() || ReplayChoice(route, ref routeIndex))
            {
                idleTime = 0f;
            }
            else
            {
                idleTime += Time.unscaledDeltaTime;

                if (idleTime > RestoreIdleLimit)
                    break;
            }

            if (++frames % 5 == 0)
                ui.SetLoadingProgress(TextCreator.SceneLineCount, data.lineCount);

            yield return null;
        }

        ui.SetLoadingProgress(TextCreator.SceneLineCount, data.lineCount);

        TextCreator.InstantMode = false;

        // Two frames so the restored line lays itself out and its Next button settles.
        yield return null;
        yield return null;

        Time.timeScale = previousScale;
        AudioListener.pause = previousAudioPause;
        QualitySettings.vSyncCount = previousVSync;
        Application.targetFrameRate = previousTargetRate;

        isRestoring = false;
        ui.HideLoading();

        // The replay happened behind black; this is the moment the scene is actually seen again.
        ui.FadeScreenIn(LoadFadeIn);
    }

    /// <summary>
    /// Clicks the scene's own Next button once, but only when that button is on screen: while a
    /// line is typing the scene hides it, so the replay waits instead of skipping dialogue.
    /// </summary>
    bool AdvanceIfPossible()
    {
        Button next = FindNextButton();
        bool ready = next != null && next.gameObject.activeInHierarchy && next.IsInteractable();

        if (waitingForNextButtonToClear)
        {
            if (!ready)
                waitingForNextButtonToClear = false;

            return false;
        }

        if (!ready)
            return false;

        if (!handlerSearched)
            CacheNextHandler();

        if (nextHandler == null || nextTarget == null)
            return false;

        try
        {
            nextHandler.Invoke(nextTarget, null);
        }
        catch (Exception error)
        {
            Debug.LogWarning("SaveLoad: could not advance the scene while restoring - " + error.Message);
            return false;
        }

        waitingForNextButtonToClear = true;
        return true;
    }

    bool ReplayChoice(string[] route, ref int routeIndex)
    {
        if (routeIndex >= route.Length)
            return false;

        Button choice = FindChoiceButton(route[routeIndex]);

        if (choice == null)
            return false;

        // Remember the choice in this session's route as well, so a save made after this load
        // still knows which branch of the scene it is on.
        choiceRoute.Add(route[routeIndex]);
        routeIndex++;

        try
        {
            choice.onClick.Invoke();
        }
        catch (Exception error)
        {
            Debug.LogWarning("SaveLoad: could not replay a choice while restoring - " + error.Message);
        }

        return true;
    }

    Button FindChoiceButton(string buttonName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null || button.name != buttonName)
                continue;

            if (!button.gameObject.activeInHierarchy || !button.IsInteractable())
                continue;

            return button;
        }

        return null;
    }

    Button FindNextButton()
    {
        if (nextButton != null)
            return nextButton;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
                continue;

            string name = button.name;

            if (name.IndexOf("next", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (name.IndexOf("sound", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            nextButton = button;
            break;
        }

        return nextButton;
    }

    /// <summary>
    /// The dialogue scripts all expose a public NextButton method wired to the Next button, so
    /// invoking it here is the same thing the player's click does, without needing the pointer.
    /// </summary>
    void CacheNextHandler()
    {
        handlerSearched = true;
        nextHandler = null;
        nextTarget = null;

        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            MethodInfo method = behaviour.GetType().GetMethod(
                "NextButton",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);

            if (method == null || method.ReturnType != typeof(void))
                continue;

            nextHandler = method;
            nextTarget = behaviour;
            return;
        }
    }
}
