using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// The "PRESS ANY KEY" line at the bottom of the title screen.
///
/// It waits for the opening fade to clear, fades in with wide letter spacing and then
/// breathes gently so it never looks frozen. The first key press (or click, if allowed)
/// fades it out, brings the menu labels in and hands focus to the first one, so the arrow
/// keys and Enter take over from there.
///
/// The line is built at runtime under the canvas it is pointed at, which keeps the menu
/// scene free of another hand-placed text object.
/// </summary>
[DisallowMultipleComponent]
public class PressAnyKeyPrompt : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("Canvas the prompt is drawn on. Drag the scene Canvas here.")]
    [SerializeField] private RectTransform container;
    [Tooltip("Optional. Receives focus for the first label when the prompt is dismissed.")]
    [SerializeField] private MenuSelectionBar selectionBar;
    [Tooltip("Optional. Brings the menu labels in once the prompt is answered.")]
    [SerializeField] private MenuReveal menuReveal;
    [Tooltip("Font for the prompt. Leave empty to use the TextMeshPro default.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Prompt")]
    [SerializeField] private string message = "PRESS ANY KEY";
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField, Range(8f, 64f)] private float fontSize = 26f;
    [Tooltip("Letter spacing, in the same units as the TextMeshPro inspector slider.")]
    [SerializeField, Range(0f, 40f)] private float letterSpacing = 20f;
    [Tooltip("Distance of the line from the bottom edge of the screen.")]
    [SerializeField, Range(0f, 200f)] private float bottomOffset = 42f;

    [Header("Timing")]
    [Tooltip("Seconds to wait before the prompt appears, so it shows up after the opening fade.")]
    [SerializeField, Range(0f, 6f)] private float appearDelay = 2.2f;
    [SerializeField, Range(0.05f, 2f)] private float fadeTime = 0.45f;
    [Tooltip("Seconds for one full breath of the prompt.")]
    [SerializeField, Range(1f, 8f)] private float breathPeriod = 3.2f;
    [SerializeField, Range(0f, 1f)] private float breathMinAlpha = 0.35f;
    [Tooltip("Seconds after the key press before the first label takes focus, so the plate lands as the menu appears.")]
    [SerializeField, Range(0f, 2f)] private float focusDelay = 0.35f;
    [Tooltip("Also dismiss on a mouse click or a gamepad button.")]
    [SerializeField] private bool acceptMouseClick = true;

    private TMP_Text _text;
    private float _appearAt;
    private bool _dismissed;

    private void Start()
    {
        Build();
        _appearAt = Time.unscaledTime + appearDelay;
        StartCoroutine(Breathe());
    }

    private void Update()
    {
        if (_dismissed || !AnyInputThisFrame())
        {
            return;
        }

        _dismissed = true;

        if (menuReveal != null)
        {
            menuReveal.Reveal();
        }

        StartCoroutine(HandOver());
    }

    /// <summary>
    /// Focus the first label once it has appeared, so the plate slides in with the row.
    /// Anything the player already picked wins.
    /// </summary>
    private IEnumerator HandOver()
    {
        yield return new WaitForSecondsRealtime(focusDelay);

        if (selectionBar == null)
        {
            yield break;
        }

        EventSystem system = EventSystem.current;
        if (system != null && system.currentSelectedGameObject != null)
        {
            yield break;
        }

        selectionBar.SelectDefault();
    }

    // ---------------------------------------------------------------- setup

    private void Build()
    {
        var promptObject = new GameObject("PressAnyKeyPrompt", typeof(RectTransform));
        var rect = (RectTransform)promptObject.transform;

        // Sit in the menu's own layer, just above the background, so the black fade
        // overlays still cover the prompt on the way out of the scene.
        RectTransform parent = selectionBar != null ? selectionBar.Layer : null;
        if (parent == null)
        {
            parent = container;
        }

        rect.SetParent(parent != null ? parent : transform, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, bottomOffset);
        rect.sizeDelta = new Vector2(900f, 48f);

        _text = promptObject.AddComponent<TextMeshProUGUI>();

        if (font != null)
        {
            _text.font = font;
            _text.fontSharedMaterial = font.material;
        }

        _text.text = message;
        _text.fontSize = fontSize;
        _text.characterSpacing = letterSpacing;
        _text.alignment = TextAlignmentOptions.Center;
        _text.color = WithAlpha(0f);

        // The prompt must never swallow the pointer: the menu buttons sit above it anyway.
        _text.raycastTarget = false;
    }

    // ---------------------------------------------------------------- presentation

    private IEnumerator Breathe()
    {
        SetAlpha(0f);

        float elapsed = 0f;

        while (!_dismissed)
        {
            // Unscaled time, so the prompt keeps moving if the game pauses with timeScale = 0.
            elapsed += Time.unscaledDeltaTime;

            float intro = Mathf.Clamp01((elapsed - appearDelay) / Mathf.Max(fadeTime, 0.0001f));
            float wave = Mathf.Sin(elapsed * Mathf.PI * 2f / Mathf.Max(breathPeriod, 0.01f)) * 0.5f + 0.5f;
            float breath = Mathf.Lerp(breathMinAlpha, 1f, wave);
            float smooth = intro * intro * (3f - 2f * intro);

            SetAlpha(Mathf.Lerp(0f, breath, smooth));
            yield return null;
        }

        // Dismissed: wind the alpha down from wherever the breath left it.
        float start = _text != null && color.a > 0f ? _text.color.a / color.a : 0f;
        float faded = 0f;

        while (faded < fadeTime)
        {
            faded += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, 0f, Mathf.Clamp01(faded / Mathf.Max(fadeTime, 0.0001f))));
            yield return null;
        }

        SetAlpha(0f);

        if (_text != null)
        {
            _text.gameObject.SetActive(false);
        }
    }

    private void SetAlpha(float alpha01)
    {
        if (_text == null)
        {
            return;
        }

        _text.color = WithAlpha(color.a * Mathf.Clamp01(alpha01));
    }

    private Color WithAlpha(float alpha)
    {
        Color tinted = color;
        tinted.a = alpha;
        return tinted;
    }

    // ---------------------------------------------------------------- input

    private bool AnyInputThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
        {
            return true;
        }

        if (!acceptMouseClick)
        {
            return false;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null &&
               (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame);
#else
        return Input.anyKeyDown;
#endif
    }
}
