using System.Collections.Generic;
using UnityEngine;

namespace Game.Search.Data
{
    public enum SearchObjectCategory
    {
        Box,
        Journal,
        Life,
        Collection,
        Apartment,
        Custom
    }

    [CreateAssetMenu(menuName = "GAME/Search/Searchable Object")]
    public sealed class SearchableObjectDefinitionSO : ScriptableObject
    {
        [SerializeField] private string objectId;
        [SerializeField] private string displayName;
        [SerializeField] private string[] keywords;
        [SerializeField] private SearchObjectCategory category;
        [SerializeField] private string promptText = "E: 조사";
        [SerializeField] private bool searchOnce = true;
        [SerializeField] private bool showQuestionOnEnter = true;
        [SerializeField] private string questionMessage = "조사해볼까?";
        [SerializeField] private bool requireConfirmation = true;
        [SerializeField] private string confirmationMessage = "어떻게 할까?";
        [SerializeField] private string confirmChoiceText = "조사한다";
        [SerializeField] private string cancelChoiceText = "그만둔다";
        [SerializeField] private float resultMessageSeconds = 2.5f;
        [SerializeField] private List<SearchOutcome> outcomes = new();

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public string PromptText => promptText;
        public bool SearchOnce => searchOnce;
        public bool ShowQuestionOnEnter => showQuestionOnEnter;
        public string QuestionMessage => questionMessage;
        public bool RequireConfirmation => requireConfirmation;
        public string ConfirmationMessage => confirmationMessage;
        public string ConfirmChoiceText => confirmChoiceText;
        public string CancelChoiceText => cancelChoiceText;
        public float ResultMessageSeconds => resultMessageSeconds;
        public IReadOnlyList<SearchOutcome> Outcomes => outcomes;
    }
}
