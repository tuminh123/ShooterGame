using System;

// Visual chỉ đọc snapshot và nghe sự kiện; không phụ thuộc input, AI hoặc logic skill.
public interface ICharacterVisualEventSource
{
    event Action<CharacterVisualState> VisualStateRequested;
    event Action<bool> FacingChanged;
    CharacterVisualState MovementVisualState { get; }
    bool FacingRight { get; }
    bool IsVisualAlive { get; }
}
