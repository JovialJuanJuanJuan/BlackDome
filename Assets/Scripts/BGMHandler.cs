using System.Collections;
using UnityEngine;

/// <summary>
/// Fades a background music AudioSource in when the scene starts and back out
/// to silence when the "Fadeout" GameObject becomes active.
/// Put this on an empty GameObject and drag the music AudioSource into the inspector.
/// </summary>
[DisallowMultipleComponent]
public class BackgroundMusicFader : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("The AudioSource playing the background music.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Start the AudioSource automatically if it is not already playing.")]
    [SerializeField] private bool playOnStart = true;

    [Header("Fade In")]
    [Tooltip("Seconds to fade from silence up to the AudioSource's volume (its volume at Awake is used as the target).")]
    [SerializeField] private float fadeInDuration = 2f;

    [Header("Fade Out")]
    [Tooltip("Seconds to fade from the current volume down to silence.")]
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Tooltip("GameObject that triggers the fade out when it is enabled. It may start inactive - assign it here so it can be found. If left empty, the object name below is searched for.")]
    [SerializeField] private GameObject fadeoutObject;

    [Tooltip("Name used to look up the Fadeout GameObject when no reference is assigned.")]
    [SerializeField] private string fadeoutObjectName = "Fadeout";

    [Tooltip("Stop the AudioSource once the fade out reaches silence.")]
    [SerializeField] private bool stopWhenSilent;

    [Header("Behaviour")]
    [Tooltip("Keep fading while Time.timeScale is 0 (e.g. paused menus).")]
    [SerializeField] private bool ignoreTimeScale;

    /// <summary>Volume the music fades in to, captured from the AudioSource in Awake.</summary>
    private float targetVolume;

    /// <summary>True once a fade out has been requested, so it only happens once.</summary>
    private bool fadeOutRequested;

    private Coroutine fadeRoutine;

    // Used by the quick duck-out / resume pair below
    private Coroutine quickFadeRoutine;
    private bool quickFadePaused;
    private float quickFadeVolume;

    public bool IsFadingOut { get; private set; }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            Debug.LogError($"{nameof(BackgroundMusicFader)}: no AudioSource assigned on '{name}'.", this);
            enabled = false;
            return;
        }

        // Remember the designer's volume, then start silent so the fade in has somewhere to go.
        targetVolume = audioSource.volume;
        audioSource.volume = 0f;

        if (targetVolume <= 0f)
        {
            Debug.LogWarning(
                $"{nameof(BackgroundMusicFader)}: the AudioSource volume is 0, so the fade in will be silent. " +
                "Set the volume you want on the AudioSource.", this);
        }
    }

    private void Start()
    {
        if (audioSource == null)
        {
            return;
        }

        if (playOnStart && !audioSource.isPlaying)
        {
            audioSource.Play();
        }

        FadeIn();
        StartCoroutine(WatchFadeoutObject());
    }

    /// <summary>Fades the music up to its original volume.</summary>
    public void FadeIn()
    {
        if (audioSource == null)
        {
            return;
        }

        fadeOutRequested = false;
        IsFadingOut = false;
        BeginFade(targetVolume, fadeInDuration);
    }

    /// <summary>Fades the music down to silence. Safe to call more than once.</summary>
    public void FadeOut()
    {
        if (audioSource == null || fadeOutRequested)
        {
            return;
        }

        fadeOutRequested = true;
        IsFadingOut = true;
        BeginFade(0f, fadeOutDuration);
    }

    /// <summary>
    /// Fades the music down to silence across the given duration. Handy when the last
    /// lines of a scene should wind the track down instead of cutting it.
    /// </summary>
    public void FadeOutOver(float duration)
    {
        if (audioSource == null || fadeOutRequested)
        {
            return;
        }

        fadeOutRequested = true;
        IsFadingOut = true;
        BeginFade(0f, Mathf.Max(0.01f, duration));
    }

    /// <summary>
    /// Cuts the music out quickly and pauses it, so it can be brought back on the exact
    /// sample it left. Used for suspense beats mid-scene.
    /// </summary>
    public void QuickFadeOutAndPause(float duration)
    {
        if (audioSource == null)
        {
            return;
        }

        StopQuickFade();
        quickFadeRoutine = StartCoroutine(QuickFadeOutRoutine(Mathf.Max(0.01f, duration)));
    }

    /// <summary>
    /// Resumes music that was paused by <see cref="QuickFadeOutAndPause"/> and fades it
    /// back up to the level it had before the cut.
    /// </summary>
    public void ResumeAfterQuickFade(float duration)
    {
        if (audioSource == null)
        {
            return;
        }

        StopQuickFade();
        quickFadeRoutine = StartCoroutine(ResumeRoutine(Mathf.Max(0.01f, duration)));
    }

    private IEnumerator QuickFadeOutRoutine(float duration)
    {
        // Don't let a normal fade fight this one
        StopFade();

        quickFadeVolume = audioSource.volume > 0f ? audioSource.volume : targetVolume;

        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        audioSource.volume = 0f;

        // Pause, not Stop: the track keeps its playback position for a seamless resume
        audioSource.Pause();
        quickFadePaused = true;
        quickFadeRoutine = null;
    }

    private IEnumerator ResumeRoutine(float duration)
    {
        if (!quickFadePaused)
        {
            // Nothing was cut, so just make sure the music is back at its level
            FadeIn();
            yield break;
        }

        StopFade();
        audioSource.UnPause();

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(0f, quickFadeVolume, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        audioSource.volume = quickFadeVolume;
        quickFadePaused = false;
        quickFadeRoutine = null;
    }

    private void StopQuickFade()
    {
        if (quickFadeRoutine != null)
        {
            StopCoroutine(quickFadeRoutine);
            quickFadeRoutine = null;
        }
    }

    /// <summary>Immediately sets the music to its fade-in level, skipping the fade.</summary>
    public void SnapToFullVolume()
    {
        if (audioSource == null)
        {
            return;
        }

        StopFade();
        fadeOutRequested = false;
        IsFadingOut = false;
        audioSource.volume = targetVolume;
    }

    private IEnumerator WatchFadeoutObject()
    {
        while (!fadeOutRequested)
        {
            if (fadeoutObject == null && !string.IsNullOrEmpty(fadeoutObjectName))
            {
                // GameObject.Find only sees active objects, so a Fadeout that starts
                // inactive must be assigned in the inspector to be caught here.
                fadeoutObject = GameObject.Find(fadeoutObjectName);
            }

            if (fadeoutObject != null && fadeoutObject.activeInHierarchy)
            {
                FadeOut();
                yield break;
            }

            yield return null;
        }
    }

    private void BeginFade(float target, float duration)
    {
        StopFade();

        if (duration <= 0f)
        {
            audioSource.volume = target;
            OnFadeComplete();
            return;
        }

        fadeRoutine = StartCoroutine(FadeRoutine(target, duration));
    }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        float startVolume = audioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Smoothstep eases in and out so the fade never starts or stops abruptly.
            audioSource.volume = Mathf.Lerp(startVolume, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        audioSource.volume = target;
        fadeRoutine = null;
        OnFadeComplete();
    }

    private void OnFadeComplete()
    {
        if (IsFadingOut && stopWhenSilent)
        {
            audioSource.Stop();
        }
    }

    private void StopFade()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
    }

    private void OnDisable()
    {
        StopFade();
        StopQuickFade();
    }
}
