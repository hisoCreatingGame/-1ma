using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartSceneController : MonoBehaviour
{
    bool isStarting = false;
    [SerializeField] Animator startAnimator;

    public void OnStartButton()
    {
        if (isStarting) return;
        isStarting = true;

        StartCoroutine(StartGame());
    }

    IEnumerator StartGame()
    {
        yield return new WaitForSeconds(1.0f);

        SceneManager.LoadScene("LobbyScene");
    }
}
