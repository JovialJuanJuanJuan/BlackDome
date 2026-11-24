using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] GameObject fadeOut;
    [SerializeField] AudioSource buttonClick;
    [SerializeField] GameObject fadeIn;

    void Start()
    {
        StartCoroutine(StopFade());
    }

    public void StartGame()
    {
        buttonClick.Play();
        fadeOut.SetActive(true);
        StartCoroutine(TransferToIntroSeq());
    }

    public void LoadGame()
    {
        buttonClick.Play();

        // The title menu opens the same twelve save boxes the pause menu uses. The chosen slot
        // reloads its scene and fast-forwards it back to the saved line.
        SaveMenuUI.OpenLoadFromMenu();
    }

    public void GotToCredits()
    {
        buttonClick.Play();
        fadeOut.SetActive(true);
        StartCoroutine(TransferToCredits());
    }

    public void QuitGame()
    {
        // Quitting goes through the menu's confirmation card, so the game is never closed by a
        // single stray click.
        SaveMenuUI.ConfirmQuit();
    }

    IEnumerator TransferToIntroSeq()
    {
        yield return new WaitForSeconds(3);
        yield return SceneManager.LoadSceneAsync(3);
    }

    IEnumerator TransferToCredits()
    {
        yield return new WaitForSeconds(3);
        yield return SceneManager.LoadSceneAsync(2);
    }

    IEnumerator StopFade()
    {
        yield return new WaitForSeconds(2);
        fadeIn.SetActive(false);
    }

    /// <summary>
    /// Drops the title's own opening fade. Used when the game already faded to black and brings
    /// the menu in itself, so the two fades do not stack on top of each other.
    /// </summary>
    public void SkipFadeIn()
    {
        if (fadeIn != null)
            fadeIn.SetActive(false);
    }
}