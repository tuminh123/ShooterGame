using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Nút debug của panel test: mỗi lần bấm "gacha" ra một nhân vật Player ngẫu nhiên trong Character Database.
/// Chưa có thì sở hữu luôn, đã có thì thành một bản trùng (để test thẻ xám, ghép, phân rã và nâng sao bằng bản trùng).
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class GachaCharacterButton : MonoBehaviour
{
    #region Dependencies And State

    private IPlayerSelection selection;
    private ICharacterCollection collection;
    private Button button;

    #endregion

    #region Injection And Initialization

    /// <summary>SceneContext inject danh sách nhân vật Player và bộ sưu tập.</summary>
    [Inject]
    public void Construct(IPlayerSelection playerSelection, ICharacterCollection characterCollection)
    {
        selection = playerSelection;
        collection = characterCollection;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Roll);
    }

    private void OnDestroy() => button.onClick.RemoveListener(Roll);

    #endregion

    #region Gacha

    private void Roll()
    {
        if (selection == null || collection == null || selection.PlayableCharacters.Count == 0)
            return;
        CharacterDataSO character = selection.PlayableCharacters[Random.Range(0, selection.PlayableCharacters.Count)];
        CharacterGrantResult result = collection.Grant(character.CharacterType, out bool saved);
        Debug.Log($"[Test] Gacha hero: {character.DisplayName} → {result}.", this);
        if (!saved)
            Debug.LogWarning("[Test] Gacha hero was applied but not saved yet; SaveManager will retry.", this);
    }

    #endregion
}
