using Game.NonCombat.Save;
using UnityEngine;

namespace Game.NonCombat.Progress
{
    /// <summary>Unity lifecycle and save bridge for the pure CharacterSkillRuntime domain model.</summary>
    public sealed class CharacterSkillSaveParticipant : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer, INewGameRuntimeReset
    {
        public static CharacterSkillSaveParticipant Instance { get; private set; }

        public CharacterSkillRuntime Runtime { get; } = new CharacterSkillRuntime();
        private CharacterStartDefinitionSO _startDefinition;
        private SaveReadProvenance _equipmentReadProvenance;

        internal EquipmentReadOrigin GetEquipmentReadOrigin(string characterId) =>
            _equipmentReadProvenance?.ForCharacter(characterId) ?? EquipmentReadOrigin.Unknown;
        internal int? EquipmentSourceSchemaVersion => _equipmentReadProvenance?.SourceSchemaVersion;
        internal bool HadDiscardedEquipment(string characterId) => _equipmentReadProvenance?.HadDiscardedEquipment(characterId) == true;
        internal void InvalidateEquipmentReadProvenance() => _equipmentReadProvenance = null;

        private void ObserveSkillChanges()
        {
            Runtime.Changed -= InvalidateEquipmentReadProvenance;
            Runtime.Changed += InvalidateEquipmentReadProvenance;
        }

        internal bool CanConfigureStartDefinition(CharacterStartDefinitionSO start, out string message)
        {
            if (start == null) { message = "Character start definition is missing."; return false; }
            if (!start.TryValidate(out message)) return false;
            if (_startDefinition != null && _startDefinition != start)
            { message = "A different Character start definition already owns initial skill configuration."; return false; }
            message = null;
            return true;
        }

        internal void ConfigureStartDefinition(CharacterStartDefinitionSO start)
        {
            // Configuration never grants or equips: Load and repeated scene bootstrap preserve saved state.
            _startDefinition = start;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ObserveSkillChanges();
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            Runtime.Changed -= InvalidateEquipmentReadProvenance;
            if (Instance == this)
                Instance = null;
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.characterSkills ??= new CharacterSkillCollectionSaveData();
            Runtime.Capture(saveData.characterSkills);
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            ObserveSkillChanges();
            InvalidateEquipmentReadProvenance();
            Runtime.Restore(saveData?.characterSkills);
            if (saveData?.ReadProvenance?.Matches(saveData) == true)
                _equipmentReadProvenance = saveData.ReadProvenance;
        }

        public void ResetForNewGame()
        {
            InvalidateEquipmentReadProvenance();
            Runtime.Reset();
            if (_startDefinition == null) return;
            foreach (CharacterStartDefinitionSO.InitialSkillGrant grant in _startDefinition.InitialSkillGrants)
                foreach (var skill in grant.Skills)
                {
                    Runtime.TryAcquire(grant.CharacterId, skill.PersistentKey);
                    Runtime.TryEquip(grant.CharacterId, skill.PersistentKey);
                }
        }
    }
}
