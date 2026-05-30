using Game.Search.UI;
using UnityEngine;

namespace Game.UI
{
    public sealed class OverworldHUDRoot : MonoBehaviour
    {
        [SerializeField] private StatusHUD statusHUD;
        [SerializeField] private CurrencyHUD currencyHUD;
        [SerializeField] private BagButtonHUD bagButtonHUD;
        [SerializeField] private MissionTrackerHUD missionTrackerHUD;
        [SerializeField] private MapHUD mapHUD;
        [SerializeField] private DialogueLogHUD dialogueLogHUD;
        [SerializeField] private ItemAcquisitionHUD itemAcquisitionHUD;

        public StatusHUD StatusHUD => statusHUD;
        public CurrencyHUD CurrencyHUD => currencyHUD;
        public BagButtonHUD BagButtonHUD => bagButtonHUD;
        public MissionTrackerHUD MissionTrackerHUD => missionTrackerHUD;
        public MapHUD MapHUD => mapHUD;
        public DialogueLogHUD DialogueLogHUD => dialogueLogHUD;
        public ItemAcquisitionHUD ItemAcquisitionHUD => itemAcquisitionHUD;

        private void Awake()
        {
            ResolveReferences();
        }

        public void ShowStaticHUD()
        {
            statusHUD?.Show();
            currencyHUD?.Show();
            bagButtonHUD?.Show();
            missionTrackerHUD?.Show();
            mapHUD?.Show();
        }

        public void HideStaticHUD()
        {
            statusHUD?.Hide();
            currencyHUD?.Hide();
            bagButtonHUD?.Hide();
            missionTrackerHUD?.Hide();
            mapHUD?.Hide();
        }

        private void ResolveReferences()
        {
            statusHUD ??= GetComponentInChildren<StatusHUD>(true);
            currencyHUD ??= GetComponentInChildren<CurrencyHUD>(true);
            bagButtonHUD ??= GetComponentInChildren<BagButtonHUD>(true);
            missionTrackerHUD ??= GetComponentInChildren<MissionTrackerHUD>(true);
            mapHUD ??= GetComponentInChildren<MapHUD>(true);
            dialogueLogHUD ??= GetComponentInChildren<DialogueLogHUD>(true);
            itemAcquisitionHUD ??= GetComponentInChildren<ItemAcquisitionHUD>(true);
        }
    }
}
