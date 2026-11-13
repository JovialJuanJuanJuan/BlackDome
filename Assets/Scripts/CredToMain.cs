using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CredToMain : MonoBehaviour
{
    // How long the credits are shown before the screen starts fading out
    [SerializeField] float creditsDuration = 15f;

    // How long before the scene loads that the FadeOut is switched on
    [SerializeField] float fadeOutLeadTime = 2f;

    // Full-screen fade that plays when it is enabled
    [SerializeField] GameObject fadeOut;

    void Start()
    {
        StartCoroutine(CreditsBack());
    }

    IEnumerator CreditsBack()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, creditsDuration - fadeOutLeadTime));

        if (fadeOut != null)
        {
            fadeOut.SetActive(true);
        }

        yield return new WaitForSeconds(fadeOutLeadTime);
        yield return SceneManager.LoadSceneAsync(1);
    }
}
