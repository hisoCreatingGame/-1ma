using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StartSceneController : MonoBehaviour
{
    bool isStarting = false;
    [SerializeField] Animator startAnimator;
    [SerializeField] private string transitionSeKey = SeKeys.StartToLobbyTransition;

    public void OnStartButton()
    {
        if (isStarting) return;
        isStarting = true;

        StartCoroutine(StartGame());
    }

    IEnumerator StartGame()
    {
        // クリック演出の余韻を少し残しつつ、SEが終わってから遷移
        yield return StartCoroutine(SeController.PlayAndWait(transitionSeKey, 1.0f));

        SceneManager.LoadScene("LobbyScene");
    }
}
