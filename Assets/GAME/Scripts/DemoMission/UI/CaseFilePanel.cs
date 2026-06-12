using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Game.DemoMission.Data;

namespace Game.DemoMission.UI
{
    public sealed class CaseFilePanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private DemoMissionDefinitionSO missionDefinition;
        [SerializeField] private TMP_Text missionTitleText;
        [SerializeField] private Image rescueNpcPortrait;
        [SerializeField] private TMP_Text rescueNpcNameText;
        [SerializeField] private TMP_Text rescueNpcDescriptionText;
        [FormerlySerializedAs("rescueNpcLocationText")]
        [SerializeField] private TMP_Text lastKnownLocationText;
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private Transform monsterListRoot;
        [FormerlySerializedAs("monsterRowPrefab")]
        [SerializeField] private GameObject monsterListItemPrefab;

        private DemoMissionDefinitionSO _activeMission;

        public DemoMissionDefinitionSO ActiveMission => _activeMission != null ? _activeMission : missionDefinition;

        private void Awake()
        {
            if (root == null)
                root = gameObject;
        }

        public void OpenConfiguredMission()
        {
            Open(missionDefinition);
        }

        public void Open(DemoMissionDefinitionSO mission)
        {
            _activeMission = mission != null ? mission : missionDefinition;

            if (root != null)
                root.SetActive(true);

            Refresh();
        }

        public void Close()
        {
            if (root != null)
                root.SetActive(false);
        }

        public void Refresh()
        {
            DemoMissionDefinitionSO mission = ActiveMission;
            if (mission == null)
            {
                Debug.LogWarning("[CaseFilePanel] Mission definition is not assigned.", this);
                ClearMonsterRows();
                return;
            }

            RefreshRescueTarget(mission);
            RefreshObjective(mission);
            RefreshMonsterList(mission);

            if (missionTitleText != null)
                missionTitleText.text = mission.missionTitle;
        }

        private void RefreshRescueTarget(DemoMissionDefinitionSO mission)
        {
            RescueNpcDefinitionSO npc = mission.rescueTarget;

            if (rescueNpcNameText != null)
                rescueNpcNameText.text = npc != null ? npc.displayName : "Unknown";

            if (rescueNpcDescriptionText != null)
                rescueNpcDescriptionText.text = npc != null ? npc.briefDescription : string.Empty;

            if (lastKnownLocationText != null)
                lastKnownLocationText.text = npc != null ? npc.lastKnownLocation : string.Empty;

            if (rescueNpcPortrait != null)
            {
                rescueNpcPortrait.sprite = npc != null ? npc.portrait : null;
                rescueNpcPortrait.enabled = rescueNpcPortrait.sprite != null;
            }
        }

        private void RefreshObjective(DemoMissionDefinitionSO mission)
        {
            if (objectiveText == null)
                return;

            string objective = mission.objectiveDescription;
            if (string.IsNullOrWhiteSpace(objective))
            {
                string targetName = mission.rescueTarget != null ? mission.rescueTarget.displayName : "the rescue target";
                objective = $"Defeat {mission.requiredEnemyKills} enemy and rescue {targetName}.";
            }

            objectiveText.text = objective;
        }

        private void RefreshMonsterList(DemoMissionDefinitionSO mission)
        {
            ClearMonsterRows();

            if (monsterListRoot == null)
            {
                Debug.LogWarning("[CaseFilePanel] Monster list root is not assigned.", this);
                return;
            }

            if (mission.monsters == null || mission.monsters.Count == 0)
            {
                CreateFallbackMonsterRow("No monster briefing available.", string.Empty, null);
                return;
            }

            for (int i = 0; i < mission.monsters.Count; i++)
            {
                MonsterBriefingEntry monster = mission.monsters[i];
                if (monster == null)
                    continue;

                if (monsterListItemPrefab != null)
                    PopulateMonsterRow(Instantiate(monsterListItemPrefab, monsterListRoot), monster);
                else
                    CreateFallbackMonsterRow(monster.displayName, monster.description, monster.portrait);
            }
        }

        private void PopulateMonsterRow(GameObject row, MonsterBriefingEntry monster)
        {
            if (row == null || monster == null)
                return;

            TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
                texts[0].text = monster.displayName;
            if (texts.Length > 1)
                texts[1].text = monster.description;

            Image image = row.GetComponentInChildren<Image>(true);
            if (image != null)
            {
                image.sprite = monster.portrait;
                image.enabled = image.sprite != null;
            }
        }

        private void CreateFallbackMonsterRow(string displayName, string description, Sprite portrait)
        {
            GameObject row = new GameObject("MonsterBriefingRow", typeof(RectTransform));
            row.transform.SetParent(monsterListRoot, false);

            TMP_Text text = row.AddComponent<TextMeshProUGUI>();
            text.text = string.IsNullOrWhiteSpace(description)
                ? displayName
                : $"{displayName}\n{description}";
            text.fontSize = 20f;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private void ClearMonsterRows()
        {
            if (monsterListRoot == null)
                return;

            for (int i = monsterListRoot.childCount - 1; i >= 0; i--)
                Destroy(monsterListRoot.GetChild(i).gameObject);
        }
    }
}
