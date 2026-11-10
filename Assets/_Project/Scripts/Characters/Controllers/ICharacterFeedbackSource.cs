using System;

public enum CharacterFeedbackKind { Hit, Poison, Burn, Slow }

// Controller phát trạng thái; bên nhận quyết định màu và cách trình diễn.
public interface ICharacterFeedbackSource
{
    event Action<CharacterFeedbackKind, bool> FeedbackChanged;
    void NotifyEffect(CharacterView view, CharacterRuntimeData runtime, ulong generation,
        CharacterFeedbackKind kind, bool active);
}
