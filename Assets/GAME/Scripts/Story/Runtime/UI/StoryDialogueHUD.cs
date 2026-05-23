// Assets/GAME/Scripts/Story/Runtime/UI/StoryDialogueHUD.cs
using System;
using System.Collections.Generic;
using Game.Story.Data;
using UnityEngine;

namespace Game.Story.UI
{
    public sealed class StoryDialogueHUD : MonoBehaviour
    {
        [SerializeField] private WorldDialogueBubble worldBubble;
        [SerializeField] private TimedChoicePanel timedChoicePanel;

        private void Awake()
        {
            HideAll();
        }

        public void ShowLine(StorySpeakerAnchor speakerAnchor, StoryNode node)
        {
            if (worldBubble != null)
            {
                worldBubble.Show(speakerAnchor, node != null ? node.Body : string.Empty);
            }
        }

        public void ShowTimedChoices(StoryNode node, IReadOnlyList<StoryChoice> choices, Action<StoryChoice> onChoiceSelected, Action onTimeout)
        {
            if (node != null && node.HideBubbleAfterChoice)
            {
                worldBubble?.Hide();
            }

            float timeLimit = node != null && node.UseTimedChoices ? node.ChoiceTimeLimitSeconds : 0f;
            timedChoicePanel?.ShowChoices(choices, timeLimit, onChoiceSelected, onTimeout);
        }

        public void HideChoices()
        {
            timedChoicePanel?.Hide();
        }

        public void HideAll()
        {
            worldBubble?.Hide();
            timedChoicePanel?.Hide();
        }
    }
}
