using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Re-skins the in-scene dialogue box after the card RidingBlue uses, in Black Dome's black
/// palette: a bottom card whose top corners are rounded, a vertical gradient that keeps the
/// text readable, a cold hairline rim that dissolves down the sides and a soft shadow floating
/// above the card. The component finds the existing dialogue objects in each scene and installs
/// itself after every scene loads, so all dialogue scenes (and any new scene that follows the
/// same naming) share the same presentation.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueBoxUI : MonoBehaviour
{
    private const string PanelObjectName = "TextBox";
    private const string BodyTextObjectName = "SpeakText";
    private const string NameTextObjectName = "CharName";
    private const string NextButtonObjectName = "NextButton";
    private const string FadeOutObjectName = "FadeOut";

    private const float ShowDuration = 0.36f;
    private const float HideDuration = 0.26f;

    // The box repeats RidingBlue's authored layout value for value, so both games share one
    // arrangement: the same card height, the same text blocks and the same button placement.
    // Numbers are in canvas units for the 1920x1080 reference.
    private const float PanelHeight = 349.7563f;          // 1080 - 730.2437
    private const float NameMarginRight = -1655.8954f;    // keeps the name's text area 2075.9 wide
    private const float BodyMarginLeft = 20.646973f;
    private const float BodyMarginRight = 61.94104f;
    // The choice plates stack up the right edge of the frame, clear of the card and of the
    // middle of the screen where the art and the dialogue text are.
    private const float ChoiceGap = 34f;
    private const float ChoiceRightMargin = 60f;
    private const float ChoiceBottomClearance = 10f;
    private const float ChoiceTopMargin = 24f;

    /// <summary>The name kicks up a little and flashes when a new speaker takes over.</summary>
    private const float NamePulseDuration = 0.26f;
    private const float NamePulseScale = 1.06f;

    // RidingBlue ships a 200x50 name box; it is grown upwards here so the larger type keeps its
    // left edge and lifts clear of the divider.
    private static readonly Vector2 NameSize = new Vector2(420f, 64f);
    private static readonly Vector2 NamePosition = new Vector2(227.8f, 304f);
    private static readonly Vector2 SplitterSize = new Vector2(514.529f, 2f);
    private static readonly Vector2 SplitterPosition = new Vector2(248.06453f, 249.538f);
    private static readonly Vector2 BodySize = new Vector2(1928.509f, 175.1409f);
    private static readonly Vector2 BodyPosition = new Vector2(977f, 126f);
    private static readonly Vector2 NextSize = new Vector2(263.63232f, 59.106506f);
    private static readonly Vector2 NextPosition = new Vector2(-183f, 83f);
    private static readonly Vector2 ChoiceSize = new Vector2(650.01f, 134.3603f);

    private static readonly Color DialogueText = Color.white;
    private static readonly Color DefaultAccent = new Color(0.92f, 0.92f, 0.92f, 0.85f);
    private static readonly Color SplitterColor = new Color(1f, 1f, 1f, 0.6f);

    /// <summary>
    /// The card lifts off the scene along its top edge and sinks into near-black at the bottom.
    /// The ramp is strictly neutral — equal red, green and blue — so it reads as black rather
    /// than as a tinted panel. Both ends stay close to zero so the card never turns grey.
    /// </summary>
    private static readonly Color GradientTop = new Color(0.055f, 0.055f, 0.055f, 0.945f);
    private static readonly Color GradientBottom = new Color(0.004f, 0.004f, 0.004f, 0.995f);
    private static readonly Color FrameColor = new Color(1f, 1f, 1f, 0.6f);
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.4f);

    /// <summary>Only the top corners are rounded, because the card is flush with the screen edge.</summary>
    private const float CardCornerRadius = 26f;
    private const float CardFrameWidth = 2f;
    private const float CardFrameBand = 40f;
    private const float CardShadowHeight = 72f;
    private const float CardShadowOverhang = 60f;

    private static readonly Dictionary<string, Color> SpeakerAccents =
        new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "You", new Color(0.82f, 0.80f, 0.75f, 1f) },
            { "Midnight", new Color(0.55f, 0.72f, 0.84f, 1f) },
            { "Koda", new Color(0.78f, 0.68f, 0.52f, 1f) },
            { "Hiromi", new Color(0.78f, 0.64f, 0.64f, 1f) },
            { "Everyone", new Color(0.66f, 0.67f, 0.66f, 1f) }
        };

    private RectTransform panelRect;
    private CanvasGroup panelGroup;
    private Graphic panelGraphic;
    private TMP_Text nameText;
    private TMP_Text bodyText;
    private Button nextButton;
    private readonly List<Button> choiceButtons = new List<Button>();
    private GameObject fadeOutObject;

    private RawImage panelFillGraphic;
    private RawImage panelFrameGraphic;
    private Texture2D panelFillTexture;
    private Texture2D panelFrameTexture;
    private Vector2 panelCardSize;

    private Vector2 panelRestPosition;
    private Coroutine panelTransition;
    private bool isVisible;
    private bool isHiding;
    private string lastSpeaker;
    private Color nameColor = Color.white;
    private float namePulse;

    /// <summary>
    /// The dialogue manager lives in the scene, so a fresh one is installed whenever a
    /// scene containing a dialogue panel finishes loading. This keeps the presentation
    /// in one place instead of repeating it across every scene's UI hierarchy.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas.GetComponent<DialogueBoxUI>() != null)
            {
                continue;
            }

            Transform panel = FindDescendant(canvas.transform, PanelObjectName);
            if (panel == null || panel.GetComponent<Graphic>() == null)
            {
                continue;
            }

            canvas.gameObject.AddComponent<DialogueBoxUI>();
            break;
        }
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == objectName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private void Awake()
    {
        if (!CacheReferences())
        {
            enabled = false;
            return;
        }

        ApplyPanelLayout();
        ApplyPanelStyle();
        ApplyTextStyle();
        ApplyButtonStyle();
        SilenceChoiceClickSound();
        ApplySpeakerStyle(string.Empty);

        panelRestPosition = panelRect.anchoredPosition;
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;
        isVisible = false;
    }

    private bool CacheReferences()
    {
        Transform panel = null;
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];

            if (panel == null && current.name == PanelObjectName)
            {
                panel = current;
            }

            if (nameText == null && current.name == NameTextObjectName)
            {
                nameText = current.GetComponent<TMP_Text>();
            }

            if (bodyText == null && current.name == BodyTextObjectName)
            {
                bodyText = current.GetComponent<TMP_Text>();
            }

            if (fadeOutObject == null && current.name == FadeOutObjectName)
            {
                fadeOutObject = current.gameObject;
            }
        }

        if (panel == null)
        {
            return false;
        }

        panelRect = panel as RectTransform;
        panelGraphic = panel.GetComponent<Graphic>();
        if (panelRect == null || panelGraphic == null)
        {
            return false;
        }

        panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup == null)
        {
            panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        if (bodyText == null)
        {
            TMP_Text[] panelTexts = panel.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < panelTexts.Length; i++)
            {
                if (panelTexts[i] != nameText)
                {
                    bodyText = panelTexts[i];
                    break;
                }
            }
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            string buttonName = buttons[i].name.ToLowerInvariant();

            if (nextButton == null && (buttonName == NextButtonObjectName.ToLowerInvariant() || buttonName.Contains("next")))
            {
                nextButton = buttons[i];
            }
            else if (buttonName.Contains("choice"))
            {
                choiceButtons.Add(buttons[i]);
            }
        }

        choiceButtons.Sort(CompareChoicesByHeight);

        return bodyText != null;
    }

    private static int CompareChoicesByHeight(Button left, Button right)
    {
        RectTransform leftRect = left.transform as RectTransform;
        RectTransform rightRect = right.transform as RectTransform;
        float leftY = leftRect != null ? leftRect.anchoredPosition.y : 0f;
        float rightY = rightRect != null ? rightRect.anchoredPosition.y : 0f;
        return rightY.CompareTo(leftY);
    }

    private void ApplyPanelLayout()
    {
        RectTransform parentRect = panelRect.parent as RectTransform;
        float parentHeight = parentRect != null && parentRect.rect.height > 1f
            ? parentRect.rect.height
            : 1080f;

        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(0f, PanelHeight);
        panelRect.anchoredPosition = Vector2.zero;

        if (nameText != null)
        {
            RectTransform nameRect = nameText.rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.zero;
            nameRect.pivot = new Vector2(0.5f, 0.5f);
            nameRect.sizeDelta = NameSize;
            nameRect.anchoredPosition = NamePosition;
            nameRect.localScale = Vector3.one;
        }

        if (bodyText != null)
        {
            RectTransform bodyRect = bodyText.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.zero;
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.sizeDelta = BodySize;
            bodyRect.anchoredPosition = BodyPosition;
            bodyRect.localScale = Vector3.one;
        }

        if (nextButton != null)
        {
            RectTransform nextRect = nextButton.transform as RectTransform;
            if (nextRect != null)
            {
                // Anchored to the card's corner, which is the screen's corner: the same spot the
                // button holds in RidingBlue while still fading with the box.
                if (nextRect.parent != panelRect)
                {
                    nextRect.SetParent(panelRect, false);
                }

                nextRect.anchorMin = new Vector2(1f, 0f);
                nextRect.anchorMax = new Vector2(1f, 0f);
                nextRect.pivot = new Vector2(0.5f, 0.5f);
                nextRect.sizeDelta = NextSize;
                nextRect.anchoredPosition = NextPosition;
                nextRect.localScale = Vector3.one;
                nextRect.localRotation = Quaternion.identity;
            }
        }

        ApplyChoiceLayout(parentHeight);
    }

    /// <summary>
    /// Stacks the choice plates up the right edge of the frame, the last plate just clear of the
    /// card and the rest rising above it in reading order. The middle of the screen is left open,
    /// so the choices no longer sit on top of the art or the dialogue text, and a stack too tall
    /// for the frame tightens its gap instead of running off the top.
    /// </summary>
    private void ApplyChoiceLayout(float canvasHeight)
    {
        if (choiceButtons.Count == 0)
        {
            return;
        }

        int count = choiceButtons.Count;
        float lowestCenter = PanelHeight + ChoiceBottomClearance + ChoiceSize.y * 0.5f;
        float spacing = ChoiceSize.y + ChoiceGap;
        float ceiling = canvasHeight - ChoiceTopMargin - ChoiceSize.y * 0.5f;

        if (count > 1 && lowestCenter + spacing * (count - 1) > ceiling)
        {
            spacing = Mathf.Max(ChoiceSize.y, (ceiling - lowestCenter) / (count - 1));
        }

        // Anchored to the right edge, so the margin holds at any aspect ratio.
        float rightOffset = -(ChoiceSize.x * 0.5f + ChoiceRightMargin);
        float canvasCenter = canvasHeight * 0.5f;

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = choiceButtons[i].transform as RectTransform;
            if (rect == null)
            {
                continue;
            }

            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ChoiceSize;
            rect.anchoredPosition = new Vector2(
                rightOffset,
                lowestCenter + spacing * (count - 1 - i) - canvasCenter);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }

    private void ApplyPanelStyle()
    {
        panelGraphic.color = new Color(1f, 1f, 1f, 0f);
        panelGraphic.raycastTarget = false;

        // The card is drawn from three layers: the shadow spills up out of its top edge, the fill
        // carries the gradient and the rounded top corners, and the rim traces the upper contour.
        DialogueGradient.EnsureShadow(
            panelRect,
            "DialogueFX_Shadow",
            ShadowColor,
            CardShadowHeight,
            CardShadowOverhang).transform.SetSiblingIndex(0);

        panelFillGraphic = DialogueGradient.EnsureFill(panelRect, "DialogueFX_Gradient");
        panelFrameGraphic = DialogueGradient.EnsureBand(
            panelRect,
            "DialogueFX_Frame",
            CardFrameBand + CardFrameWidth * 2f);

        // Shadow behind the card, rim over it, and the divider, text and buttons above both.
        panelFillGraphic.transform.SetSiblingIndex(1);
        panelFrameGraphic.transform.SetSiblingIndex(2);
        EnsureSplitter().transform.SetSiblingIndex(3);
    }

    /// <summary>
    /// The hairline divider that sits under the speaker name, drawn where RidingBlue draws it.
    /// </summary>
    private RawImage EnsureSplitter()
    {
        RawImage splitter = DialogueGradient.EnsureSplitter(
            panelRect,
            "Splitter",
            SplitterSize,
            SplitterPosition,
            SplitterColor);

        return splitter;
    }

    /// <summary>
    /// The card's fill and rim are baked at the panel's own size, so they are rebuilt only when
    /// layout changes the rect (a resolution switch, say). Everything else is free to stretch.
    /// </summary>
    private void RefreshPanelCard()
    {
        if (panelRect == null || panelFillGraphic == null || panelFrameGraphic == null)
        {
            return;
        }

        Vector2 size = panelRect.rect.size;
        if (size.x < 2f || size.y < 2f)
        {
            return;
        }

        if (panelFillTexture != null
            && Mathf.Abs(size.x - panelCardSize.x) < 1f
            && Mathf.Abs(size.y - panelCardSize.y) < 1f)
        {
            return;
        }

        panelCardSize = size;

        DialogueGradient.DestroyTexture(panelFillTexture);
        DialogueGradient.DestroyTexture(panelFrameTexture);

        panelFillTexture = DialogueGradient.CreatePanelFill(
            size,
            CardCornerRadius,
            GradientTop,
            GradientBottom);

        panelFrameTexture = DialogueGradient.CreatePanelFrame(
            size,
            CardCornerRadius,
            CardFrameWidth,
            CardFrameBand,
            FrameColor);

        panelFillGraphic.texture = panelFillTexture;
        panelFrameGraphic.texture = panelFrameTexture;
    }

    private void ApplyTextStyle()
    {
        if (nameText != null)
        {
            nameText.fontSize = 58f;
            nameText.enableAutoSizing = false;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.TopLeft;
            nameText.characterSpacing = 0f;
            nameText.lineSpacing = 0f;
            nameText.margin = new Vector4(0f, 0f, NameMarginRight, 0f);
            nameText.color = DialogueText;
            nameText.raycastTarget = false;

            Shadow nameShadow = nameText.GetComponent<Shadow>();
            if (nameShadow == null)
            {
                nameShadow = nameText.gameObject.AddComponent<Shadow>();
            }

            // A firmer drop shadow than the body carries, so the name stays legible and reads as
            // the loudest label on the card.
            nameShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            nameShadow.effectDistance = new Vector2(2f, -2f);
            nameShadow.useGraphicAlpha = true;
        }

        if (bodyText != null)
        {
            bodyText.fontSize = 38f;
            bodyText.enableAutoSizing = false;
            bodyText.fontStyle = FontStyles.Normal;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.lineSpacing = 0f;
            bodyText.characterSpacing = 0f;
            bodyText.color = DialogueText;
            bodyText.margin = new Vector4(BodyMarginLeft, 0f, BodyMarginRight, 0f);
            bodyText.raycastTarget = false;

            Shadow shadow = bodyText.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = bodyText.gameObject.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }
    }

    private void ApplyButtonStyle()
    {
        if (nextButton != null)
        {
            DialogueButtonFx effect = nextButton.GetComponent<DialogueButtonFx>();
            if (effect == null)
            {
                effect = nextButton.gameObject.AddComponent<DialogueButtonFx>();
            }

            effect.Initialize(DialogueButtonFx.ButtonRole.Next, 0);
        }

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            Button choice = choiceButtons[i];
            DialogueButtonFx effect = choice.GetComponent<DialogueButtonFx>();
            if (effect == null)
            {
                effect = choice.gameObject.AddComponent<DialogueButtonFx>();
            }

            effect.Initialize(DialogueButtonFx.ButtonRole.Choice, i);
        }
    }

    /// <summary>
    /// The choice plates now play the Next button's click (see DialogueButtonFx), so the scene's
    /// separate choice click component is switched off. Left enabled it would fire its own clip on
    /// the same click and the two would be heard together.
    /// </summary>
    private void SilenceChoiceClickSound()
    {
        ChoiceButtonClickSound[] legacy = FindObjectsByType<ChoiceButtonClickSound>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < legacy.Length; i++)
        {
            legacy[i].enabled = false;
        }
    }

    private void ApplySpeakerStyle(string speaker)
    {
        Color accent = DefaultAccent;
        string key = speaker != null ? speaker.Trim() : string.Empty;

        Color paletteAccent;
        if (SpeakerAccents.TryGetValue(key, out paletteAccent))
        {
            accent = paletteAccent;
        }

        // Mostly white with only a hint of the speaker's colour, so the name stays the brightest
        // thing on the card instead of sinking into a tint.
        nameColor = Color.Lerp(DialogueText, accent, 0.35f);

        if (nameText != null)
        {
            nameText.color = nameColor;
        }

        namePulse = 1f;
    }

    /// <summary>
    /// Runs the name's entrance: it flashes white-hot and settles back down with a small easing
    /// kick, which is what makes a speaker change feel like something happened.
    /// </summary>
    private void UpdateNamePulse()
    {
        if (nameText == null)
        {
            return;
        }

        RectTransform rect = nameText.rectTransform;

        if (namePulse <= 0f)
        {
            rect.localScale = Vector3.one;
            nameText.color = nameColor;
            return;
        }

        namePulse = Mathf.Max(0f, namePulse - Time.unscaledDeltaTime / NamePulseDuration);

        // Full kick on the first frame, gone by the end of the pulse.
        float kick = namePulse * namePulse;

        rect.localScale = Vector3.one * (1f + (NamePulseScale - 1f) * kick);
        nameText.color = Color.Lerp(nameColor, DialogueText, kick * 0.8f);
    }

    private void Update()
    {
        if (nameText != null)
        {
            string speaker = nameText.text;
            if (!string.Equals(speaker, lastSpeaker, System.StringComparison.Ordinal))
            {
                lastSpeaker = speaker;
                ApplySpeakerStyle(speaker);
            }
        }

        UpdateNamePulse();

        if (panelRect == null)
        {
            return;
        }

        RefreshPanelCard();

        if (!panelRect.gameObject.activeInHierarchy)
        {
            if (panelTransition != null)
            {
                StopCoroutine(panelTransition);
                panelTransition = null;
            }

            isVisible = false;
            isHiding = false;
            panelGroup.alpha = 0f;
            panelRect.anchoredPosition = panelRestPosition;
            panelRect.localScale = Vector3.one;
            return;
        }

        if (fadeOutObject != null && fadeOutObject.activeInHierarchy)
        {
            if (isVisible && !isHiding)
            {
                StartHide();
            }

            return;
        }

        if (!isVisible && !isHiding && HasDialogueContent())
        {
            StartShow();
        }
    }

    private bool HasDialogueContent()
    {
        if (bodyText != null && !string.IsNullOrWhiteSpace(bodyText.text))
        {
            return true;
        }

        return nameText != null && !string.IsNullOrWhiteSpace(nameText.text);
    }

    private void StartShow()
    {
        if (panelTransition != null)
        {
            StopCoroutine(panelTransition);
        }

        panelTransition = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        isVisible = true;
        isHiding = false;
        namePulse = 1f;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        Vector2 fromPosition = panelRestPosition + new Vector2(0f, -14f);
        Vector3 fromScale = new Vector3(0.985f, 0.985f, 1f);
        float elapsed = 0f;

        panelGroup.alpha = 0f;
        panelRect.anchoredPosition = fromPosition;
        panelRect.localScale = fromScale;

        while (elapsed < ShowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / ShowDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            panelGroup.alpha = eased;
            panelRect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, panelRestPosition, eased);
            panelRect.localScale = Vector3.LerpUnclamped(fromScale, Vector3.one, eased);
            yield return null;
        }

        panelGroup.alpha = 1f;
        panelGroup.blocksRaycasts = true;
        panelGroup.interactable = true;
        panelRect.anchoredPosition = panelRestPosition;
        panelRect.localScale = Vector3.one;
        panelTransition = null;
    }

    private void StartHide()
    {
        if (isHiding)
        {
            return;
        }

        if (panelTransition != null)
        {
            StopCoroutine(panelTransition);
        }

        panelTransition = StartCoroutine(HideRoutine());
    }

    private IEnumerator HideRoutine()
    {
        isHiding = true;
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        Vector2 fromPosition = panelRect.anchoredPosition;
        Vector2 toPosition = panelRestPosition + new Vector2(0f, -10f);
        Vector3 fromScale = panelRect.localScale;
        Vector3 toScale = new Vector3(0.99f, 0.99f, 1f);
        float startAlpha = panelGroup.alpha;
        float elapsed = 0f;

        while (elapsed < HideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / HideDuration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            panelGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
            panelRect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, eased);
            panelRect.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            yield return null;
        }

        panelGroup.alpha = 0f;
        panelRect.anchoredPosition = panelRestPosition;
        panelRect.localScale = Vector3.one;
        isVisible = false;
        isHiding = false;
        panelTransition = null;

        panelRect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (panelTransition != null)
        {
            StopCoroutine(panelTransition);
        }

        DialogueGradient.DestroyTexture(panelFillTexture);
        DialogueGradient.DestroyTexture(panelFrameTexture);
    }
}

