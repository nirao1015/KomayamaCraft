using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UnitUG 中の配信コメント風テキスト（OjComment 内を右→左に流す）。game02 の浮遊コメント相当を game03 専用実装。
/// </summary>
[DisallowMultipleComponent]
public sealed class Game03UnitUgCommentTickerController : MonoBehaviour
{
    private const float HorizontalMargin = 120f;

    [Header("Refs")]
    [SerializeField] private RectTransform commentParent;
    [SerializeField, Tooltip("未設定時は子の DefaultText (TMP) / Text (TMP) をテンプレートに使い非表示にする。")]
    private GameObject commentLinePrefab;

    [Header("Timing")]
    [SerializeField] private float spawnIntervalMinSeconds = 0.35f;
    [SerializeField] private float spawnIntervalMaxSeconds = 1.05f;
    [SerializeField] private float speedBasePixelsPerSecond = 280f;
    [SerializeField] private float speedPerCharacter = 12f;
    [SerializeField] private float minAnchoredY = -260f;
    [SerializeField] private float maxAnchoredY = 260f;
    [SerializeField] private int maxConcurrentComments = 12;

    [Header("Comments")]
    [SerializeField] private List<string> commentCandidates = new List<string>
    {
        "それ積むの！？",
        "違法改造だろ",
        "終わったな敵",
        "メーカー保証消えた",
        "そのパーツ使うんかい",
        "いやそれ敵のだろ",
        "パクってて草",
        "現地調達つよい",
        "拾い食いメカ改造",
        "もう正規品じゃない",
        "敵の技術を即実装",
        "判断が早い",
        "迷いゼロで草",
        "それ絶対やばいやつ",
        "なんか光ってるんですが",
        "爆発しない？大丈夫？",
        "ちゃんと説明書読んだ？",
        "そのコアまだ熱いって",
        "火花めっちゃ出てる",
        "そのまま差すなｗ",
        "スロット合うんだ…",
        "規格一致してて草",
        "互換性あるのかよ",
        "プラグアンドプレイ草",
        "USB感覚で積むな",
        "敵さん涙目",
        "これもうラスボス側",
        "技術ツリー乗っ取ってて草",
        "敵の研究成果を横取り！",
        "配信映え優先改造きた",
        "視聴者が一番喜ぶやつ",
        "コメント欄お祭り状態",
        "これBANされない？",
        "敵設計者が泣いてる",
        "その改造、倫理審査通った？",
        "なんで即接続できるんだよ",
        "今ので戦力差ひっくり返った",
        "敵の心が折れる音した",
        "ご視聴ありがとうございます、敵終了です",
        "敵性炉心の鹵獲を確認",
        "未知規格の接続を開始",
        "動力系統に異常値",
        "オーバークロック入った",
        "臨界まであと何秒？",
        "出力制限解除された？",
        "冷却追いついてなくない？",
        "そのリアクター生きてるぞ",
        "敵AIの制御核じゃん",
        "指揮ユニット抜いたか",
        "敵機の戦術アルゴリズム奪取",
        "フレーム負荷やばそう",
        "それ軍用パーツだろ",
        "未承認改修を検知",
        "エネルギー逆流してる",
        "炉心直結はまずい",
        "侵略兵器の部品流用きた",
        "敵技術のリバースエンジニアリング",
        "システム権限奪取完了",
        "制御プロトコル上書き中",
        "互換性あるのが怖い",
        "敵機構成データ吸ってる？",
        "武装出力が段違いになった",
        "うおおおおおお",
        "きたああああ",
        "それ拾うの！？",
        "いや草",
        "判断が早いｗ",
        "それ使っていいんだｗ",
        "えっそれ本当に大丈夫？",
        "火花出てる火花出てる",
        "そうはならんやろ",
        "なっとるやろがい",
        "その発想はなかった",
        "いけるんかそれ！？",
        "配信的には100点",
        "コメント欄こういうの好き",
        "これ盛り上がるやつだ",
        "敵の研究成果いただきます",
        "つよそう（小並感）",
        "ラスボスの装備してない？",
        "今回の主役そのコアだろ",
        "配信事故かと思った",
    };

    private bool tickerActive;
    private int activeCommentCount;
    private Coroutine spawnRoutine;

    private void Awake()
    {
        ResolveRefs();
        EnsureRectMask2D();
        HideTemplateIfSceneObject();
    }

    public void BeginTicker()
    {
        ResolveRefs();
        if (commentParent == null)
        {
            return;
        }

        tickerActive = true;
        ClearSpawnedComments();
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
        }

