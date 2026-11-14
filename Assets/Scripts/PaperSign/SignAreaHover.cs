using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Forwards pointer hover from the SignArea button to the scene's PaperSign controller, which
/// holds the area fully solid while it is hovered, plays the hover blip and lets the pulse carry
/// on from there when the pointer leaves.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SignAreaHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("The scene controller that owns the sign area pulse. Drag the component on SceneControl here.")]
    [SerializeField] private PaperSign paperSign;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (paperSign != null)
        {
            paperSign.SignAreaHoverEnter();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (paperSign != null)
        {
            paperSign.SignAreaHoverExit();
        }
    }
}
