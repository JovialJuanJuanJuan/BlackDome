using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Re-skins the in-scene dialogue box in the same Persona language as the pause menu: a
/// near-black slab cut with a white edge, a slate shadow above it, a white name tag carrying
/// black type and an inverted NEXT plate. The component finds the existing dialogue objects in
/// each scene and installs itself after every scene loads, so all dialogue scenes (and any new
/// scene that follows the same naming) share the same presentation.
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
    private const float PanelHeight = 280f;               // trimmed so the card sits lower on the art
    private const float BodyMarginLeft = 60f;
    private const float BodyMarginRight = 61.94104f;
    // The choice plates are centred on the frame and stacked in the open screen above the card,
    // with the whole stack centred in that space so every choice set sits in the same place.
    private const float ChoiceGap = 34f;
    private const float ChoiceBottomClearance = 18f;
    private const float ChoiceTopMargin = 24f;

    /// <summary>
    /// A choice set dims the screen behind it, so the answer is the only thing in focus while it
    /// is up. The chosen plate then fades away in place.
    /// </summary>
    private const float ChoiceDimOpacity = 0.62f;
    private const float ChoiceDimFadeIn = 0.18f;
    private const float ChoiceDimFadeOut = 0.32f;
    private const float ChoiceGhostFade = 0.26f;

    private static readonly Color ChoiceGhostFace = new Color(0.012f, 0.012f, 0.013f, 1f);
    private static readonly Color ChoiceGhostEdge = new Color(1f, 1f, 1f, 0.95f);

    /// <summary>The name kicks up a little and flashes when a new speaker takes over.</summary>
    private const float NamePulseDuration = 0.26f;
    private const float NamePulseScale = 1.06f;

    // The name sits on a white tag that rests on the card's top edge. The tag is cut to the name,
    // so a short speaker gets a short tab instead of a wide white block; these are its starting
    // size, its left edge and the limits it is fitted between.
    private const float NameLeft = 29f;
    private const float NamePadRight = 14f;
    private const float NameMinWidth = 150f;
    private const float NameMaxWidth = 620f;
    private static readonly Vector2 NameSize = new Vector2(NameMinWidth, 52f);
    private static readonly Vector2 NamePosition = new Vector2(104f, 282f);
    private static readonly Vector2 BodySize = new Vector2(1928.509f, 175.1409f);
    private static readonly Vector2 BodyPosition = new Vector2(977f, 126f);
    private static readonly Vector2 NextSize = new Vector2(263.63232f, 66f);
    private static readonly Vector2 NextPosition = new Vector2(-183f, 83f);
    private static readonly Vector2 ChoiceSize = new Vector2(820f, 124f);

    private static readonly Color DialogueText = Color.white;
    private static readonly Color DefaultAccent = new Color(0.92f, 0.92f, 0.92f, 0.85f);

    /// <summary>
    /// The card is cut from the same slabs as the pause poster: a slate shadow, a white cut edge
    /// and a near-black face. The card runs the full width of the frame, so the cut edge above it
    /// and the tag resting on it are what carry the Persona shape.
    /// </summary>
    private static readonly Color CardFaceColor = new Color(0.012f, 0.012f, 0.013f, 0.85f);
    private static readonly Color CardEdgeColor = new Color(0.93f, 0.93f, 0.94f, 0.92f);
    private static readonly Color CardShadowColor = new Color(0.13f, 0.13f, 0.14f, 0.40f);
    private static readonly Color TagFace = new Color(0.94f, 0.94f, 0.95f, 1f);
    private static readonly Color TagInk = new Color(0.05f, 0.05f, 0.05f, 1f);

    private const float CardSlant = 40f;
    private const float CardRuleThickness = 3f;
    private const float NameTagSlant = 18f;
    private const float NamePadLeft = 30f;

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

    private RectTransform nameTagRect;
    private string nameTagText;
    private Color pulseColor = Color.white;

    private Image choiceDim;
    private RectTransform choiceDimRect;
    private float choiceDimAlpha;
    private bool choiceChosen;
    private bool choicesRaised;

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
        EnsureChoiceDim();
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
    /// Centres the choice plates on the frame and centres the whole stack in the open screen above
    /// the card, so every set sits in the same place and the answer is the only thing in the middle
    /// of the view. A stack too tall for the space closes its gap instead of running off the top.
    /// </summary>
    private void ApplyChoiceLayout(float canvasHeight)
    {
        if (choiceButtons.Count == 0)
        {
            return;
        }

        int count = choiceButtons.Count;
        float ceiling = canvasHeight - ChoiceTopMargin;
        float floor = PanelHeight + ChoiceBottomClearance;
        float gap = ChoiceGap;

        if (count > 1)
        {
            float room = ceiling - floor - count * ChoiceSize.y;
            gap = Mathf.Clamp(room / (count - 1), 0f, ChoiceGap);
        }

        float stackHeight = count * ChoiceSize.y + (count - 1) * gap;
        float topCenter = (ceiling + floor) * 0.5f + stackHeight * 0.5f - ChoiceSize.y * 0.5f;
        float canvasCenter = canvasHeight * 0.5f;

        for (int i = 0; i < count; i++)
        {
            RectTransform rect = choiceButtons[i].transform as RectTransform;
            if (rect == null)
            {
                continue;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ChoiceSize;
            rect.anchoredPosition = new Vector2(
                0f,
                topCenter - i * (ChoiceSize.y + gap) - canvasCenter);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }

    private void ApplyPanelStyle()
    {
        panelGraphic.color = new Color(1f, 1f, 1f, 0f);
        panelGraphic.raycastTarget = false;

        ClearLegacyCardLayers();

        SkewedPanel shadow = EnsureCardSlab(
            "DialogueFX_CardShadow",
            CardShadowColor,
            new Vector2(24f, 24f),
            new Vector2(10f, 10f));

        SkewedPanel face = EnsureCardSlab(
            "DialogueFX_CardFace",
            CardFaceColor,
            Vector2.zero,
            Vector2.zero);

        // The card shows the art through it, so the cut edge is a thin white rule laid along the
        // top instead of a white slab behind the face, which would wash the whole card grey.
        SkewedPanel rule = EnsureCardRule("DialogueFX_CardEdge", CardEdgeColor);

        // Shadow at the back, the face over it, the cut edge over that. The scene's own name,
        // body and buttons stay above all three.
        shadow.transform.SetSiblingIndex(0);
        face.transform.SetSiblingIndex(1);
        rule.transform.SetSiblingIndex(2);

        BuildNameTag();
    }

    /// <summary>
    /// Drops the rounded gradient card the box used to be drawn from, so a scene that already
    /// carries it comes up as the slab instead of wearing two cards at once.
    /// </summary>
    private void ClearLegacyCardLayers()
    {
        string[] legacy = { "DialogueFX_Gradient", "DialogueFX_Frame", "DialogueFX_Shadow", "Splitter" };

        for (int i = 0; i < legacy.Length; i++)
        {
            Transform existing = panelRect.Find(legacy[i]);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }
    }

    /// <summary>A slab that hugs the card, grown and nudged to sit in its own layer.</summary>
    private SkewedPanel EnsureCardSlab(string objectName, Color color, Vector2 grow, Vector2 offset)
    {
        RectTransform rect = EnsureChild(panelRect, objectName);

        SkewedPanel slab = rect.GetComponent<SkewedPanel>();
        if (slab == null)
        {
            slab = rect.gameObject.AddComponent<SkewedPanel>();
        }

        slab.Slant = CardSlant;
        slab.color = color;
        slab.raycastTarget = false;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(-grow.x * 0.5f + offset.x, -grow.y * 0.5f + offset.y);
        rect.offsetMax = new Vector2(grow.x * 0.5f + offset.x, grow.y * 0.5f + offset.y);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        return slab;
    }

    /// <summary>
    /// The thin white cut edge along the card's top: it hugs the same line the face's top edge
    /// runs on, so it reads as the panel's own edge rather than a bar laid over it.
    /// </summary>
    private SkewedPanel EnsureCardRule(string objectName, Color color)
    {
        RectTransform rect = EnsureChild(panelRect, objectName);

        SkewedPanel slab = rect.GetComponent<SkewedPanel>();
        if (slab == null)
        {
            slab = rect.gameObject.AddComponent<SkewedPanel>();
        }

        slab.Slant = 0f;
        slab.color = color;
        slab.raycastTarget = false;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(CardSlant, -CardRuleThickness);
        rect.offsetMax = new Vector2(CardSlant, 0f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        return slab;
    }

    /// <summary>
    /// The white label block the speaker's name sits on: the pause menu's header tag, resting on
    /// the card's top edge. It is dropped in just behind the name so the black type stays on top.
    /// </summary>
    private void BuildNameTag()
    {
        if (nameText == null)
        {
            return;
        }

        RectTransform nameRect = nameText.rectTransform;
        RectTransform rect = EnsureChild(nameRect.parent, "DialogueFX_NameTag");
        nameTagRect = rect;

        SkewedPanel tag = rect.GetComponent<SkewedPanel>();
        if (tag == null)
        {
            tag = rect.gameObject.AddComponent<SkewedPanel>();
        }

        tag.Slant = NameTagSlant;
        tag.color = TagFace;
        tag.raycastTarget = false;

        rect.anchorMin = nameRect.anchorMin;
        rect.anchorMax = nameRect.anchorMax;
        rect.pivot = nameRect.pivot;
        rect.sizeDelta = nameRect.sizeDelta + new Vector2(14f, 10f);
        rect.anchoredPosition = nameRect.anchoredPosition;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        rect.SetSiblingIndex(Mathf.Max(0, nameRect.GetSiblingIndex()));
    }

    private static RectTransform EnsureChild(Transform parent, string objectName)
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

    private void ApplyTextStyle()
    {
        if (nameText != null)
        {
            // Black on the white tag: the name is a label now, not a coloured headline, so it
            // needs none of the drop shadow it used to carry over the art.
            // Small, bold and tracked like the menu's own tag, so the name reads as a label on
            // the tab rather than a headline squeezed into a white box.
            nameText.fontSize = 28f;
            nameText.enableAutoSizing = false;
            nameText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            nameText.alignment = TextAlignmentOptions.Left;
            nameText.characterSpacing = 6f;
            nameText.lineSpacing = 0f;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.margin = new Vector4(NamePadLeft, 0f, 14f, 0f);
            nameText.color = TagInk;
            nameText.raycastTarget = false;

            Shadow nameShadow = nameText.GetComponent<Shadow>();
            if (nameShadow != null)
            {
                nameShadow.enabled = false;
            }
        }

        if (bodyText != null)
        {
            bodyText.fontSize = 34f;
            bodyText.enableAutoSizing = false;
            bodyText.fontStyle = FontStyles.Normal;
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.lineSpacing = 6f;
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

            // The scenes hide the whole set on click, so the fade of the picked plate is drawn
            // from a snapshot taken just after that (see FadeChosenChoice).
            int chosen = i;
            choice.onClick.AddListener(() => OnChoiceSelected(chosen));
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

        // The type stays black on the white tag; the speaker's colour only shows as the flash on
        // a change, so the name never stops reading as a label.
        nameColor = TagInk;
        pulseColor = Color.Lerp(TagInk, accent, 0.55f);

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
        nameText.color = Color.Lerp(nameColor, pulseColor, kick * 0.8f);
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

        UpdateNameTag();
        UpdateChoiceDim();

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

    /// <summary>
    /// The tag follows the name: when the speaker is cleared between lines, the white block goes
    /// with it instead of floating on the empty card.
    /// </summary>
    private void UpdateNameTag()
    {
        if (nameTagRect == null || nameText == null)
        {
            return;
        }

        string speaker = nameText.text;
        bool show = nameText.gameObject.activeInHierarchy
            && !string.IsNullOrWhiteSpace(speaker);

        if (show && !string.Equals(speaker, nameTagText, System.StringComparison.Ordinal))
        {
            nameTagText = speaker;
            FitNameTag(speaker);
        }

        if (nameTagRect.gameObject.activeSelf != show)
        {
            nameTagRect.gameObject.SetActive(show);
        }
    }

    /// <summary>
    /// Cuts the tag to the name. The left edge stays put so the name lines up with the body text
    /// under it, and only the right edge moves, which keeps a two-letter speaker in a small tab.
    /// </summary>
    private void FitNameTag(string speaker)
    {
        // Measured with the margins off, so the padding below is the only padding in the tag
        // whatever the text layout counts as part of the preferred width.
        Vector4 margin = nameText.margin;
        nameText.margin = Vector4.zero;
        Vector2 preferred = nameText.GetPreferredValues(speaker);
        nameText.margin = margin;

        float width = Mathf.Clamp(
            Mathf.Ceil(preferred.x) + NamePadLeft + NamePadRight,
            NameMinWidth,
            NameMaxWidth);

        RectTransform nameRect = nameText.rectTransform;
        nameRect.sizeDelta = new Vector2(width, NameSize.y);
        nameRect.anchoredPosition = new Vector2(NameLeft + width * 0.5f, NamePosition.y);

        nameTagRect.sizeDelta = new Vector2(width + 14f, NameSize.y + 10f);
        nameTagRect.anchoredPosition = nameRect.anchoredPosition;
    }

    // ------------------------------------------------------------------ choices

    private void OnChoiceSelected(int index)
    {
        choiceChosen = true;

        if (index < 0 || index >= choiceButtons.Count)
        {
            return;
        }

        StartCoroutine(FadeChosenChoice(choiceButtons[index]));
    }

    /// <summary>
    /// The scenes switch every choice off the moment one is picked, which would cut a fade short.
    /// So the click is left to the scene, and one frame later the picked plate's own look is drawn
    /// back in its place and faded out from there: the answer visibly leaves as the dimming lifts.
    /// </summary>
    private IEnumerator FadeChosenChoice(Button choice)
    {
        yield return null;

        if (choice == null)
        {
            yield break;
        }

        // A scene that leaves the plate up is faded directly instead of being copied.
        if (choice.gameObject.activeInHierarchy)
        {
            DialogueButtonFx effect = choice.GetComponent<DialogueButtonFx>();
            if (effect != null)
            {
                effect.FadeOut(ChoiceGhostFade);
            }

            yield break;
        }

        SpawnChoiceGhost(choice);
    }

    private void SpawnChoiceGhost(Button choice)
    {
        RectTransform canvasRect = transform as RectTransform;
        RectTransform source = choice.transform as RectTransform;
        if (canvasRect == null || source == null)
        {
            return;
        }

        // The plate's place in canvas space; its own rect is still known once it is switched off.
        // The conversion lands relative to the canvas pivot, so it is shifted onto the centre
        // anchor the ghost is placed against.
        Vector2 pivot = canvasRect.pivot;
        Vector2 size = source.rect.size;
        Vector2 center = canvasRect.InverseTransformPoint(source.TransformPoint(source.rect.center));
        center += new Vector2(
            (pivot.x - 0.5f) * canvasRect.rect.width,
            (pivot.y - 0.5f) * canvasRect.rect.height);

        var ghostObject = new GameObject("DialogueFX_ChoiceGhost", typeof(RectTransform));
        RectTransform rect = ghostObject.GetComponent<RectTransform>();
        rect.SetParent(canvasRect, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = center;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        CanvasGroup group = ghostObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 1f;

        AddGhostSlab(rect, "Edge", new Vector2(3f, 3f), ChoiceGhostEdge);
        AddGhostSlab(rect, "Face", Vector2.zero, ChoiceGhostFace);

        TMP_Text sourceLabel = choice.GetComponentInChildren<TMP_Text>(true);

        var labelObject = new GameObject("Label", typeof(RectTransform));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        if (sourceLabel != null)
        {
            label.font = sourceLabel.font;
            label.text = sourceLabel.text;
            label.fontSize = sourceLabel.fontSize;
            label.fontStyle = sourceLabel.fontStyle;
        }

        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        StartCoroutine(FadeGhost(ghostObject, group, rect));
    }

    private static void AddGhostSlab(RectTransform parent, string objectName, Vector2 grow, Color color)
    {
        var slabObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = slabObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(-grow.x, -grow.y);
        rect.offsetMax = new Vector2(grow.x, grow.y);
        rect.localScale = Vector3.one;

        SkewedPanel slab = slabObject.AddComponent<SkewedPanel>();
        slab.Slant = 14f;
        slab.color = color;
        slab.raycastTarget = false;
    }

    private IEnumerator FadeGhost(GameObject ghost, CanvasGroup group, RectTransform rect)
    {
        float elapsed = 0f;
        Vector3 fromScale = rect.localScale;

        while (elapsed < ChoiceGhostFade)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / ChoiceGhostFade);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);

            group.alpha = 1f - eased;
            rect.localScale = Vector3.LerpUnclamped(fromScale, fromScale * 0.985f, eased);
            yield return null;
        }

        Destroy(ghost);
    }

    /// <summary>
    /// The dark sheet the choice plates sit on, dropped in between the scene art and the plates
    /// so the art falls back while the choices and the dialogue line stay at full brightness.
    /// </summary>
    private void EnsureChoiceDim()
    {
        if (choiceDim != null || panelRect == null)
        {
            return;
        }

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        RectTransform rect = EnsureChild(canvasRect, "DialogueFX_Dim");
        choiceDimRect = rect;

        choiceDim = rect.GetComponent<Image>();
        if (choiceDim == null)
        {
            choiceDim = rect.gameObject.AddComponent<Image>();
        }

        choiceDim.color = new Color(0f, 0f, 0f, 0f);
        choiceDim.raycastTarget = false;
        choiceDim.enabled = false;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        // Between the art and the choice plates. The scenes draw their backgrounds as full-screen
        // images inside this same canvas, so a sheet at the very back would be hidden behind them;
        // the plates and the card, which come later, stay above it and stay bright.
        int insertAt = Mathf.Max(0, rect.parent.childCount - 1);
        insertAt = Mathf.Min(insertAt, TopLevelIndex(panelRect, canvasRect));

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            RectTransform choiceRect = choiceButtons[i].transform as RectTransform;
            if (choiceRect != null)
            {
                insertAt = Mathf.Min(insertAt, TopLevelIndex(choiceRect, canvasRect));
            }
        }

        rect.SetSiblingIndex(insertAt);
    }

    /// <summary>
    /// The index of a rect's outermost ancestor under the canvas, which is the slot the canvas
    /// actually sorts by.
    /// </summary>
    private static int TopLevelIndex(RectTransform rect, RectTransform canvasRoot)
    {
        Transform top = rect;
        while (top.parent != null && top.parent != canvasRoot)
        {
            top = top.parent;
        }

        return top.parent == canvasRoot
            ? Mathf.Max(0, top.GetSiblingIndex())
            : Mathf.Max(0, canvasRoot.childCount - 1);
    }

    private void UpdateChoiceDim()
    {
        if (choiceDim == null)
        {
            return;
        }

        bool anyShowing = AnyChoiceShowing();
        if (!anyShowing)
        {
            choiceChosen = false;
            choicesRaised = false;
        }

        bool showing = anyShowing && !choiceChosen;

        if (showing && !choicesRaised)
        {
            choicesRaised = true;
            RaiseChoicesAboveScene();
        }
        float target = showing ? ChoiceDimOpacity : 0f;
        float duration = showing ? ChoiceDimFadeIn : ChoiceDimFadeOut;

        choiceDimAlpha = Mathf.MoveTowards(
            choiceDimAlpha,
            target,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));

        choiceDim.color = new Color(0f, 0f, 0f, choiceDimAlpha);
        choiceDim.enabled = choiceDimAlpha > 0.002f;
    }

    /// <summary>
    /// Builds the choice stack: the plates over everything else the canvas draws — character
    /// expressions included, since scenes stack theirs above the buttons — the dim directly under
    /// the plates so those characters are dimmed too, the dialogue card just above the dim so the
    /// line stays readable, and the scene's fade overlays above the lot so a transition still
    /// covers everything.
    /// </summary>
    private void RaiseChoicesAboveScene()
    {
        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        var fades = new List<Transform>();

        for (int i = 0; i < canvasRect.childCount; i++)
        {
            Transform child = canvasRect.GetChild(i);
            if (child.name.StartsWith("Fade", System.StringComparison.OrdinalIgnoreCase))
            {
                fades.Add(child);
            }
        }

        var tops = new List<Transform>();

        for (int i = 0; i < choiceButtons.Count; i++)
        {
            RectTransform choiceRect = choiceButtons[i].transform as RectTransform;
            if (choiceRect == null)
            {
                continue;
            }

            Transform top = TopUnderCanvas(choiceRect, canvasRect);
            if (top != null && !tops.Contains(top))
            {
                tops.Add(top);
            }
        }

        if (tops.Count == 0)
        {
            return;
        }

        Transform cardTop = TopUnderCanvas(panelRect, canvasRect);
        bool cardSharesPlate = cardTop != null && tops.Contains(cardTop);

        // Appended in this order, so this is the stack they end up in: the dim, then the card,
        // then the plates, then the fades. Characters fall behind the dim, the line and the
        // answers stay in front of it, and a transition still covers everything.
        if (choiceDimRect != null)
        {
            choiceDimRect.SetAsLastSibling();
        }

        if (cardTop != null && !cardSharesPlate)
        {
            cardTop.SetAsLastSibling();
        }

        for (int i = 0; i < tops.Count; i++)
        {
            tops[i].SetAsLastSibling();
        }

        for (int i = 0; i < fades.Count; i++)
        {
            fades[i].SetAsLastSibling();
        }
    }

    private static Transform TopUnderCanvas(Transform rect, RectTransform canvasRoot)
    {
        Transform top = rect;
        while (top.parent != null && top.parent != canvasRoot)
        {
            top = top.parent;
        }

        return top.parent == canvasRoot ? top : null;
    }

    private bool AnyChoiceShowing()
    {
        for (int i = 0; i < choiceButtons.Count; i++)
        {
            Button button = choiceButtons[i];
            if (button != null && button.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
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
