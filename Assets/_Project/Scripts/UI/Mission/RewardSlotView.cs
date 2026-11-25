using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Một ô thưởng (thẻ mission và popup nhận thưởng).</summary>
public sealed class RewardSlotView : MonoBehaviour
{
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    /// <summary>Hiện một phần thưởng.</summary>
    public void Bind(RewardSlotModel model)
    {
        frame.color = model.Frame;
        icon.sprite = model.Icon;
        icon.enabled = model.Icon != null;
        icon.preserveAspect = true;
        label.text = model.Label;
    }

    /// <summary>Hiện một hàng ô thưởng; thiếu thì sinh thêm từ prefab, thừa thì tắt để tái dùng.</summary>
    public static void BindAll(List<RewardSlotView> slots, RewardSlotView prefab, Transform parent, IReadOnlyList<RewardSlotModel> models)
    {
        while (slots.Count < models.Count) slots.Add(Instantiate(prefab, parent));
        for (int i = 0; i < slots.Count; i++)
        {
            bool used = i < models.Count;
            slots[i].gameObject.SetActive(used);
            if (used) slots[i].Bind(models[i]);
        }
    }
}
