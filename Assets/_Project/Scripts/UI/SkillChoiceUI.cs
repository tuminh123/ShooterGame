using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using Zenject;

// Gắn trên object UI luôn hoạt động (Play_UI_Action), không đặt trên modal đang tắt.
// Chỉ render và điều phối lựa chọn; luật cấp kế tiếp nằm trong SkillChoicePool.
public sealed class SkillChoiceUI : MonoBehaviour
{
    [Serializable]
    private sealed class Card
    {
        public Button button;
        public Image icon;
        public TMP_Text title;
        public TMP_Text description;
        public Transform gemsOff;
        public Transform gemsOn;

        public void Show(SkillChoiceOffer offer)
        {
            button.gameObject.SetActive(true);
            button.interactable = true;
            icon.sprite = offer.Definition.Icon;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            title.text = string.IsNullOrWhiteSpace(offer.Definition.DisplayName)
                ? offer.Definition.name : offer.Definition.DisplayName;
            string action = offer.CurrentLevel == 0 ? "NEW SKILL - Lv. 1" :
                $"UPGRADE - Lv. {offer.CurrentLevel} > {offer.OfferedLevel}";
            description.text = action + "\n" + offer.Definition.GetRunLevelDescription(offer.OfferedLevel);
            // Group_GreadGems đã có Off/On, mỗi hàng 5 Gem. Không nhân bản cả group.
            // Off = số cấp tối đa; On = cấp sẽ nhận nếu chọn. Skill mới luôn bắt đầu một sao.
            for (int i = 0; i < gemsOff.childCount; i++) gemsOff.GetChild(i).gameObject.SetActive(i < offer.MaxLevel);
            for (int i = 0; i < gemsOn.childCount; i++) gemsOn.GetChild(i).gameObject.SetActive(i < offer.OfferedLevel);
        }
    }

    [SerializeField] private GameObject modalRoot;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private Card[] cards = new Card[3];
    [SerializeField] private SkillDataSO[] availableSkills = Array.Empty<SkillDataSO>();
    private readonly SkillChoicePool pool = new SkillChoicePool();
    private readonly SkillChoiceProgress progress = new SkillChoiceProgress();
    private readonly List<SkillChoiceOffer> offers = new List<SkillChoiceOffer>(3);
    private LevelManager levels;
    private SkillManager skills;
    private PlayerManager players;
    private GameplayPauseService pauses;
    private CharacterView owner;
    private IDisposable pauseLease;
    private GameObject previousSelection;
    private bool initialized;
    private bool subscribed;
    private bool open;
    private bool selecting;
    private bool refreshPending;
    private int openedFrame;
    private float nextInputAt;
    private UnityAction[] clicks;

    [Inject]
    public void Construct(LevelManager levels, SkillManager skills, PlayerManager players, GameplayPauseService pauses)
    {
        this.levels = levels; this.skills = skills; this.players = players; this.pauses = pauses;
        InitializeCards();
        if (isActiveAndEnabled) Subscribe();
    }

    private void Awake() => InitializeCards();

    private void InitializeCards()
    {
        if (initialized) return;
        if (modalRoot == null || cards == null || cards.Length != 3)
            throw new InvalidOperationException("SkillChoiceUI requires its modal and exactly three cards.");
        clicks = new UnityAction[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            Card card = cards[i];
            if (card == null || card.button == null || card.icon == null || card.title == null ||
                card.description == null || card.gemsOff == null || card.gemsOn == null)
                throw new InvalidOperationException($"Assign all SkillChoiceUI card {i + 1} references.");
            int index = i;
            clicks[i] = () => Choose(index);
            card.button.onClick.AddListener(clicks[i]);
        }
        initialized = true;
        modalRoot.SetActive(false);
    }

