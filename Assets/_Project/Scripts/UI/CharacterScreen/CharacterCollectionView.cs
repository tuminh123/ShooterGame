using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// View màn Collection: lưới thẻ nhân vật, đúng một thẻ cho mỗi nhân vật Player trong Character Database.
/// Tự sinh và tái dùng thẻ từ prefab. Không gọi service. Về Home bằng thanh bar dưới (<see cref="HomeNavigationBar"/>).
/// </summary>
public sealed class CharacterCollectionView : MonoBehaviour
{
    #region References

    [SerializeField] private CharacterCardView cardPrefab;
    [Tooltip("Content của Scroll View (có GridLayoutGroup).")]
    [SerializeField] private RectTransform content;
    [Tooltip("Chữ \"đã có/tổng\".")]
    [SerializeField] private TMP_Text countText;

    #endregion

    #region Events And State

    /// <summary>Chạm vào thẻ thứ <c>index</c> (theo thứ tự model truyền vào).</summary>
    public event Action<int> CardClicked;

    private readonly List<CharacterCardView> cards = new List<CharacterCardView>(); // Thẻ đã sinh, tái dùng giữa các lần Bind.

    #endregion

    #region Presentation

    /// <summary>Vẽ lại lưới thẻ và chữ đếm.</summary>
    public void Bind(IReadOnlyList<CharacterCardModel> models, int owned, int total)
    {
        countText.text = owned.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);

        // Đúng một thẻ cho mỗi nhân vật trong Character Database; không lấp thẻ trống cho đủ hàng.
        EnsureCards(models.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < models.Count;
            cards[i].gameObject.SetActive(used);
            if (used) cards[i].Bind(models[i]);
        }
    }

    // Sinh thêm thẻ khi thiếu; mỗi thẻ giữ chỉ số vị trí của nó để báo CardClicked.
    private void EnsureCards(int count)
    {
        while (cards.Count < count)
        {
            CharacterCardView card = Instantiate(cardPrefab, content);
            int index = cards.Count;
            card.Clicked += () => CardClicked?.Invoke(index);
            cards.Add(card);
        }
    }

    #endregion
}
