using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drop this component on an empty GameObject (e.g. "MainMenu") and drag your four
/// menu buttons into the slots below. It gives them the following and nothing else:
///
///  1. A subtle hover animation: an ease-out scale pop plus a gentle brightness lift on
///     pointer enter, smoothly wound back on pointer exit.
///  2. One shared hover sound, played through an AudioSource it adds to itself.
///  3. An optional description line per button (e.g. "StartGame_Desc"): it is switched on and
///     faded in while its button is hovered, then faded back out and switched off.
///
/// Click handling is left entirely to your own Button.onClick wiring.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [Header("Menu Buttons")]
    [Tooltip("The four menu buttons. Any slot can be left empty if you do not use that button.")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Hover Descriptions")]
    [Tooltip("Text object revealed while the matching button is hovered. Any slot can be left empty.")]
    [SerializeField] private TMP_Text startDescription;
    [SerializeField] private TMP_Text loadDescription;
    [SerializeField] private TMP_Text creditsDescription;
    [SerializeField] private TMP_Text quitDescription;
    [Tooltip("Seconds a description takes to fade in on hover and back out when the pointer leaves.")]
    [SerializeField, Range(0.05f, 1f)] private float descriptionFadeTime = 0.3f;

    [Header("Hover Animation")]
    [Tooltip("Scale the button grows to while hovered. Keep it close to 1 for a subtle feel.")]
    [SerializeField, Range(1f, 1.25f)] private float hoverScale = 1.05f;
    [Tooltip("How much brighter the button gets while hovered (1 = no change).")]
    [SerializeField, Range(1f, 2f)] private float hoverBrightness = 1.18f;
    [Tooltip("Seconds the button takes to grow when hovered.")]
    [SerializeField, Range(0.01f, 0.5f)] private float hoverEnterTime = 0.12f;
    [Tooltip("Seconds the button takes to settle back when the pointer leaves.")]
    [SerializeField, Range(0.01f, 0.5f)] private float hoverExitTime = 0.09f;

    [Header("Hover Sound")]
    [Tooltip("Single clip shared by all four buttons.")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.6f;
    [Tooltip("Minimum seconds between two hover sounds, so sweeping the mouse across the menu does not stack them.")]
    [SerializeField, Range(0f, 0.5f)] private float hoverSoundCooldown = 0.05f;

    /// <summary>Per-button visual state, cached at start so animations never drift.</summary>
    private sealed class ButtonVisual
    {
        public Button button;
        public Graphic graphic;
        public Color baseColor;
        public Vector3 baseScale;
        public float hoverAmount;   // 0 = idle, 1 = fully hovered
        public Coroutine animation;
    }

    /// <summary>Per-description state, so a fade can be interrupted mid-way without popping.</summary>
    private sealed class DescriptionVisual
    {
        public TMP_Text text;
        public GameObject root;
        public Color baseColor;
        public Coroutine fade;
    }

    private readonly List<ButtonVisual> _visuals = new List<ButtonVisual>();
    private readonly Dictionary<Button, DescriptionVisual> _descriptions =
        new Dictionary<Button, DescriptionVisual>();
    private AudioSource _audio;
    private float _lastHoverSoundTime = float.NegativeInfinity;

    private void Awake()
    {
        // A 2D audio source is all a UI hover sound needs.
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
        }

        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 0f;

        CacheButton(startButton);
        CacheButton(loadButton);
        CacheButton(creditsButton);
        CacheButton(quitButton);

        CacheDescription(startButton, startDescription);
        CacheDescription(loadButton, loadDescription);
        CacheDescription(creditsButton, creditsDescription);
        CacheDescription(quitButton, quitDescription);
    }

    private void OnDisable()
    {
        // Coroutines stop with the component, so put the buttons back to their idle look.
        ResetVisuals();
    }

    // ---------------------------------------------------------------- setup

    private void CacheButton(Button button)
    {
        if (button == null || _visuals.Exists(v => v.button == button))
        {
            return;
        }

        Graphic target = button.targetGraphic;

        var visual = new ButtonVisual
        {
            button = button,
            graphic = target,
            baseColor = target != null ? target.color : Color.white,
            baseScale = button.transform.localScale,
            hoverAmount = 0f,
        };

        _visuals.Add(visual);
        RegisterHoverEvents(visual);
        ApplyVisual(visual);
    }

    private void CacheDescription(Button button, TMP_Text text)
    {
        if (button == null || text == null || _descriptions.ContainsKey(button))
        {
            return;
        }

        // The descriptions sit over the menu, so they must never swallow pointer events.
        text.raycastTarget = false;

        // Alpha is driven here instead of by an Animator, so the fade in and the fade out
        // can share one curve and be interrupted at any point without fighting the clip.
        Animator animator = text.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }

        var description = new DescriptionVisual
        {
            text = text,
            root = text.gameObject,
            baseColor = text.color,
        };

        _descriptions.Add(button, description);
        SetDescriptionAlpha(description, 0f);
        description.root.SetActive(false);
    }

    /// <summary>
    /// Hover is read through an EventTrigger so the buttons keep their own
    /// click handling and inspector wiring untouched.
    /// </summary>
    private void RegisterHoverEvents(ButtonVisual visual)
    {
        EventTrigger trigger = visual.button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = visual.button.gameObject.AddComponent<EventTrigger>();
        }

        AddEntry(trigger, EventTriggerType.PointerEnter, () => HandleHover(visual, true));
        AddEntry(trigger, EventTriggerType.PointerExit, () => HandleHover(visual, false));
    }

    private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityAction callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => callback());
        trigger.triggers.Add(entry);
    }

    // ---------------------------------------------------------------- hover

    private void HandleHover(ButtonVisual visual, bool isHovered)
    {
        if (visual == null)
        {
            return;
        }

        if (isHovered)
        {
            if (!visual.button.IsInteractable())
            {
                return;
            }

            PlayHoverSound();
        }

        DescriptionVisual description;
        if (_descriptions.TryGetValue(visual.button, out description))
        {
            ShowDescription(description, isHovered);
        }

        if (visual.animation != null)
        {
            StopCoroutine(visual.animation);
        }

        float target = isHovered ? 1f : 0f;
        float duration = isHovered ? hoverEnterTime : hoverExitTime;
        visual.animation = StartCoroutine(AnimateHover(visual, target, duration));
    }

    private IEnumerator AnimateHover(ButtonVisual visual, float target, float duration)
    {
        duration = Mathf.Max(duration, 0.0001f);

        float start = visual.hoverAmount;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Unscaled time, so the menu still animates if the game pauses with timeScale = 0.
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out, feels snappy but smooth
            visual.hoverAmount = Mathf.LerpUnclamped(start, target, eased);

            ApplyVisual(visual);
            yield return null;
        }

        visual.hoverAmount = target;
        visual.animation = null;
        ApplyVisual(visual);
    }

    // ---------------------------------------------------------------- descriptions

    private void ShowDescription(DescriptionVisual description, bool visible)
    {
        if (description.fade != null)
        {
            StopCoroutine(description.fade);
            description.fade = null;
        }

        if (description.root == null)
        {
            return;
        }

        if (visible)
        {
            // Switch the object on first; it is already transparent, so nothing flashes.
            description.root.SetActive(true);
        }

        description.fade = StartCoroutine(FadeDescription(description, visible ? 1f : 0f, visible));
    }

    private IEnumerator FadeDescription(DescriptionVisual description, float target, bool stayActive)
    {
        float duration = Mathf.Max(descriptionFadeTime, 0.0001f);
        float start = description.baseColor.a > 0f
            ? description.text.color.a / description.baseColor.a
            : 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f); // same ease-out as the hover pop
            SetDescriptionAlpha(description, Mathf.LerpUnclamped(start, target, eased));
            yield return null;
        }

        SetDescriptionAlpha(description, target);
        description.fade = null;

        if (!stayActive && description.root != null)
        {
            description.root.SetActive(false);
        }
    }

    private static void SetDescriptionAlpha(DescriptionVisual description, float alpha01)
    {
        // The menu may already be tearing down (scene unload, play mode exit) when a fade or
        // the disable pass still runs, so the text can be a destroyed object by now.
        if (description.text == null)
        {
            return;
        }

        Color color = description.baseColor;
        color.a = description.baseColor.a * Mathf.Clamp01(alpha01);
        description.text.color = color;
    }

    private void ApplyVisual(ButtonVisual visual)
    {
        float t = Mathf.Clamp01(visual.hoverAmount);

        if (visual.button != null)
        {
            // Multiplicative so buttons with a custom base scale keep their proportions.
            visual.button.transform.localScale =
                Vector3.LerpUnclamped(visual.baseScale, visual.baseScale * hoverScale, t);
        }

        if (visual.graphic != null)
        {
            // Brighten by scaling the colour, and keep the original alpha.
            Color hovered = visual.baseColor * hoverBrightness;
            hovered.a = visual.baseColor.a;

            visual.graphic.color = Color.LerpUnclamped(visual.baseColor, hovered, t);
        }
    }

    private void ResetVisuals()
    {
        for (int i = 0; i < _visuals.Count; i++)
        {
            ButtonVisual visual = _visuals[i];

            if (visual.animation != null)
            {
                StopCoroutine(visual.animation);
                visual.animation = null;
            }

            visual.hoverAmount = 0f;
            ApplyVisual(visual);
        }

        // Coroutines stop with the component, so hide any description that was mid-fade.
        foreach (DescriptionVisual description in _descriptions.Values)
        {
            if (description.fade != null)
            {
                StopCoroutine(description.fade);
                description.fade = null;
            }

            SetDescriptionAlpha(description, 0f);

            if (description.root != null)
            {
                description.root.SetActive(false);
            }
        }
    }

    private void PlayHoverSound()
    {
        if (hoverSound == null || _audio == null)
        {
            return;
        }

        if (Time.unscaledTime - _lastHoverSoundTime < hoverSoundCooldown)
        {
            return;
        }

        _lastHoverSoundTime = Time.unscaledTime;
        _audio.PlayOneShot(hoverSound, hoverVolume);
    }
}
