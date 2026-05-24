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
        [SerializeField] private List<SearchOutcome> outcomes = new();

        public string ObjectId => objectId;
        public string DisplayName => displayName;
        public string PromptText => promptText;
        public bool SearchOnce => searchOnce;
        public IReadOnlyList<SearchOutcome> Outcomes => outcomes;
    }
}
