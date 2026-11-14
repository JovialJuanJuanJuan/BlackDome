using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Holds the main menu labels back until the "press any key" prompt is answered, then
/// snaps them in one after another, left to right, with a short overshoot — the way a
/// Persona title screen hands the screen over to its options.
///
/// The buttons are hidden in Awake so nothing flashes while the opening fade is still
/// clearing, and revealed by <see cref="Reveal"/>, which the prompt calls.
/// </summary>
[DisallowMultipleComponent]
public class MenuReveal : MonoBehaviour
{
    [Header("Menu Buttons")]
    [Tooltip("Labels revealed left to right. Any empty slot is skipped.")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button loadButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Reveal")]
    [Tooltip("Seconds between the key press and the first label appearing.")]
    [SerializeField, Range(0f, 1f)] private float delay = 0.15f;
    [Tooltip("Seconds between one label landing and the next one starting.")]
    [SerializeField, Range(0f, 0.4f)] private float stagger = 0.07f;
    [Tooltip("Seconds one label takes to snap into place.")]
    [SerializeField, Range(0.05f, 1f)] private float revealTime = 0.28f;
    [Tooltip("Scale a label starts at. Keep it close to 1 for a subtle entrance.")]
    [SerializeField, Range(0.5f, 1f)] private float startScale = 0.86f;
    [Tooltip("How far left of its resting spot a label starts, in UI pixels.")]
    [SerializeField, Range(0f, 200f)] private float slideFrom = 48f;

    private readonly List<RectTransform> _rects = new List<RectTransform>();
    private readonly List<Vector2> _restingPositions = new List<Vector2>();
    private bool _revealed;

    private void Awake()
    {
        Cache(startButton);
        Cache(loadButton);
        Cache(creditsButton);
        Cache(quitButton);

        // Hide straight away: Awake runs before the first frame, so the row never flashes
        // behind the fade-in. Scale is left untouched at 1 so MainMenuController keeps the
        // base scale it caches for its hover pop.
        for (int i = 0; i < _rects.Count; i++)
        {
            _rects[i].gameObject.SetActive(false);
        }
    }

    private void Cache(Button button)
    {
        var rect = button != null ? button.transform as RectTransform : null;

        if (rect == null || _rects.Contains(rect))
        {
            return;
        }

        _rects.Add(rect);
        _restingPositions.Add(rect.anchoredPosition);
    }

    /// <summary>Brings the menu in. Safe to call more than once; only the first call runs.</summary>
    public void Reveal()
    {
        if (_revealed || _rects.Count == 0)
        {
            return;
        }

        _revealed = true;
        StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        for (int i = 0; i < _rects.Count; i++)
        {
            RectTransform rect = _rects[i];

            if (rect == null)
            {
                continue;
            }

            rect.gameObject.SetActive(true);
            StartCoroutine(PopIn(rect, _restingPositions[i]));

            if (stagger > 0f)
            {
                yield return new WaitForSecondsRealtime(stagger);
            }
        }
    }

    private IEnumerator PopIn(RectTransform rect, Vector2 restingPosition)
    {
        Vector2 startPosition = restingPosition + Vector2.left * slideFrom;
        Vector3 startSize = Vector3.one * startScale;
        float elapsed = 0f;

        // Unscaled time, so the menu still animates if the game pauses with timeScale = 0.
        while (elapsed < revealTime)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / Mathf.Max(revealTime, 0.0001f));
            float eased = EaseOutBack(t);

            rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, restingPosition, eased);
            rect.localScale = Vector3.LerpUnclamped(startSize, Vector3.one, eased);
            yield return null;
        }

        rect.anchoredPosition = restingPosition;
        rect.localScale = Vector3.one;
    }

    /// <summary>Ease-out with a small overshoot, so each label lands with a snap.</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
