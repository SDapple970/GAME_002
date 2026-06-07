using UnityEngine;

namespace Game.Office
{
    public enum OfficeHotspotType
    {
        CharacterDialogue,
        CaseBookshelf,
        Custom
    }

    [RequireComponent(typeof(Collider2D))]
    public sealed class OfficeHotspot2D : MonoBehaviour
    {
        [SerializeField] private string hotspotId;
        [SerializeField] private string hoverText;
        [SerializeField] private OfficeHotspotType type = OfficeHotspotType.Custom;

        public string HotspotId => hotspotId;
        public string HoverText => hoverText;
        public OfficeHotspotType Type => type;
    }
}
