using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

[RequireComponent(typeof(Image))]            // Image�R���|�[�l���g�K�{
[RequireComponent(typeof(SmoothHoverObject))] // SmoothHoverObject�R���|�[�l���g�K�{
public class YakumanAchievementSlot : MonoBehaviour, IPointerEnterHandler
{
    [Header("���ѐݒ�")]
    [Tooltip("MahjongGameManager�Œ�`�����𖞂̖��O�Ɗ��S�Ɉ�v�����Ă��������i��: ���m���o�j")]
    public string targetYakumanName;

    [Header("�摜�ݒ�")]
    public Sprite unlockedSprite; // �������ꂽ���̉摜�i�J���[�Ȃǁj
    public Sprite lockedSprite;   // �������̎��̉摜�i�V���G�b�g�⌮�A�C�R���Ȃǁj
    [Header("SE")]
    [SerializeField] private AudioSource touchSeSource;
    [FormerlySerializedAs("touchSeClip")]
    [SerializeField] private AudioClip unlockedTouchSeClip;
    [SerializeField] private AudioClip lockedTouchSeClip;
    [FormerlySerializedAs("touchSeVolume")]
    [SerializeField, Range(0f, 1f)] private float unlockedTouchSeVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float lockedTouchSeVolume = 1f;

    private bool _isUnlocked;

    private void Start()
    {
        SetupTouchSeSource();
        UpdateSlotStatus();
    }

    public void UpdateSlotStatus()
    {
        // 1. �ۑ����ꂽ���уf�[�^�̊m�F
        // �L�[�� Manager �ŕۑ����� "Yakuman_" + �𖞖�
        string key = "Yakuman_" + targetYakumanName;
        bool isUnlocked = PlayerPrefs.GetInt(key, 0) == 1;
        _isUnlocked = isUnlocked;

        // 2. �摜�̐؂�ւ�
        Image targetImage = GetComponent<Image>();
        if (targetImage != null)
        {
            if (isUnlocked)
            {
                if (unlockedSprite != null) targetImage.sprite = unlockedSprite;
                targetImage.color = Color.white; // �{���̐F
            }
            else
            {
                if (lockedSprite != null) targetImage.sprite = lockedSprite;
                else
                {
                    // ���b�N�摜���ݒ肳��Ă��Ȃ��ꍇ�́A��������Ȃǂ̑Ή�
                    targetImage.color = Color.black; 
                }
            }
        }

        // 3. �z�o�[���̃e�L�X�g�؂�ւ� (SmoothHoverObject�A�g)
        SmoothHoverObject hoverScript = GetComponent<SmoothHoverObject>();
        if (hoverScript != null)
        {
            if (isUnlocked)
            {
                // �����ς݂Ȃ�𖞖���\��
                hoverScript.SetDisplayName(targetYakumanName);
            }
            else
            {
                // �������Ȃ� "???" �ɂ���
                hoverScript.SetDisplayName("???");
            }
        }
    }
    
    // �f�o�b�O�p: �����I�Ƀ��b�N��Ԃ����Z�b�g�������ꍇ�Ɏg�p

    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayTouchSeForCurrentStatus();
    }

    public void ResetStatus()
    {
         string key = "Yakuman_" + targetYakumanName;
         PlayerPrefs.DeleteKey(key);
         UpdateSlotStatus();
    }

    private void SetupTouchSeSource()
    {
        if (touchSeSource == null)
        {
            touchSeSource = GetComponent<AudioSource>();
        }

        if (touchSeSource == null)
        {
            touchSeSource = gameObject.AddComponent<AudioSource>();
        }

        touchSeSource.playOnAwake = false;
        touchSeSource.loop = false;
    }

    private void PlayTouchSeForCurrentStatus()
    {
        if (touchSeSource == null)
        {
            return;
        }

        AudioClip clip = _isUnlocked ? unlockedTouchSeClip : lockedTouchSeClip;
        float volume = _isUnlocked ? unlockedTouchSeVolume : lockedTouchSeVolume;

        // どちらか未設定でも最低限鳴るようにフォールバック
        if (clip == null)
        {
            clip = _isUnlocked ? lockedTouchSeClip : unlockedTouchSeClip;
            volume = _isUnlocked ? lockedTouchSeVolume : unlockedTouchSeVolume;
        }

        if (clip == null)
        {
            return;
        }

        touchSeSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