/// <summary>
/// Builds the dialogue card and the button plates the interface is drawn from. Every texture is
/// generated at runtime: the shapes come from a distance field, so the corners stay smooth and a
/// hairline rim can trace them without any sprite, mask or shader.
/// </summary>
internal static class DialogueGradient
{
    private const int MaxTexturePixels = 1200000;
    private const int ShadowTextureHeight = 64;

    private static Texture2D shadowTexture;

    /// <summary>
    /// Builds a vertical gradient whose corners follow a rounded rectangle, so a button can be
    /// curved without a sprite or a mask. The rounded alpha is anti-aliased here, which keeps the
    /// edge clean even when the texture is stretched to the element's size.
    /// </summary>
    internal static Texture2D CreateRoundedFill(Vector2 size, float radius, Color top, Color bottom)
    {
        return BuildRoundedTexture(size, radius, 0f, (distance, thickness, t) =>
        {
            Color color = Color.Lerp(bottom, top, t);
            color.a *= Coverage(distance);
            return color;
        });
    }

    /// <summary>
    /// Builds the thin rim that traces the same rounded rectangle as the fill. It is a separate
    /// texture so hover can brighten the border without touching the gradient.
    /// </summary>
    internal static Texture2D CreateRoundedBorder(Vector2 size, float radius, float thickness, Color color)
    {
        return BuildRoundedTexture(size, radius, thickness, (distance, strokeWidth, t) =>
        {
            Color stroke = color;
            stroke.a *= Coverage(distance) * Mathf.Clamp01(0.5f + distance + strokeWidth);
            return stroke;
        });
    }

