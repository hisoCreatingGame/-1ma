using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class SmoothZoomButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("�e�L�X�g�ݒ�")]
    [SerializeField] private TMP_Text outputText;
    [SerializeField, TextArea] private string message;

    [Header("�Y�[���ݒ�")]
    [SerializeField] private float zoomScale = 1.2f;   // �ڕW�̔{��
    [SerializeField] private float duration = 0.2f;    // �ω��ɂ����鎞�ԁi�b�j
    [SerializeField] private float startDelay = 0.0f;  // �J�n����܂ł̒x���i�b�j
    [Header("SE")]
    [SerializeField] private AudioSource clickSeSource;
    [SerializeField] private AudioClip clickSeClip;
    [SerializeField, Range(0f, 1f)] private float clickSeVolume = 1f;

    private Vector3 defaultScale;
    
    // �Y�[���p�̃R���[�`��
    private Coroutine currentZoomCoroutine; 
    
    // ���ǉ�: ���b�Z�[�W�\���p�̃R���[�`���i�Y�[���p�Ƃ͕�����K�v������܂��j
    private Coroutine _messageCoroutine;

    void Start()
    {
        defaultScale = transform.localScale;
        SetupClickSeSource();

        // ���ߕ����̔��菜�O�ݒ�
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    // --- �Y�[������ (OnPointerEnter / OnPointerExit) ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayClickSe();
        if (currentZoomCoroutine != null) StopCoroutine(currentZoomCoroutine);
        currentZoomCoroutine = StartCoroutine(ScaleTo(defaultScale * zoomScale, startDelay));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentZoomCoroutine != null) StopCoroutine(currentZoomCoroutine);
        currentZoomCoroutine = StartCoroutine(ScaleTo(defaultScale, 0f));
    }

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

    // --- �N���b�N���̃e�L�X�g���� (OnPointerClick) ---

    // ���C���ӏ�: �N���b�N����2�b�Ԃ����e�L�X�g��\������
    public void OnPointerClick(PointerEventData eventData)
    {
        if (outputText != null)
        {
            // �A�ő΍�: ���Ƀ��b�Z�[�W�\���̃^�C�}�[�������Ă����烊�Z�b�g����
            if (_messageCoroutine != null)
            {
                StopCoroutine(_messageCoroutine);
            }

            // �V�������b�Z�[�W�\���R���[�`�����J�n
            _messageCoroutine = StartCoroutine(ShowMessageRoutine());
        }
    }

    // ���ǉ�: �e�L�X�g��\������2�b��ɏ����R���[�`��
    private IEnumerator ShowMessageRoutine()
    {
        // 1. �e�L�X�g��\��
        outputText.text = message;

        // 2. 2�b�҂�
        yield return new WaitForSeconds(2.0f);

        // 3. �e�L�X�g������
        outputText.text = "";
        
        _messageCoroutine = null;
    }

    private void SetupClickSeSource()
    {
        if (clickSeSource == null)
        {
            clickSeSource = GetComponent<AudioSource>();
        }

        if (clickSeSource == null)
        {
            clickSeSource = gameObject.AddComponent<AudioSource>();
        }

        clickSeSource.playOnAwake = false;
        clickSeSource.loop = false;
    }

    private void PlayClickSe()
    {
        if (clickSeSource == null || clickSeClip == null)
        {
            return;
        }

        clickSeSource.PlayOneShot(clickSeClip, Mathf.Clamp01(clickSeVolume));
    }
}
