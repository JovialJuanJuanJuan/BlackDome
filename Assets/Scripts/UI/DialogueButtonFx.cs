using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared styling and hover behaviour for the dialogue Next button and choice buttons.
/// The old scene animators are parked so the reveal, hover and press states all come from one
/// place: a dark plate with rounded corners, a cold hairline rim and a short staggered fade when
/// a choice set appears. It is the black reading of RidingBlue's flat rounded button plates — the
/// same shape and rim, lit by the pale accent of Black Dome's palette instead of blue.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum ButtonRole
    {
        Next,
        Choice
    }


    /// <summary>
    /// Choice plates sit a shade above the Next plate so they read as the real decision. Every
    /// value is neutral grey, so the plates stay black instead of picking up a blue cast.
    /// </summary>
    private static readonly Color ChoiceFillTop = new Color(0.135f, 0.135f, 0.14f, 0.95f);
    private static readonly Color ChoiceFillBottom = new Color(0.025f, 0.025f, 0.028f, 0.98f);
    private static readonly Color NextFillTop = new Color(0.105f, 0.105f, 0.11f, 0.95f);
    private static readonly Color NextFillBottom = new Color(0.015f, 0.015f, 0.017f, 0.98f);

    private static readonly Color RimBase = new Color(1f, 1f, 1f, 0.45f);
    private static readonly Color RimHover = new Color(1f, 1f, 1f, 0.9f);

    /// <summary>The plate's own dim tint, so hover can lift it towards white.</summary>
    private static readonly Color FillTint = new Color(0.9f, 0.9f, 0.9f, 1f);

    private const float CornerRadius = 12f;
    private const float BorderThickness = 1f;

    private Button button;
    private Graphic background;
    private RawImage fill;
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
    private bool revealing;
    private float hoverAmount;
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
        hoverAmount = 0f;

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
        hoverAmount = Mathf.MoveTowards(hoverAmount, target, Time.unscaledDeltaTime * 6f);
        ApplyHover();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || button.IsInteractable())
        {
            hovered = true;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    private void CreateFrame()
    {
        // The border is created first so the fill ends up behind it and the label above both.
        border = DialogueGradient.EnsureFill(transform, "DialogueFX_Border");
        fill = DialogueGradient.EnsureFill(transform, "DialogueFX_Gradient");

        fill.transform.SetSiblingIndex(0);
        border.transform.SetSiblingIndex(1);
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
        Color top = role == ButtonRole.Next ? NextFillTop : ChoiceFillTop;
        Color bottom = role == ButtonRole.Next ? NextFillBottom : ChoiceFillBottom;

        ReleaseTextures();

        fillTexture = DialogueGradient.CreateRoundedFill(size, radius, top, bottom);
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
        labelHover = Color.white;

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

        if (border != null)
        {
            border.color = borderBase;
        }

        if (label != null)
        {
            label.color = labelBase;
        }

        hoverAmount = 0f;
        ApplyHover();
    }

    private void ApplyHover()
    {
        if (fill != null)
        {
            fill.color = Color.Lerp(FillTint, Color.white, hoverAmount);
        }

        if (border != null)
        {
            border.color = Color.Lerp(borderBase, borderHover, hoverAmount);
        }

        if (label != null)
        {
            label.color = Color.Lerp(labelBase, labelHover, hoverAmount);
        }
    }
}
