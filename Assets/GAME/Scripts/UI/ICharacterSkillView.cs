using System;
using Game.NonCombat.Progress;

namespace Game.UI
{
    /// <summary>Reusable content view. Hosting/visibility remains owned by UIScreenRouter and GameUIRootController.</summary>
    public interface ICharacterSkillView
    {
        event Action<string> CharacterSelected;
        event Action<string> EquipRequested;
        event Action<string> UnequipRequested;
        // CanShow gates this content in Dialogue/Choice/Reward/etc; the host retains global root routing.
        void Render(CharacterSkillViewModel model);
        void ShowEquipResult(CharacterSkillEquipResult result);
    }
}