    private void OnEnable() { if (levels != null) Subscribe(); }

    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        levels.RunLevelChanged += OnRunLevelChanged;
        levels.RunReset += CancelChoices;
        players.PlayerChanged += OnPlayerChanged;
        OnPlayerChanged(players.CurrentPlayer);
    }

    private void OnPlayerChanged(CharacterView character)
    {
        if (owner != null) { owner.Died -= OnPlayerDied; owner.Released -= OnPlayerReleased; }
        CancelChoices();
        owner = character;
        if (owner != null) { owner.Died += OnPlayerDied; owner.Released += OnPlayerReleased; }
    }

    private void OnPlayerDied(CharacterView character) => CancelChoices();
    private void OnPlayerReleased(CharacterView character) { CancelChoices(); owner = null; }

    private void OnRunLevelChanged(int previous, int current)
    {
        progress.AddLevels(previous, current);
        refreshPending = true;
        // Dừng ngay, không đợi frame sau. Queue vẫn nhận đủ lượt nếu cùng lần nhặt lên nhiều cấp.
        if (pauseLease == null) pauseLease = pauses.Acquire();
    }

    private void LateUpdate()
    {
        if (open && (modalRoot == null || !modalRoot.activeInHierarchy)) { CancelChoices(); return; }
        if (!refreshPending) return;
        refreshPending = false;
        RefreshChoices();
    }

    private void RefreshChoices()
    {
        if (progress.Pending == 0 || owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive)
        { CancelChoices(); return; }
        pool.Build(availableSkills, skills, owner, offers);
        if (offers.Count == 0)
        {
            // Tất cả skill đã max: không khóa game ở một bảng không có nút hợp lệ.
            CancelChoices();
            return;
        }
        if (!open && EventSystem.current != null) previousSelection = EventSystem.current.currentSelectedGameObject;
        open = true;
        modalRoot.SetActive(true);
        modalRoot.transform.SetAsLastSibling();
        if (pauseLease == null) pauseLease = pauses.Acquire();
        if (heading != null) heading.text = progress.Pending > 1 ? $"SELECT A SKILL ({progress.Pending})" : "SELECT A SKILL";
        for (int i = 0; i < cards.Length; i++)
        {
            if (i < offers.Count) cards[i].Show(offers[i]);
            else cards[i].button.gameObject.SetActive(false);
        }
        for (int i = 0; i < offers.Count; i++)
        {
            // Keyboard/controller chỉ đi giữa ba lựa chọn, không lọt sang nút Pause phía dưới modal.
            cards[i].button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = cards[(i + offers.Count - 1) % offers.Count].button,
                selectOnRight = cards[(i + 1) % offers.Count].button
            };
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cards[0].button.gameObject);
        openedFrame = Time.frameCount;
        nextInputAt = Time.unscaledTime + 0.15f;
    }

    private void Choose(int index)
    {
        if (!open || selecting || refreshPending || Time.frameCount <= openedFrame || Time.unscaledTime < nextInputAt ||
            index < 0 || index >= offers.Count) return;
        selecting = true;
        try
        {
            foreach (Card card in cards) card.button.interactable = false;
            if (offers[index].TryApply(skills, owner)) progress.Consume();
            // Roll lại sau khi runtime thực sự đổi cấp. Không giữ các offer Lv.2 cũ cho lượt chọn tiếp.
            refreshPending = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CancelChoices();
        }
        finally { selecting = false; }
    }

    private void CancelChoices()
    {
        progress.Clear(); offers.Clear(); refreshPending = false; open = false;
        if (modalRoot != null) modalRoot.SetActive(false);
        IDisposable previous = pauseLease; pauseLease = null; previous?.Dispose();
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void OnDisable()
    {
        if (subscribed)
        {
            levels.RunLevelChanged -= OnRunLevelChanged;
            levels.RunReset -= CancelChoices;
            players.PlayerChanged -= OnPlayerChanged;
            subscribed = false;
        }
        if (owner != null) { owner.Died -= OnPlayerDied; owner.Released -= OnPlayerReleased; }
        owner = null;
        CancelChoices();
    }

    private void OnDestroy()
    {
        if (clicks == null || cards == null) return;
        for (int i = 0; i < cards.Length && i < clicks.Length; i++)
            if (cards[i]?.button != null && clicks[i] != null) cards[i].button.onClick.RemoveListener(clicks[i]);
    }
}
