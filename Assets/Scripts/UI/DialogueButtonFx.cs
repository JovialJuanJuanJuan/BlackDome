using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared styling, hover and press behaviour for the dialogue Next button and choice buttons.
/// The old scene animators are parked so the reveal, hover and press states all come from one
/// place: slanted slabs, the same ones the pause menu is built from. The Next plate is inverted —
/// a white slab with black tracked type, like the pause menu's BACK plate — and the choice plates
/// are near-black slabs with a white cut edge. Hovering firms the edge or lifts the plate's tone,
/// clicking dips it and shrinks the plate a hair under the cursor. A click also plays the scene's
/// Next button click, so choosing an answer sounds exactly like advancing the dialogue.
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
    /// The choice plates are the pause menu's cards in miniature: a near-black face with a white
    /// cut edge behind it. The Next plate flips that around, white with black type, the way the
    /// menu's BACK plate does.
    /// </summary>
    private static readonly Color PlateFace = new Color(0.012f, 0.012f, 0.013f, 1f);
    private static readonly Color PlateEdgeIdle = new Color(1f, 1f, 1f, 0.42f);
    private static readonly Color PlateEdgeHover = new Color(1f, 1f, 1f, 1f);

    private static readonly Color InvertedIdle = new Color(0.92f, 0.92f, 0.93f, 1f);
    private static readonly Color InvertedHover = new Color(0.80f, 0.80f, 0.81f, 1f);
    private static readonly Color InvertedPress = new Color(0.70f, 0.70f, 0.71f, 1f);
    private static readonly Color TagInk = new Color(0.05f, 0.05f, 0.05f, 1f);

    /// <summary>A plate this dark cannot be dimmed visibly, so a choice's hover response is a thin
    /// white wash laid over it: it catches the light on hover and drops back when pressed.</summary>
    private const float HoverSheen = 0.10f;
    private const float PressSheen = 0.02f;

    /// <summary>Caption tints that follow the plate, so the label reacts with the surface.</summary>
    private static readonly Color LabelHoverTint = new Color(0.92f, 0.92f, 0.92f, 1f);
    private static readonly Color LabelPressTint = new Color(0.76f, 0.76f, 0.76f, 1f);

    private const float SlabSlant = 14f;
    private const float SlabGrow = 3f;

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
    private SkewedPanel edge;
    private SkewedPanel face;
    private SkewedPanel sheen;
    private TMP_Text label;
    private CanvasGroup canvasGroup;

    private ButtonRole role;
    private int order;
    private bool initialized;
    private bool hovered;
    private bool pressed;
    private bool revealing;
    private bool fadingOut;
    private float hoverAmount;
    private float pressAmount;
    private float revealElapsed;
    private float revealDelay;

    private Color labelBase;
    private Color labelHover;
    private Color labelPress;
    private Color faceIdle;
    private Color faceHover;
    private Color facePress;
    private Color edgeIdle;
    private Color edgeHover;

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

        if (isActiveAndEnabled)
        {
            BeginReveal();
        }
    }

    private void OnEnable()
    {
        if (initialized)
        {
            BeginReveal();
        }
    }

    private void OnDisable()
    {
        revealing = false;
        fadingOut = false;
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
        if (!initialized || fadingOut)
        {
            return;
        }

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
        ClearLegacyFrame();

        // The white cut edge sits behind the plate and shows as a rim around it; the wash sits
        // over the plate. The scene's caption stays above all three.
        edge = EnsureSlab("DialogueFX_SlabEdge", PlateEdgeIdle, SlabGrow);
        face = EnsureSlab("DialogueFX_SlabFace", PlateFace, 0f);
        sheen = EnsureSlab("DialogueFX_SlabSheen", new Color(1f, 1f, 1f, 0f), 0f);

        edge.transform.SetSiblingIndex(0);
        face.transform.SetSiblingIndex(1);
        sheen.transform.SetSiblingIndex(2);
    }

    /// <summary>
    /// Adds one slab that fills the plate, optionally grown a few pixels so it can show as a cut
    /// edge around the slab in front of it.
    /// </summary>
    private SkewedPanel EnsureSlab(string objectName, Color color, float grow)
    {
        Transform existing = transform.Find(objectName);
        RectTransform rect;

        if (existing != null)
        {
            rect = existing as RectTransform;
        }
        else
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            rect = child.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
        }

        SkewedPanel slab = rect.GetComponent<SkewedPanel>();
        if (slab == null)
        {
            slab = rect.gameObject.AddComponent<SkewedPanel>();
        }

        slab.Slant = SlabSlant;
        slab.color = color;
        slab.raycastTarget = false;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(-grow, -grow);
        rect.offsetMax = new Vector2(grow, grow);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        return slab;
    }

    /// <summary>Removes the rounded frame an older build of this component left behind.</summary>
    private void ClearLegacyFrame()
    {
        string[] legacy = { "DialogueFX_Gradient", "DialogueFX_Border" };

        for (int i = 0; i < legacy.Length; i++)
        {
            Transform existing = transform.Find(legacy[i]);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }
    }

    private void ApplyStyle()
    {
        bool isNext = role == ButtonRole.Next;

        if (isNext)
        {
            faceIdle = InvertedIdle;
            faceHover = InvertedHover;
            facePress = InvertedPress;
            edgeIdle = new Color(1f, 1f, 1f, 0f);
            edgeHover = new Color(1f, 1f, 1f, 0f);
            labelBase = TagInk;
            labelHover = TagInk;
            labelPress = new Color(0.10f, 0.10f, 0.10f, 1f);
        }
        else
        {
            faceIdle = PlateFace;
            faceHover = PlateFace;
            facePress = PlateFace;
            edgeIdle = PlateEdgeIdle;
            edgeHover = PlateEdgeHover;
            labelBase = Color.white;
            labelHover = LabelHoverTint;
            labelPress = LabelPressTint;
        }

        // The caption is left exactly as the scene authored it, so only its typography is set: a
        // big tracked bold NEXT on the inverted plate, a centred choice over the dark one. Both
        // plates lean, so the text is nudged half the slant to the right to sit in the middle of
        // the slab the eye sees rather than of the upright rect.
        if (label != null)
        {
            float nudge = SlabSlant * 0.5f;

            label.fontSize = isNext ? 38f : 40f;
            label.enableAutoSizing = false;
            label.fontStyle = isNext ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = isNext ? 6f : 2f;
            label.lineSpacing = 0f;
            label.margin = new Vector4(nudge, 0f, -nudge, 0f);
            label.raycastTarget = false;
        }

        if (background != null)
        {
            background.color = new Color(1f, 1f, 1f, 0f);
            background.raycastTarget = true;
        }

        hoverAmount = 0f;
        pressAmount = 0f;
        ApplyHover();
        ApplyPress();
    }

    /// <summary>
    /// Hover lifts the plate's tone and firms its edge; a press pulls it back down and shrinks the
    /// plate, so the click reads as the button being pushed in.
    /// </summary>
    private void ApplyHover()
    {
        if (face != null)
        {
            Color lifted = Color.Lerp(faceIdle, faceHover, hoverAmount);
            face.color = Color.Lerp(lifted, facePress, pressAmount);
        }

        if (edge != null)
        {
            edge.color = Color.Lerp(edgeIdle, edgeHover, hoverAmount);
        }

        if (sheen != null)
        {
            // The inverted plate already changes tone on hover, so only the dark plates take the
            // white wash.
            float wash = role == ButtonRole.Next
                ? 0f
                : Mathf.Lerp(Mathf.Lerp(0f, HoverSheen, hoverAmount), PressSheen, pressAmount);
            sheen.color = new Color(1f, 1f, 1f, wash);
        }

        if (label != null)
        {
            Color lifted = Color.Lerp(labelBase, labelHover, hoverAmount);
            label.color = Color.Lerp(lifted, labelPress, pressAmount);
        }
    }

    /// <summary>
    /// Fades this plate out where it stands, for scenes that leave the chosen button switched on
    /// instead of hiding the choice set. The scene that hides it has its own fade drawn over the
    /// spot by DialogueBoxUI.
    /// </summary>
    public void FadeOut(float duration)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        fadingOut = true;
        revealing = false;

        if (button != null)
        {
            button.interactable = false;
        }

        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        RectTransform rect = transform as RectTransform;
        Vector3 fromScale = rect != null ? rect.localScale : Vector3.one;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress * progress);
            }

            if (rect != null)
            {
                rect.localScale = Vector3.LerpUnclamped(fromScale, fromScale * 0.98f, progress);
            }

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
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
