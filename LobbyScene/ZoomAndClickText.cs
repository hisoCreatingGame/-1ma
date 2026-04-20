using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class ZoomAndClickText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("�ݒ�")]
    [SerializeField] private TMP_Text outputText;
    [SerializeField, TextArea] private string message;
    [SerializeField] private float zoomScale = 1.2f;
    [Header("SE")]
    [SerializeField] private AudioSource clickSeSource;
    [SerializeField] private AudioClip clickSeClip;
    [SerializeField, Range(0f, 1f)] private float clickSeVolume = 1f;

    private Vector3 defaultScale;
    private Coroutine _currentCoroutine; // ���ݎ��s���̃R���[�`����ۑ�����ϐ�

    void Start()
    {
        defaultScale = transform.localScale;
        SetupClickSeSource();

        // ���������̃N���b�N���ߐݒ�
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.3f;
        }
    }

    // �z�o�[���̏���
    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayClickSe();
        transform.localScale = defaultScale * zoomScale;
    }

    // �z�o�[�I�����̏���
    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = defaultScale;
    }

    // ���C���ӏ�: �C���^�[�t�F�[�X�̎����͕K�� void �ɂ���K�v������܂�
    public void OnPointerClick(PointerEventData eventData)
    {
        if (outputText != null)
        {
            // �������Ƀ��b�Z�[�W�\���̃^�C�}�[�������Ă�����A��x�~�߂�i�A�ő΍�j
            // ��������Ȃ��ƁA�A�ł����Ƃ��ɕ������_�ł����葁���������肵�Ă��܂��܂�
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
            }

            // �R���[�`���i�x�������j���J�n����
            _currentCoroutine = StartCoroutine(ShowMessageRoutine());
        }
    }

    // ���C���ӏ�: ���ۂ̒x���������s���R���[�`����ʂ̃��\�b�h�Ƃ��Ē�`
    private IEnumerator ShowMessageRoutine()
    {
        // 1. �e�L�X�g��\��
        outputText.text = message;

        // 2. 2�b�҂�
        yield return new WaitForSeconds(2.0f);

        // 3. �e�L�X�g������
        outputText.text = "";
        
        // 4. ���������̂ŕϐ����N���A
        _currentCoroutine = null;
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
