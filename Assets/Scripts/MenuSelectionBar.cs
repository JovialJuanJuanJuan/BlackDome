using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Persona-flavoured cursor for the main menu, kept in the game's black-and-grey palette:
/// a near-black plate edged with a pale hairline slides behind the label the pointer is
/// resting on, plus a thin slanted rule that frames the top of the row. The plate borrows
/// Persona 5's dialogue-box treatment — solid dark fill, bright thin edge — so it reads as
/// a slab of glass rather than a coloured button.
///
/// The plate is hover only: with the pointer off the row the menu is bare text, and the
/// plate fades away the moment the pointer leaves a label.
///
/// Everything is built at runtime from <see cref="SkewedPanel"/>, so the scene only holds
/// this component and the four buttons: there is no sprite to import and the menu keeps
/// working if the buttons are nudged around in the editor.
///
/// Pointing at a label also selects it, so the arrow keys and Enter continue where the
/// mouse left off; the selection alone never shows the plate.
/// </summary>
[DisallowMultipleComponent]
public class MenuSelectionBar : MonoBehaviour
{
    [Header("Menu Buttons")]
    [Tooltip("Buttons the plate can follow, left to right. Any empty slot is skipped.")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Selection Plate")]
    [Tooltip("Dark fill of the plate. Alpha is the fully shown value.")]
    [SerializeField] private Color fillColor = new Color(0.055f, 0.055f, 0.065f, 0.93f);
    [Tooltip("Pale hairline around the plate. This is the edge that makes it read at a glance.")]
    [SerializeField] private Color borderColor = new Color(0.93f, 0.94f, 0.95f, 0.85f);
    [Tooltip("Thickness of the pale hairline, in UI pixels.")]
    [SerializeField, Range(0f, 8f)] private float borderWidth = 2f;
    [Tooltip("Horizontal shear of the plate. This is what gives it the slanted Persona look.")]
    [SerializeField, Range(0f, 80f)] private float plateSlant = 26f;
    [Tooltip("Extra width added on both sides of the label the plate sits behind.")]
    [SerializeField, Range(0f, 80f)] private float platePadding = 26f;
    [Tooltip("Plate height as a fraction of the button height.")]
    [SerializeField, Range(0.4f, 1.2f)] private float plateHeightScale = 0.9f;
    [Tooltip("Seconds the plate takes to settle under a new label.")]
    [SerializeField, Range(0.01f, 0.4f)] private float slideTime = 0.08f;
    [Tooltip("Seconds the plate takes to fade in and out.")]
    [SerializeField, Range(0.01f, 0.4f)] private float fadeTime = 0.14f;

    [Header("Accent Rule")]
    [Tooltip("Thin slanted rule above the button row, the line Persona menus use to lead the eye.")]
    [SerializeField] private Color ruleColor = new Color(1f, 1f, 1f, 0.22f);
    [SerializeField, Range(0f, 8f)] private float ruleHeight = 3f;
    [Tooltip("Gap between the top of the highest button and the rule.")]
    [SerializeField, Range(0f, 160f)] private float ruleGap = 56f;
    [SerializeField, Range(0f, 80f)] private float ruleSlant = 26f;

    private readonly List<Button> _buttons = new List<Button>();

    private RectTransform _layer;
    private RectTransform _plateEdgeRect;
    private RectTransform _plateFillRect;
    private RectTransform _ruleRect;
    private SkewedPanel _plateEdge;
    private SkewedPanel _plateFill;
    private SkewedPanel _rule;

    private float _plateAlpha;
    private float _squash = 1f;
    private Button _previous;
    private Button _hovered;

    /// <summary>
    /// Full-canvas layer holding the plate and the rule. It sits just behind the labels,
    /// so anything parented here draws above the background but under the menu and fades.
    /// </summary>
    public RectTransform Layer
    {
        get { return _layer; }
    }

    private void Awake()
    {
        RegisterButton(startButton);
        RegisterButton(loadButton);
        RegisterButton(creditsButton);
        RegisterButton(quitButton);

        Build();
    }

    private void OnDestroy()
    {
        if (_layer != null)
        {
            Destroy(_layer.gameObject);
        }
    }

    // ---------------------------------------------------------------- setup

    private void Build()
    {
        if (startButton == null)
        {
            return;
        }

        // One layer behind the labels: everything in it draws under all four buttons.
        var layerObject = new GameObject("MenuSelection", typeof(RectTransform));
        _layer = (RectTransform)layerObject.transform;
        _layer.SetParent(startButton.transform.parent, false);
        _layer.anchorMin = Vector2.zero;
        _layer.anchorMax = Vector2.one;
        _layer.offsetMin = Vector2.zero;
        _layer.offsetMax = Vector2.zero;
        _layer.SetSiblingIndex(startButton.transform.GetSiblingIndex());

        // Edge first, fill second, so the dark fill sits inside the pale outline.
        _plateEdge = CreatePanel("SelectionEdge", plateSlant);
        _plateEdgeRect = (RectTransform)_plateEdge.transform;
        _plateEdgeRect.sizeDelta = new Vector2(140f, 80f);
        _plateEdge.color = WithAlpha(borderColor, 0f);

        _plateFill = CreatePanel("SelectionPlate", plateSlant);
        _plateFillRect = (RectTransform)_plateFill.transform;
        _plateFillRect.sizeDelta = new Vector2(120f, 70f);
        _plateFill.color = WithAlpha(fillColor, 0f);

        _rule = CreatePanel("AccentRule", ruleSlant);
        _ruleRect = (RectTransform)_rule.transform;
        _rule.color = ruleColor;
        _rule.gameObject.SetActive(ruleHeight > 0f);

        Rect row = RowBounds();
        _ruleRect.anchoredPosition = new Vector2(row.center.x, row.yMax + ruleGap);
        _ruleRect.sizeDelta = new Vector2(row.width, ruleHeight);
    }

    private SkewedPanel CreatePanel(string panelName, float slant)
    {
        var panelObject = new GameObject(panelName, typeof(RectTransform));
        var rect = (RectTransform)panelObject.transform;
        rect.SetParent(_layer, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var panel = panelObject.AddComponent<SkewedPanel>();
        panel.Slant = slant;
        panel.raycastTarget = false;

        return panel;
    }

    /// <summary>
    /// Pointing at a label focuses it, so hover and keyboard share one source of truth
    /// and the plate never fights between the two.
    /// </summary>
    private void RegisterButton(Button button)
    {
        if (button == null || _buttons.Contains(button))
        {
            return;
        }

        _buttons.Add(button);

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        AddEntry(trigger, EventTriggerType.PointerEnter, () => HandleHover(button, true));
        AddEntry(trigger, EventTriggerType.PointerExit, () => HandleHover(button, false));
    }

    private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => callback());
        trigger.triggers.Add(entry);
    }