    /// <summary>
    /// Builds the card fill: a vertical gradient whose top corners are rounded and whose bottom
    /// corners stay square, because the card is flush with the bottom of the screen.
    /// </summary>
    internal static Texture2D CreatePanelFill(Vector2 size, float radius, Color top, Color bottom)
    {
        int width = Mathf.Max(2, Mathf.CeilToInt(size.x));
        int height = Mathf.Max(2, Mathf.CeilToInt(size.y));

        float scale = Mathf.Min(1f, Mathf.Sqrt(MaxTexturePixels / ((float)width * height)));
        if (scale < 1f)
        {
            width = Mathf.Max(2, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(2, Mathf.RoundToInt(height * scale));
        }

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float radiusPixels = RadiusInPixels(radius, width, size.x, halfWidth, halfHeight);

        Color[] pixels = new Color[width * height];

        // Below the corner arcs the card is a plain rectangle, so only the top band needs the
        // distance field. That keeps building the card off the first dialogue frame cheap.
        float cornerBand = halfHeight - radiusPixels - 1f;

        for (int y = 0; y < height; y++)
        {
            float offsetY = y + 0.5f - halfHeight;
            bool rounded = offsetY > cornerBand;
            Color color = Color.Lerp(bottom, top, y / (height - 1f));

            for (int x = 0; x < width; x++)
            {
                Color pixel = color;

                if (rounded)
                {
                    float offsetX = x + 0.5f - halfWidth;
                    pixel.a *= Coverage(TopRoundedDistance(offsetX, offsetY, halfWidth, halfHeight, radiusPixels));
                }

                pixels[y * width + x] = pixel;
            }
        }

        return Upload("DialogueCardFill", width, height, pixels);
    }

    /// <summary>
    /// Builds the hairline rim that runs along the card's top edge and around its top corners, then
    /// tapers away as it comes down the sides. Only the band the rim can appear in is baked, so the
    /// stroke keeps its true width even when the card is very wide.
    /// </summary>
    internal static Texture2D CreatePanelFrame(
        Vector2 size,
        float radius,
        float thickness,
        float fadeBand,
        Color color)
    {
        int width = Mathf.Max(2, Mathf.CeilToInt(size.x));
        int height = Mathf.Max(2, Mathf.CeilToInt(fadeBand + thickness * 2f));

        // The band is baked at its own scale, but the shape is measured against the whole card, so
        // the corners match the fill exactly.
        float halfWidth = width * 0.5f;
        float halfHeight = size.y * 0.5f;
        float radiusPixels = Mathf.Clamp(radius, 0f, Mathf.Min(halfWidth, halfHeight));

        float innerHalfWidth = halfWidth - thickness;
        float innerHalfHeight = halfHeight - thickness;
        float innerRadius = Mathf.Max(0f, radiusPixels - thickness);

        float fadeTop = halfHeight - thickness;
        float fadeBottom = halfHeight - fadeBand;

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float offsetY = halfHeight - height + y + 0.5f;
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeBottom, fadeTop, offsetY));

