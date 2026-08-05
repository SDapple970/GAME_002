using System;
using System.Collections.Generic;
using Game.NonCombat.Progress;
using Game.NonCombat.Save;
using UnityEngine;

namespace Game.NonCombat.Party
{
    public sealed class PartyRuntime : MonoBehaviour, ISaveDataProvider, ISaveDataConsumer
    {
        public static PartyRuntime Instance { get; private set; }
        private readonly List<string> _members = new();
        private readonly List<string> _combatLineup = new();
        private string _leaderCharacterId;

        public IReadOnlyList<string> Members => new List<string>(_members).AsReadOnly();
        public IReadOnlyList<string> CombatLineup => new List<string>(_combatLineup).AsReadOnly();
        public string LeaderCharacterId => _leaderCharacterId;
        public event Action<PartyMutationResult> Changed;
        public event Action Refreshed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool Contains(string characterId) => _members.Contains(CharacterIdentity.Normalize(characterId));

        public PartyMutationResult AddMember(string characterId)
        {
            string id = CharacterIdentity.Normalize(characterId);
            if (id == null) return Result(id, PartyMutationStatus.InvalidCharacterId);
            if (_members.Contains(id)) return Result(id, PartyMutationStatus.AlreadyMember);
            _members.Add(id);
            if (_leaderCharacterId == null) _leaderCharacterId = id;
            return Publish(id);
        }

        public PartyMutationResult RemoveMember(string characterId)
        {
            string id = CharacterIdentity.Normalize(characterId);
            if (id == null) return Result(id, PartyMutationStatus.InvalidCharacterId);
            if (!_members.Remove(id)) return Result(id, PartyMutationStatus.NotOwned);
            _combatLineup.Remove(id);
            if (_leaderCharacterId == id) _leaderCharacterId = _members.Count > 0 ? _members[0] : null;
            return Publish(id);
        }

        public PartyMutationResult SetLeader(string characterId)
        {
            string id = CharacterIdentity.Normalize(characterId);
            if (id == null || !_members.Contains(id)) return Result(id, PartyMutationStatus.InvalidLeader);
            if (_leaderCharacterId == id) return Result(id, PartyMutationStatus.NoChange);
            _leaderCharacterId = id;
            return Publish(id);
        }

        public PartyMutationResult SelectForCombat(string characterId, bool selected)
        {
            string id = CharacterIdentity.Normalize(characterId);
            if (id == null) return Result(id, PartyMutationStatus.InvalidCharacterId);
            if (!_members.Contains(id)) return Result(id, PartyMutationStatus.NotOwned);
            bool changed = selected ? AddUnique(_combatLineup, id) : _combatLineup.Remove(id);
            return changed ? Publish(id) : Result(id, PartyMutationStatus.NoChange);
        }

        public void CaptureSaveData(GameSaveData saveData)
        {
            if (saveData == null) return;
            saveData.party ??= new PartySaveData();
            saveData.party.memberIds.Clear(); saveData.party.memberIds.AddRange(_members);
            saveData.party.leaderCharacterId = _leaderCharacterId;
            saveData.party.selectedCombatMemberIds.Clear(); saveData.party.selectedCombatMemberIds.AddRange(_combatLineup);
        }

        public void RestoreSaveData(GameSaveData saveData)
        {
            _members.Clear(); _combatLineup.Clear(); _leaderCharacterId = null;
            if (saveData?.party?.memberIds != null)
                foreach (string value in saveData.party.memberIds) AddUnique(_members, CharacterIdentity.Normalize(value));
            string leader = CharacterIdentity.Normalize(saveData?.party?.leaderCharacterId);
            _leaderCharacterId = leader != null && _members.Contains(leader) ? leader : _members.Count > 0 ? _members[0] : null;
            if (saveData?.party?.selectedCombatMemberIds != null)
                foreach (string value in saveData.party.selectedCombatMemberIds)
                {
                    string id = CharacterIdentity.Normalize(value);
                    if (id != null && _members.Contains(id)) AddUnique(_combatLineup, id);
                }
            Refreshed?.Invoke();
        }

        private PartyMutationResult Publish(string id) { PartyMutationResult result = Result(id, PartyMutationStatus.Success); Changed?.Invoke(result); return result; }
        private static PartyMutationResult Result(string id, PartyMutationStatus status) => new(id, status);
        private static bool AddUnique(List<string> list, string id) { if (id == null || list.Contains(id)) return false; list.Add(id); return true; }
    }
}
