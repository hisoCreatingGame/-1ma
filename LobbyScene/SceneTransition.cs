using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // �R���[�`���ɕK�v

public class SceneTransition : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // �J�ڐ�̃V�[����
    [SerializeField] private string transitionSeKey = SeKeys.LobbyBackButton;
    [SerializeField] private float waitTime = 1.0f;

    // �{�^���ɓo�^����֐�
    public void OnTransitionButtonPressed()
    {
        StartCoroutine(WaitAndLoad());
    }

    IEnumerator WaitAndLoad()
    {
        // �����ɃN���b�N���Ȃǂ��Đ�����R�[�h�����Ă��ǂ��ł���

        // 1�b�҂�
        yield return StartCoroutine(SeController.PlayAndWait(transitionSeKey, waitTime));

        // �V�[�������[�h
        SceneManager.LoadScene(nextSceneName);
    }
}
