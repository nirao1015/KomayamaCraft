using System;
using System.Collections.Generic;

/// <summary>
/// セーブデータへ格納する解放フラグの最小レコード。
/// 納品数、所持数、完了報告状態はそれぞれの進行データで別に保存する。
/// </summary>
[Serializable]
public struct UnlockStateRecord
{
    public string unlockId;
    public bool isUnlocked;

    public string UnlockId => unlockId;
    public bool IsUnlocked => isUnlocked;

    public UnlockStateRecord(string unlockId, bool isUnlocked)
    {
        this.unlockId = unlockId;
        this.isUnlocked = isUnlocked;
    }

    public void CollectValidationErrors(string ownerName, List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        if (!GameDataId.IsValidDefinitionId(unlockId, GameDataId.UnlockPrefix))
        {
            errors.Add($"{ownerName}: Saved unlock ID '{unlockId}' is invalid.");
        }
    }
}
