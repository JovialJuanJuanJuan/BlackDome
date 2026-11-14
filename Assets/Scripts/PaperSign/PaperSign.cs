using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PaperSign : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] GameObject fadeOut;

    [Header("Sign Area")]
    [SerializeField] GameObject signArea;
    [SerializeField] GameObject whitescreen;
    [SerializeField] GameObject paper01;
    [SerializeField] GameObject paper02;

    [Header("Sign Area Timing")]
    [Tooltip("Seconds for one full transparent -> opaque -> transparent pulse of the sign area.")]
    [SerializeField] float signPulseDuration = 1.2f;

    [Tooltip("How long the white screen stays fully opaque before it starts fading out.")]
    [SerializeField] float whitescreenHold = 3f;

    [Tooltip("Seconds the white screen takes to fade once the hold ends.")]
    [SerializeField] float whitescreenFadeDuration = 1.5f;

    [Tooltip("Pause after the white screen has faded before the black fade out starts.")]
    [SerializeField] float pauseAfterWhitescreen = 5f;

    [Tooltip("Seconds the black fade out holds before the next scene is loaded.")]
    [SerializeField] float fadeOutHold = 3f;

    [Header("Audio")]
    [Tooltip("Scene music. It is faded out as soon as the sign is clicked.")]
    [SerializeField] BackgroundMusicFader bgm;

    [Tooltip("Source the sign's sounds are played from. Left on the scene's own effects source.")]
    [SerializeField] AudioSource uiAudioSource;

    [Tooltip("Played when the pointer moves onto the sign area.")]
    [SerializeField] AudioClip hoverClip;
    [SerializeField, Range(0f, 1f)] float hoverVolume = 0.6f;

    [Tooltip("Played the moment the sign is clicked.")]
    [SerializeField] AudioClip signClickClip;
    [SerializeField, Range(0f, 1f)] float signClickVolume = 1f;

    RawImage signImage;
    Button signButton;
    float signPulseTimer;
    float signStartAlpha;
    bool signClicked;
    bool signHovered;

    void Awake()
    {
        if (signArea == null)
        {
            return;
        }

        signImage = signArea.GetComponent<RawImage>();
        signButton = signArea.GetComponent<Button>();

        // Start the pulse from the alpha the area was authored with, so it does not jump.
        if (signImage != null)
        {
            signStartAlpha = signImage.color.a;
        }
    }

    void Update()
    {
        // The sign area keeps pulsing here while it waits to be clicked, as a cue to the player.
        if (signClicked || signHovered || signImage == null)
        {
            return;
        }

        signPulseTimer += Time.deltaTime;

        // PingPong from the authored alpha keeps the first frame identical to the scene.
        float alpha = Mathf.PingPong(
            signStartAlpha + signPulseTimer / Mathf.Max(0.01f, signPulseDuration), 1f);

        SetSignAlpha(alpha);
    }

    // =====================================================
    // SIGN AREA
    // =====================================================

    /// <summary>Called by SignAreaHover. Holds the area fully solid and plays the hover blip.</summary>
    public void SignAreaHoverEnter()
    {
        if (signClicked || signImage == null)
        {
            return;
        }

        signHovered = true;
        SetSignAlpha(1f);

        if (uiAudioSource != null && hoverClip != null)
        {
            uiAudioSource.PlayOneShot(hoverClip, hoverVolume);
        }
    }

    /// <summary>Called by SignAreaHover. The pulse picks up again from fully solid.</summary>
    public void SignAreaHoverExit()
    {
        if (signClicked || signImage == null)
        {
            return;
        }

        signHovered = false;

        // Fully opaque is the top of the pulse, so resuming from there carries on downward.
        signPulseTimer = (1f - signStartAlpha) * Mathf.Max(0.01f, signPulseDuration);
    }

    /// <summary>Wired to the SignArea button. Swaps the papers under a white screen, then fades out.</summary>
    public void SignAreaClicked()
    {
        if (signClicked)
        {
            return;
        }

        signClicked = true;

        // The music winds down while the scene leaves the sign behind.
        if (bgm != null)
        {
            bgm.FadeOut();
        }

        if (uiAudioSource != null && signClickClip != null)
        {
            uiAudioSource.PlayOneShot(signClickClip, signClickVolume);
        }

        if (signButton != null)
        {
            signButton.interactable = false;
        }

        StartCoroutine(SignAreaSequence());
    }

    IEnumerator SignAreaSequence()
    {
        // The white screen goes up first and covers the swap, so the change is never seen.
        if (whitescreen != null)
        {
            whitescreen.SetActive(true);
        }

        if (paper01 != null)
        {
            paper01.SetActive(false);
        }

        if (paper02 != null)
        {
            paper02.SetActive(true);
        }

        // The prompt is hidden under the white screen and stops pulsing.
        if (signImage != null)
        {
            signImage.enabled = false;
        }

        yield return new WaitForSeconds(whitescreenHold);
        yield return StartCoroutine(FadeWhitescreenOut());

        // Let the second paper sit on screen before the black fade out.
        yield return new WaitForSeconds(pauseAfterWhitescreen);

        if (fadeOut != null)
        {
            fadeOut.SetActive(true);
        }

        yield return new WaitForSeconds(fadeOutHold);
        yield return SceneManager.LoadSceneAsync(4);
    }

    IEnumerator FadeWhitescreenOut()
    {
        if (whitescreen == null)
        {
            yield break;
        }

        RawImage whiteImage = whitescreen.GetComponent<RawImage>();

        if (whiteImage == null)
        {
            whitescreen.SetActive(false);
            yield break;
        }

        float duration = Mathf.Max(0.01f, whitescreenFadeDuration);
        float startAlpha = whiteImage.color.a;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            SetAlpha(whiteImage, Mathf.Lerp(startAlpha, 0f, timer / duration));

            yield return null;
        }

        SetAlpha(whiteImage, 0f);

        // A fully transparent RawImage still catches raycasts, so it is switched off.
        whitescreen.SetActive(false);
    }

    void SetSignAlpha(float alpha)
    {
        SetAlpha(signImage, alpha);
    }

    static void SetAlpha(RawImage image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
