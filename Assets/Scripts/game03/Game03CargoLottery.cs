using UnityEngine;

/// <summary>
/// エマージェンシーカーゴの中身抽選（仕様 ■5: 45% / 20% / 35%）。
/// </summary>
public static class Game03CargoLottery
{
    private static readonly float[] Weights = { 0.45f, 0.20f, 0.35f };

    public static Game03CargoItemKind DrawCargoContents()
    {
        float r = Random.Range(0f, 1f);
        float acc = 0f;
        for (int i = 0; i < Weights.Length; i++)
        {
            acc += Weights[i];
            if (r < acc)
            {
                return (Game03CargoItemKind)i;
            }
        }

        return Game03CargoItemKind.Medkit;
    }
}
