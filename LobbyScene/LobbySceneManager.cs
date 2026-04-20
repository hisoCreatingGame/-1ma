using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbySceneManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Waiting panel shown during scene transition")]
    public GameObject waitingPanel;

    [Tooltip("Destination game scene name")]
    public string gameSceneName = "GameScene";

    [Header("Settings")]
    [Tooltip("Minimum wait time while loading scene (seconds)")]
    public float minWaitTime = 6.0f;

    [Header("Slideshow Settings")]
    [Tooltip("Image component used for slideshow")]
    public Image targetImage;

    [Tooltip("Sprites used in slideshow")]
    public Sprite[] slideSprites;

    [Tooltip("Interval between slideshow images (seconds)")]
    public float slideInterval = 0.5f;

    [Header("SE")]
    [SerializeField] private string transitionSeKey = SeKeys.LobbyToGameTransition;

    [Header("Countdown")]
    [SerializeField] private bool useCountdown = true;
    [SerializeField] private float countdownSeconds = 5f;
    [SerializeField] private CountTimer countdownTimer;

    private bool isTransitioning;

    public void OnStartGameClicked()
    {
        if (isTransitioning)
        {
            return;
        }

        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        isTransitioning = true;

        // Play transition SE first, then start loading flow.
        yield return StartCoroutine(SeController.PlayAndWait(transitionSeKey, 0f));

        if (waitingPanel != null)
        {
            waitingPanel.SetActive(true);
        }

        if (useCountdown)
        {
            if (countdownTimer == null && waitingPanel != null)
            {
                countdownTimer = waitingPanel.GetComponentInChildren<CountTimer>(true);
            }

            if (countdownTimer != null)
            {
                float seconds = countdownSeconds > 0f ? countdownSeconds : countdownTimer.startCount;
                countdownTimer.BeginCountdown(seconds);
            }
        }

        if (FakeCursor.Instance != null)
        {
            FakeCursor.Instance.SetVisible(false);
        }

        Coroutine slideshow = null;
        if (targetImage != null && slideSprites != null && slideSprites.Length > 0)
        {
            slideshow = StartCoroutine(SlideshowRoutine());
        }

        float startTime = Time.unscaledTime;
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            if (op.progress >= 0.9f)
            {
                float elapsedTime = Time.unscaledTime - startTime;
                bool minWaitDone = elapsedTime >= minWaitTime;
                bool countdownDone = !useCountdown || countdownTimer == null || countdownTimer.IsFinished;

                if (minWaitDone && countdownDone)
                {
                    if (slideshow != null)
                    {
                        StopCoroutine(slideshow);
                    }

                    op.allowSceneActivation = true;
                }
            }

            yield return null;
        }

        isTransitioning = false;
    }

    private IEnumerator SlideshowRoutine()
    {
        int index = 0;
        WaitForSeconds wait = new WaitForSeconds(slideInterval);

        while (true)
        {
            targetImage.sprite = slideSprites[index];
            index = (index + 1) % slideSprites.Length;
            yield return wait;
        }
    }
}