    private void HandleHover(Button button, bool isHovered)
    {
        if (isHovered)
        {
            _hovered = button;

            // Focus follows the pointer, so Enter and the arrow keys pick up from the mouse.
            Select(button);
            return;
        }

        // A pointer moving straight from one label to the next can raise the new enter first,
        // so only clear the plate if this is still the label under the pointer.
        if (_hovered == button)
        {
            _hovered = null;
        }
    }

    private void Select(Button button)
    {
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    /// <summary>Focus the first label. The "press any key" prompt calls this.</summary>
    public void SelectDefault()
    {
        Select(startButton);
    }

    // ---------------------------------------------------------------- update

    private void Update()
    {
        if (_plateFill == null)
        {
            return;
        }

        Button active = _hovered;

        if (active != _previous)
        {
            // A quick horizontal kick as the plate lands on a new label.
            _squash = 0.88f;
            _previous = active;
        }

        Vector2 targetPosition = _plateEdgeRect.anchoredPosition;
        Vector2 targetSize = _plateEdgeRect.sizeDelta;
        float targetAlpha = 0f;

        Vector2 center;
        Vector2 size;
        if (TryGetRect(active, out center, out size))
        {
            targetPosition = center;
            targetSize = new Vector2(size.x + platePadding * 2f, size.y * plateHeightScale);
            targetAlpha = 1f;
        }

        // Unscaled time, so the menu still animates if the game pauses with timeScale = 0.
        float deltaTime = Time.unscaledDeltaTime;
        float slide = 1f - Mathf.Exp(-deltaTime / Mathf.Max(slideTime, 0.0001f));
        float fade = 1f - Mathf.Exp(-deltaTime / Mathf.Max(fadeTime, 0.0001f));

        _plateEdgeRect.anchoredPosition = Vector2.Lerp(_plateEdgeRect.anchoredPosition, targetPosition, slide);
        _plateEdgeRect.sizeDelta = Vector2.Lerp(_plateEdgeRect.sizeDelta, targetSize, slide);

        _squash = Mathf.Lerp(_squash, 1f, slide);
        _plateEdgeRect.localScale = new Vector3(_squash, 1f, 1f);

        // The fill is the edge pulled in by the hairline, so the two stay parallel.
        Vector2 outer = _plateEdgeRect.sizeDelta;
        _plateFillRect.anchoredPosition = _plateEdgeRect.anchoredPosition;
        _plateFillRect.sizeDelta = new Vector2(
            Mathf.Max(outer.x - borderWidth * 2f, 0f),
            Mathf.Max(outer.y - borderWidth * 2f, 0f));
        _plateFillRect.localScale = _plateEdgeRect.localScale;

        _plateAlpha = Mathf.Lerp(_plateAlpha, targetAlpha, fade);

        SetPanelColor(_plateEdge, WithAlpha(borderColor, borderColor.a * _plateAlpha));
        SetPanelColor(_plateFill, WithAlpha(fillColor, fillColor.a * _plateAlpha));
    }

    // ---------------------------------------------------------------- helpers

    private static void SetPanelColor(SkewedPanel panel, Color target)
    {
        // Only touch the graphic when the value actually moves, so a settled menu is free.
        if (!NearlyEqual(panel.color, target))
        {
            panel.color = target;
        }
    }

    private bool TryGetRect(Button button, out Vector2 center, out Vector2 size)
    {
        center = Vector2.zero;
        size = Vector2.zero;

        if (button == null)
        {
            return false;
        }

        var rect = button.transform as RectTransform;
        if (rect == null)
        {
            return false;
        }

        // Both the layer and the buttons hang off the canvas, so canvas units round-trip.
        center = _layer.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
        size = rect.rect.size;
        return true;
    }

    private Rect RowBounds()
    {
        bool found = false;
        float xMin = 0f, xMax = 0f, yMin = 0f, yMax = 0f;

        for (int i = 0; i < _buttons.Count; i++)
        {
            Vector2 center;
            Vector2 size;
            if (!TryGetRect(_buttons[i], out center, out size))
            {
                continue;
            }

            float left = center.x - size.x * 0.5f;
            float right = center.x + size.x * 0.5f;
            float bottom = center.y - size.y * 0.5f;
            float top = center.y + size.y * 0.5f;

            if (!found)
            {
                xMin = left;
                xMax = right;
                yMin = bottom;
                yMax = top;
                found = true;
                continue;
            }

            xMin = Mathf.Min(xMin, left);
            xMax = Mathf.Max(xMax, right);
            yMin = Mathf.Min(yMin, bottom);
            yMax = Mathf.Max(yMax, top);
        }

        return found ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : new Rect();
    }

    private static bool NearlyEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.002f
            && Mathf.Abs(a.g - b.g) < 0.002f
            && Mathf.Abs(a.b - b.b) < 0.002f
            && Mathf.Abs(a.a - b.a) < 0.002f;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
