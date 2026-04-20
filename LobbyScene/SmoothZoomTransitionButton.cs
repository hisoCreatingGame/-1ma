using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement; // �V�[���J�ڂɕK�{
using System.Collections;

public class SmoothZoomTransitionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("�J�ڐݒ�")]
    [SerializeField] private string nextSceneName = "GameScene"; // �ړ���̃V�[����
    [SerializeField] private float waitTime = 1.0f; // �N���b�N��̑ҋ@����
    [SerializeField] private string transitionSeKey = SeKeys.LobbyToGameTransition;

    [Header("�Y�[���ݒ�")]
    [SerializeField] private float zoomScale = 1.2f;   // �Y�[���{��
    [SerializeField] private float duration = 0.2f;    // �ω��ɂ����鎞��
    [SerializeField] private float startDelay = 0.0f;  // �Y�[���J�n�܂ł̒x��

    private Vector3 defaultScale;
    private Coroutine zoomCoroutine;
    private bool isTransitioning = false; // �A�Ŗh�~�p�t���O

    void Start()
    {
        defaultScale = transform.localScale;

        // ���������̖����ݒ�i�摜��Read/Write Enabled��Y�ꂸ�ɁI�j
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    // �J�[�\�����������
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isTransitioning) return; // �J�ڒ��͔��������Ȃ�

        if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
        zoomCoroutine = StartCoroutine(ScaleTo(defaultScale * zoomScale, startDelay));
    }

    // �J�[�\�������ꂽ��
    public void OnPointerExit(PointerEventData eventData)
    {
        if (isTransitioning) return; // �J�ڒ��͔��������Ȃ�

        if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
        zoomCoroutine = StartCoroutine(ScaleTo(defaultScale, 0f));
    }

    // �N���b�N���ꂽ��
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isTransitioning) return; // ���ɉ�����Ă����牽�����Ȃ�

        // �J�ڏ������J�n
        StartCoroutine(WaitAndLoad());
    }

    // 1�b�҂��ă��[�h����R���[�`��
    IEnumerator WaitAndLoad()
    {
        isTransitioning = true; // �{�^���𖳌����i�A�Ŗh�~�j

        // �����ɃN���b�N���Đ��Ȃǂ����Ă�OK

        // �w�莞�ԑ҂i1�b�j
        yield return StartCoroutine(SeController.PlayAndWait(transitionSeKey, waitTime));

        // �V�[�������[�h
        SceneManager.LoadScene(nextSceneName);
    }

    // ���炩�ɃT�C�Y��ς���R���[�`��
    IEnumerator ScaleTo(Vector3 targetScale, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        Vector3 startScale = transform.localScale;
        float time = 0;

        while (time < duration)
        {
            transform.localScale = Vector3.Lerp(startScale, targetScale, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;
    }
}
