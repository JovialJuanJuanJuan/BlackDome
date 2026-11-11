using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] GameObject fadeOut;
    [SerializeField] AudioSource buttonClick;
    [SerializeField] int sceneToLoad;
    [SerializeField] int saveTransferValue;
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
        saveTransferValue = PlayerPrefs.GetInt("LoadState");
        if (saveTransferValue > 0)
        {
            sceneToLoad = saveTransferValue + 1;
            buttonClick.Play();
            fadeOut.SetActive(true);
            StartCoroutine(LoadScene());
        }
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

    IEnumerator LoadScene()
    {
        yield return new WaitForSeconds(3);
        yield return SceneManager.LoadSceneAsync(sceneToLoad);
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