            if (fade <= 0f)
            {
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                float offsetX = x + 0.5f - halfWidth;

                float coverage = Coverage(TopRoundedDistance(offsetX, offsetY, halfWidth, halfHeight, radiusPixels))
                    - Coverage(TopRoundedDistance(offsetX, offsetY, innerHalfWidth, innerHalfHeight, innerRadius));

                if (coverage <= 0f)
                {
                    continue;
                }

                Color pixel = color;
                pixel.a *= coverage * fade;
                pixels[y * width + x] = pixel;
            }
        }

        return Upload("DialogueCardFrame", width, height, pixels);
    }

    private static Texture2D BuildRoundedTexture(
        Vector2 size,
        float radius,
        float thickness,
        System.Func<float, float, float, Color> sample)
    {
        int width = Mathf.Max(2, Mathf.CeilToInt(size.x));
        int height = Mathf.Max(2, Mathf.CeilToInt(size.y));

        float scale = Mathf.Min(1f, Mathf.Sqrt(MaxTexturePixels / ((float)width * height)));
        if (scale < 1f)
        {
            width = Mathf.Max(2, Mathf.RoundToInt(width * scale));
            height = Mathf.Max(2, Mathf.RoundToInt(height * scale));
        }

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float radiusPixels = RadiusInPixels(radius, width, size.x, halfWidth, halfHeight);
        float strokePixels = Mathf.Max(1f, thickness * (width / Mathf.Max(1f, size.x)));

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float offsetY = Mathf.Abs(y + 0.5f - halfHeight) - halfHeight + radiusPixels;
            float nextY = offsetY > 0f ? offsetY * offsetY : 0f;

            for (int x = 0; x < width; x++)
            {
                float offsetX = Mathf.Abs(x + 0.5f - halfWidth) - halfWidth + radiusPixels;
                float nextX = offsetX > 0f ? offsetX * offsetX : 0f;

                float distance = Mathf.Sqrt(nextX + nextY)
                    + Mathf.Min(Mathf.Max(offsetX, offsetY), 0f)
                    - radiusPixels;

                pixels[y * width + x] = sample(distance, strokePixels, y / (height - 1f));
            }
        }

        return Upload("DialogueRoundedPanel", width, height, pixels);
    }

    private static float RadiusInPixels(float radius, int width, float sourceWidth, float halfWidth, float halfHeight)
    {
        float pixelScale = width / Mathf.Max(1f, sourceWidth);
        return Mathf.Clamp(radius * pixelScale, 0f, Mathf.Min(halfWidth, halfHeight));
    }

    /// <summary>
    /// Signed distance to a rectangle whose top corners are rounded and whose bottom corners are
    /// square; negative inside. Only the top edges curve because the card sits on the screen edge.
    /// </summary>
    private static float TopRoundedDistance(
        float offsetX,
        float offsetY,
        float halfWidth,
        float halfHeight,
        float radius)
    {
        float box = Mathf.Max(
            Mathf.Abs(offsetX) - halfWidth,
            Mathf.Max(offsetY - halfHeight, -halfHeight - offsetY));

        float cornerX = Mathf.Abs(offsetX) - (halfWidth - radius);
        float cornerY = offsetY - (halfHeight - radius);
        float outside = new Vector2(Mathf.Max(cornerX, 0f), Mathf.Max(cornerY, 0f)).magnitude;

        return Mathf.Max(box, outside - radius);
    }

    /// <summary>Turns the distance into antialiased coverage one texel wide.</summary>
    private static float Coverage(float distance)
    {
        return Mathf.Clamp01(0.5f - distance);
    }

    private static Texture2D Upload(string name, int width, int height, Color[] pixels)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.hideFlags = HideFlags.DontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>A white ramp that is dense where the shadow meets the card and clear further out.</summary>
    private static Texture2D ShadowTexture
    {
        get
        {
            if (shadowTexture == null)
            {
                Color[] pixels = new Color[ShadowTextureHeight];

                for (int y = 0; y < ShadowTextureHeight; y++)
                {
                    float t = y / (ShadowTextureHeight - 1f);
                    pixels[y] = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t)));
                }

                shadowTexture = Upload("DialogueCardShadow", 1, ShadowTextureHeight, pixels);
            }

            return shadowTexture;
        }
    }

    /// <summary>
    /// Adds the shadow that spills out of the card's top edge. It hangs from the panel so it moves
    /// with the card, and reaches past it on both sides so the corners stay covered.
    /// </summary>
    internal static RawImage EnsureShadow(Transform parent, string objectName, Color color, float height, float overhang)
    {
        RawImage image = EnsureRawImage(parent, objectName);
        image.texture = ShadowTexture;
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(overhang * 2f, height);
        rect.anchoredPosition = Vector2.zero;
        return image;
    }

    /// <summary>
    /// Adds the strip the rim is drawn into, pinned to the card's top edge and stretched across it.
    /// </summary>
    internal static RawImage EnsureBand(Transform parent, string objectName, float height)
    {
        RawImage image = EnsureRawImage(parent, objectName);
        image.color = Color.white;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, height);
        rect.anchoredPosition = Vector2.zero;
        return image;
    }

    /// <summary>Adds the divider that sits under the speaker name, sized and placed as authored.</summary>
    internal static RawImage EnsureSplitter(
        Transform parent,
        string objectName,
        Vector2 size,
        Vector2 position,
        Color color)
    {
        RawImage image = EnsureRawImage(parent, objectName);
        image.color = color;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return image;
    }

    /// <summary>Adds a graphic that fills its parent and sits behind its siblings.</summary>
    internal static RawImage EnsureFill(Transform parent, string objectName, Texture2D texture = null)
    {
        RawImage image = EnsureRawImage(parent, objectName);
        image.texture = texture;
        image.color = Color.white;

        RectTransform rect = image.rectTransform;
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    internal static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(texture);
        }
        else
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static RawImage EnsureRawImage(Transform parent, string objectName)
    {
        RectTransform rect = EnsureRect(parent, objectName);
        RawImage image = rect.GetComponent<RawImage>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<RawImage>();
        }

        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return image;
    }

    private static RectTransform EnsureRect(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            RectTransform existingRect = existing as RectTransform;
            if (existingRect != null)
            {
                return existingRect;
            }
        }

        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
}
