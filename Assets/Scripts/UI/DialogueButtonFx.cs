using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared styling, hover and press behaviour for the dialogue Next button and choice buttons.
/// The old scene animators are parked so the reveal, hover and press states all come from one
/// place: a dark plate with rounded corners, a cold hairline rim and a short staggered fade when
/// a choice set appears. The plates are cut from the dialogue card's own gradient and rim, so the
/// buttons read as small pieces of the box they sit under: hovering washes a little light across
/// the plate and firms its white outline, and clicking dips that light and shrinks the plate a
/// hair under the cursor. A click also plays the scene's Next button click, so choosing an answer
/// sounds exactly like advancing the dialogue.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public enum ButtonRole
    {
        Next,
        Choice
    }


    /// <summary>
    /// The Next plate is cut from the dialogue card itself — the same neutral gradient, the same
    /// hairline rim — so it reads as a small piece of the box it sits under. The choice plates sit
    /// a shade deeper, close to black, but keep a wide enough ramp that the gradient still reads.
    /// </summary>
    private static readonly Color NextPlateTop = new Color(0.055f, 0.055f, 0.055f, 0.945f);
    private static readonly Color NextPlateBottom = new Color(0.004f, 0.004f, 0.004f, 0.995f);
    private static readonly Color ChoicePlateTop = new Color(0.034f, 0.034f, 0.034f, 0.94f);
    private static readonly Color ChoicePlateBottom = new Color(0.002f, 0.002f, 0.002f, 0.99f);

    /// <summary>A thin but definite white outline: the card's hairline, drawn a little firmer.</summary>
    private static readonly Color RimBase = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color RimHover = Color.white;
    private static readonly Color RimPress = Color.white;

    /// <summary>
    /// A plate this dark cannot be dimmed visibly, so the hover response is a thin white wash laid
    /// over it: the plate catches the light on hover and drops back when pressed, which reads as
    /// the button sitting up under the pointer and sinking away under the click.
    /// </summary>
    private const float HoverSheen = 0.075f;
    private const float PressSheen = 0.02f;

    private static readonly Color FillTint = Color.white;

    /// <summary>Caption tints that follow the plate, so the label reacts with the surface.</summary>
    private static readonly Color LabelHoverTint = new Color(0.92f, 0.92f, 0.92f, 1f);
    private static readonly Color LabelPressTint = new Color(0.76f, 0.76f, 0.76f, 1f);

    private const float CornerRadius = 12f;
    private const float BorderThickness = 1f;

    private const float HoverFadeSpeed = 6f;
    private const float PressFadeSpeed = 18f;
    private const float PressedScale = 0.975f;

    // The hover blip is shared by every choice plate, so sweeping down the stack plays one sound
    // per plate instead of stacking them, and the clip is loaded once for the whole session.
    private const string HoverClipResource = "Audio/hoverbutton";
    private const float HoverSoundVolume = 0.5f;
    private const float HoverSoundCooldown = 0.06f;

    private static AudioSource hoverAudio;
    private static AudioClip hoverClip;
    private static bool hoverClipLoaded;
    private static float lastHoverSoundTime = float.NegativeInfinity;

    // The choice plates borrow the click the Next button already plays, so both sounds come from
    // one configured source instead of two clips that only sound almost the same.
    private static NextButtonClickSound nextClickSound;

    private Button button;
    private Graphic background;
    private RawImage fill;
    private RawImage sheen;
    private RawImage border;
    private TMP_Text label;
    private CanvasGroup canvasGroup;

    private Texture2D fillTexture;
    private Texture2D borderTexture;
    private Vector2 frameSize;

    private ButtonRole role;
    private int order;
    private bool initialized;
    private bool hovered;
    private bool pressed;
    private bool revealing;
    private float hoverAmount;
    private float pressAmount;
    private float revealElapsed;
    private float revealDelay;

    private Color borderBase;
    private Color borderHover;
    private Color labelBase;
    private Color labelHover;

    /// <summary>
    /// Applies the role's styling and prepares the staggered reveal. Safe to call more than
    /// once: the generated frame is only built the first time.
    /// </summary>
    public void Initialize(ButtonRole buttonRole, int index)
    {
        role = buttonRole;
        order = index;

        button = GetComponent<Button>();
        background = GetComponent<Graphic>();
        label = GetComponentInChildren<TMP_Text>(true);
        canvasGroup = GetComponent<CanvasGroup>();

        if (button != null)
        {
            // Some choice buttons ship with a near-black Selectable tint, which would
            // multiply the new dark fill into black and hide the hover response. Keep the
            // tint neutral and let this component own the colour changes.
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false;
        }

        if (!initialized)
        {
            CreateFrame();
            initialized = true;
        }

        ApplyStyle();
        RefreshFrame();

        if (isActiveAndEnabled)
        {
            BeginReveal();
        }
    }

    private void OnEnable()
    {
        if (initialized)
        {
            RefreshFrame();
            BeginReveal();
        }
    }

    private void OnDisable()
    {
        revealing = false;
        hovered = false;
        pressed = false;
        hoverAmount = 0f;
        pressAmount = 0f;

        RectTransform rect = transform as RectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void OnDestroy()
    {
        ReleaseTextures();
    }

    private void BeginReveal()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        revealElapsed = 0f;
        revealDelay = role == ButtonRole.Choice ? order * 0.07f : 0f;
        revealing = true;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        RefreshFrame();

        if (revealing)
        {
            revealElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01((revealElapsed - revealDelay) / 0.22f);
            canvasGroup.alpha = 1f - Mathf.Pow(1f - progress, 3f);

            if (progress >= 1f)
            {
                canvasGroup.alpha = 1f;
                revealing = false;
            }
        }

        float target = hovered ? 1f : 0f;
        hoverAmount = Mathf.MoveTowards(hoverAmount, target, Time.unscaledDeltaTime * HoverFadeSpeed);

        float pressTarget = pressed ? 1f : 0f;
        pressAmount = Mathf.MoveTowards(pressAmount, pressTarget, Time.unscaledDeltaTime * PressFadeSpeed);

        ApplyHover();
        ApplyPress();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.IsInteractable())
        {
            return;
        }

        hovered = true;

        if (role == ButtonRole.Choice)
        {
            PlayHoverSound();
        }
    }

    /// <summary>
    /// Plays the shared hover blip on a dedicated 2D source. The source and the clip are static, so
    /// the three plates never play over each other and nothing is loaded twice.
    /// </summary>
    private static void PlayHoverSound()
    {
        if (!hoverClipLoaded)
        {
            hoverClip = Resources.Load<AudioClip>(HoverClipResource);
            hoverClipLoaded = true;

            if (hoverClip == null)
            {
                Debug.LogWarning(
                    $"DialogueButtonFx: no hover clip found at Resources/{HoverClipResource}.");
                return;
            }
        }

        if (hoverClip == null || Time.unscaledTime - lastHoverSoundTime < HoverSoundCooldown)
        {
            return;
        }

        if (hoverAudio == null)
        {
            GameObject host = new GameObject("DialogueHoverAudio");
            hoverAudio = host.AddComponent<AudioSource>();
            hoverAudio.playOnAwake = false;
            hoverAudio.spatialBlend = 0f;
            hoverAudio.volume = HoverSoundVolume;
        }

        lastHoverSoundTime = Time.unscaledTime;
        hoverAudio.pitch = Random.Range(0.97f, 1.03f);
        hoverAudio.PlayOneShot(hoverClip, HoverSoundVolume);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.IsInteractable())
        {
            return;
        }

        pressed = true;

        // Snap part of the dip in on the press frame: a click on a choice plate often hides the
        // buttons as soon as the mouse comes back up, so the feedback has to land immediately.
        pressAmount = Mathf.Max(pressAmount, 0.6f);

        if (role == ButtonRole.Choice)
        {
            PlayChoiceClick();
        }
    }

    /// <summary>
    /// Plays the Next button's own click for a choice, through the component the scene already
    /// uses for it, so volume, pitch variation and anti-spam all match the rest of the dialogue.
    /// The reference is only looked up when missing, which also survives a scene load.
    /// </summary>
    private static void PlayChoiceClick()
    {
        if (nextClickSound == null)
        {
            nextClickSound = FindFirstObjectByType<NextButtonClickSound>(FindObjectsInactive.Exclude);
        }

        if (nextClickSound != null)
        {
            nextClickSound.PlayClickSound();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    private void CreateFrame()
    {
        // Bottom to top: the dark plate, the hover wash laid over it, then the rim, with the
        // caption the scene authored left above all three.
        fill = DialogueGradient.EnsureFill(transform, "DialogueFX_Gradient");
        sheen = DialogueGradient.EnsureFill(transform, "DialogueFX_Sheen", Texture2D.whiteTexture);
        border = DialogueGradient.EnsureFill(transform, "DialogueFX_Border");

        fill.transform.SetSiblingIndex(0);
        sheen.transform.SetSiblingIndex(1);
        border.transform.SetSiblingIndex(2);
    }

    /// <summary>
    /// The rounded fill and outline are baked at the button's current size, so they are
    /// rebuilt whenever layout changes the rect. Sizes are stable in game, so this normally
    /// runs once per button.
    /// </summary>
    private void RefreshFrame()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null || fill == null || border == null)
        {
            return;
        }

        Vector2 size = rect.rect.size;
        if (size.x < 2f || size.y < 2f)
        {
            return;
        }

        if (fillTexture != null
            && Mathf.Abs(size.x - frameSize.x) < 1f
            && Mathf.Abs(size.y - frameSize.y) < 1f)
        {
            return;
        }

        frameSize = size;
        float radius = Mathf.Min(CornerRadius, size.y * 0.5f);
        bool isNext = role == ButtonRole.Next;

        ReleaseTextures();

        fillTexture = DialogueGradient.CreateRoundedFill(
            size,
            radius,
            isNext ? NextPlateTop : ChoicePlateTop,
            isNext ? NextPlateBottom : ChoicePlateBottom);

        // One pixel, laid on the same rounded rectangle as the fill: the thin white outline.
        borderTexture = DialogueGradient.CreateRoundedBorder(size, radius, BorderThickness, Color.white);

        fill.texture = fillTexture;
        border.texture = borderTexture;

        ApplyHover();
    }

    private void ReleaseTextures()
    {
        DestroyTexture(fillTexture);
        DestroyTexture(borderTexture);
        fillTexture = null;
        borderTexture = null;
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }
    }

    private void ApplyStyle()
    {
        borderBase = RimBase;
        borderHover = RimHover;
        labelBase = Color.white;
        labelHover = LabelHoverTint;

        // The caption is left exactly as the scene authored it, so only its typography is set:
        // RidingBlue's 48pt bold Next over a 50pt choice, both centred in their plate.
        if (label != null)
        {
            bool isNext = role == ButtonRole.Next;
            label.fontSize = isNext ? 48f : 50f;
            label.enableAutoSizing = false;
            label.fontStyle = isNext ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = 0f;
            label.lineSpacing = 0f;
            label.margin = Vector4.zero;
            label.raycastTarget = false;
        }

        if (background != null)
        {
            background.color = new Color(1f, 1f, 1f, 0f);
            background.raycastTarget = true;
        }

        if (fill != null)
        {
            fill.color = FillTint;
        }

        if (sheen != null)
        {
            sheen.color = new Color(1f, 1f, 1f, 0f);
        }

        if (border != null)
        {
            border.color = borderBase;
        }

        if (label != null)
        {
            label.color = labelBase;
        }

        hoverAmount = 0f;
        pressAmount = 0f;
        ApplyHover();
        ApplyPress();
    }

    /// <summary>
    /// Hover lifts a thin white wash across the plate and firms the outline; a press pulls the wash
    /// back down and shrinks the plate, so the click reads as the button being pushed in.
    /// </summary>
    private void ApplyHover()
    {
        if (fill != null)
        {
            fill.color = FillTint;
        }

        if (sheen != null)
        {
            float wash = Mathf.Lerp(
                Mathf.Lerp(0f, HoverSheen, hoverAmount),
                PressSheen,
                pressAmount);
            sheen.color = new Color(1f, 1f, 1f, wash);
        }

        if (border != null)
        {
            Color hovered = Color.Lerp(borderBase, borderHover, hoverAmount);
            border.color = Color.Lerp(hovered, RimPress, pressAmount);
        }

        if (label != null)
        {
            Color hovered = Color.Lerp(labelBase, labelHover, hoverAmount);
            label.color = Color.Lerp(hovered, LabelPressTint, pressAmount);
        }
    }

    /// <summary>The plate gives slightly under the cursor, which is what sells the click.</summary>
    private void ApplyPress()
    {
        RectTransform rect = transform as RectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one * (1f - (1f - PressedScale) * pressAmount);
        }
    }
}
