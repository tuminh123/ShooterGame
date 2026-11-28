using UnityEngine;
using Zenject;

/// <summary>
/// Điểm phát âm cho UI, gắn một lần trên Canvas gốc. View con (kể cả clone tạo từ prefab bằng <c>Instantiate</c>)
/// tìm nó bằng <c>GetComponentInParent</c> thay vì tự inject <see cref="IAudio"/>.
/// </summary>
/// <remarks>
/// Lý do: thẻ nhân vật, thẻ đồ, thẻ mission... được <c>Instantiate</c> thường, Zenject không inject chúng,
/// nên <see cref="ButtonSound"/> trên prefab sẽ không có IAudio. Component trong scene thì được SceneContext inject sẵn.
/// </remarks>
public sealed class UIAudio : MonoBehaviour
{
    private IAudio audioPlayer;

    [Inject]
    private void Construct(IAudio audio) => audioPlayer = audio;

    /// <summary>Phát một âm UI; trước khi inject xong thì bỏ qua.</summary>
    public void Play(AudioId id) => audioPlayer?.Play(id);
}
