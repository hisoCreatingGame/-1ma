using UnityEngine;
using unityroom.Api;

public class UnityroomLoader : MonoBehaviour
{
    [Header("UnityroomApiClient��Prefab���Z�b�g")]
    [SerializeField] private GameObject unityroomApiClientPrefab;

    private void Awake()
    {
        // ���C���|�C���g
        // UnityroomApiClient.Instance ���ĂԂƁu������Ȃ��v�G���[���o�邽�߁A
        // ����� FindAnyObjectByType (�܂��� FindObjectOfType) �ŐÂ��ɒT���܂��B
        
        // Unity 2023.1�ȍ~�̏ꍇ
        var existingClient = FindAnyObjectByType<UnityroomApiClient>();
        
        // �� �����Â�Unity���g���Ă��ď�L���G���[�ɂȂ�ꍇ�́A�ȉ����g���Ă�������
        // var existingClient = FindObjectOfType<UnityroomApiClient>();

        if (existingClient == null)
        {
            if (unityroomApiClientPrefab != null)
            {
                Instantiate(unityroomApiClientPrefab);
                Debug.Log("UnityroomApiClient �𐶐����܂���");
            }
            else
            {
                Debug.LogError("UnityroomLoader: �v���n�u���Z�b�g����Ă��܂���IInspector���m�F���Ă��������B");
            }
        }
        else
        {
            Debug.Log("UnityroomApiClient �͊��ɑ��݂��邽�߁A�������X�L�b�v���܂�");
        }
    }
}
