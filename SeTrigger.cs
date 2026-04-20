using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SeTrigger : MonoBehaviour
{
    [SerializeField] private string seKey;
    [SerializeField] private bool waitForSeBeforeInvoke;
    [SerializeField] private float minimumWaitSeconds;
    [SerializeField] private UnityEvent onAfterSe;

    public void Play()
    {
        var se = SeController.GetOrFindInstance();
        if (se != null)
        {
            se.Play(seKey);
        }
    }

    public void PlayAndInvoke()
    {
        if (!waitForSeBeforeInvoke)
        {
            Play();
            onAfterSe?.Invoke();
            return;
        }

        StartCoroutine(PlayAndInvokeRoutine());
    }

    private IEnumerator PlayAndInvokeRoutine()
    {
        yield return StartCoroutine(SeController.PlayAndWait(seKey, minimumWaitSeconds));
        onAfterSe?.Invoke();
    }
}
