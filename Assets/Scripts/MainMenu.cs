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
        Application.Quit();
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
}