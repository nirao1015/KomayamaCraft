using UnityEngine;

/// <summary>
/// menu03 / game03 いずれかで DDOL の <see cref="Game03MetaProgressController"/> を確実に生成し、ディスクから初期化する。
/// プレハブルートには <see cref="Game03MetaProgressController"/> のみ付与すること。
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class Game03MetaPersistenceSceneBootstrap : MonoBehaviour
{
    [SerializeField, Tooltip("Game03MetaProgressController が付いたプレハブ。未設定時は初期化できません。")]
    private GameObject metaPersistencePrefab;

    private void Awake()
    {
        if (Game03MetaProgressController.Instance != null)
        {
            return;
        }

        if (metaPersistencePrefab == null)
        {
            Debug.LogError("[Game03MetaPersistenceSceneBootstrap] metaPersistencePrefab が未設定です。", this);
            return;
        }

        Instantiate(metaPersistencePrefab);
    }

    private void Start()
    {
        Game03MetaProgressController instance = Game03MetaProgressController.Instance;
        if (instance == null)
        {
            Debug.LogError("[Game03MetaPersistenceSceneBootstrap] Meta コントローラを生成できませんでした。", this);
            return;
        }

        instance.EnsureInitializedFromDisk(writeNewSaveIfMissing: false);
    }
}