        spawnRoutine = StartCoroutine(CoSpawnLoop());
    }

    public void StopTicker()
    {
        tickerActive = false;
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        ClearSpawnedComments();
    }

    private IEnumerator CoSpawnLoop()
    {
        List<string> pool = BuildCommentPool();
        while (tickerActive && pool.Count > 0)
        {
            if (activeCommentCount < Mathf.Max(1, maxConcurrentComments))
            {
                string message = pool[Random.Range(0, pool.Count)];
                SpawnOneComment(message);
            }

            float gap = Random.Range(
                Mathf.Min(spawnIntervalMinSeconds, spawnIntervalMaxSeconds),
                Mathf.Max(spawnIntervalMinSeconds, spawnIntervalMaxSeconds));
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, gap));
        }

        spawnRoutine = null;
    }

    private List<string> BuildCommentPool()
    {
        List<string> pool = new List<string>(commentCandidates != null ? commentCandidates.Count : 0);
        if (commentCandidates == null)
        {
            return pool;
        }

        for (int i = 0; i < commentCandidates.Count; i++)
        {
            string s = commentCandidates[i];
            if (!string.IsNullOrWhiteSpace(s))
            {
                pool.Add(s.Trim());
            }
        }

        return pool;
    }

    private void SpawnOneComment(string message)
    {
        if (commentParent == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        RectTransform rt = InstantiateCommentRect(message);
        if (rt == null)
        {
            return;
        }

        activeCommentCount++;
        float y = Random.Range(
            Mathf.Min(minAnchoredY, maxAnchoredY),
            Mathf.Max(minAnchoredY, maxAnchoredY));
        GetHorizontalExtents(commentParent, out float leftX, out float rightX);

        rt.anchoredPosition = new Vector2(0f, y);
        RefreshCommentLayout(rt, message);

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(commentParent, rt);
        float pastRight = rightX + HorizontalMargin;
        rt.anchoredPosition = new Vector2(pastRight - bounds.min.x, y);

        float speed = Mathf.Max(10f, speedBasePixelsPerSecond + message.Length * speedPerCharacter);
        float exitPastLeft = leftX - HorizontalMargin;
        StartCoroutine(CoMoveComment(rt, speed, exitPastLeft));
    }

    private static void GetHorizontalExtents(RectTransform parent, out float leftX, out float rightX)
    {
        Rect rect = parent.rect;
        leftX = rect.xMin;
        rightX = rect.xMax;
    }

    private static void RefreshCommentLayout(RectTransform rt, string message)
    {
        TextMeshProUGUI tmp = rt.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = message;
            tmp.ForceMeshUpdate(true);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    private RectTransform InstantiateCommentRect(string message)
    {
        GameObject go;
        if (commentLinePrefab != null)
        {
            go = Instantiate(commentLinePrefab, commentParent, false);
            go.SetActive(true);
        }
        else
        {
            go = new GameObject("UnitUgCommentLine", typeof(RectTransform));
            go.transform.SetParent(commentParent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            tmp.fontSize = 42f;
            tmp.text = message;
        }

        TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = message;
            text.raycastTarget = false;
        }

        RectTransform rt = go.transform as RectTransform;
        if (rt == null)
        {
            Destroy(go);
            return null;
        }

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private IEnumerator CoMoveComment(RectTransform rt, float speedPixelsPerSecond, float exitPastLeftX)
    {
        if (rt == null || commentParent == null)
        {
            yield break;
        }

        while (tickerActive && rt != null)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(commentParent, rt);
            if (bounds.max.x < exitPastLeftX)
            {
                break;
            }

            Vector2 position = rt.anchoredPosition;
            position.x -= speedPixelsPerSecond * Mathf.Max(0f, Time.unscaledDeltaTime);
            rt.anchoredPosition = position;
            yield return null;
        }

        if (rt != null)
        {
            Destroy(rt.gameObject);
        }

        activeCommentCount = Mathf.Max(0, activeCommentCount - 1);
    }

    private void ClearSpawnedComments()
    {
        if (commentParent == null)
        {
            activeCommentCount = 0;
            return;
        }

        for (int i = commentParent.childCount - 1; i >= 0; i--)
        {
            Transform child = commentParent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (commentLinePrefab != null && child.gameObject == commentLinePrefab)
            {
                continue;
            }

            Destroy(child.gameObject);
        }

        activeCommentCount = 0;
    }

    private void ResolveRefs()
    {
        if (commentParent == null)
        {
            commentParent = transform as RectTransform;
        }

        if (commentLinePrefab == null)
        {
            Transform template = transform.Find("DefaultText (TMP)");
            if (template == null)
            {
                template = transform.Find("Text (TMP)");
            }

            if (template != null)
            {
                commentLinePrefab = template.gameObject;
            }
        }
    }

    private void EnsureRectMask2D()
    {
        if (commentParent == null)
        {
            return;
        }

        if (commentParent.GetComponent<RectMask2D>() == null)
        {
            commentParent.gameObject.AddComponent<RectMask2D>();
        }
    }

    private void HideTemplateIfSceneObject()
    {
        if (commentLinePrefab == null || !commentLinePrefab.scene.IsValid())
        {
            return;
        }

        commentLinePrefab.SetActive(false);
    }

    private void OnDisable()
    {
        StopTicker();
    }
}
