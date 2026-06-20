using Game.Story;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Office
{
    public sealed class OfficeMenuController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private Camera targetCamera;

        [Header("Hover UI")]
        [SerializeField] private Text hoverLabelText;
        [SerializeField] private TMP_Text hoverLabelTmpText;
        [SerializeField] private GameObject hoverLabelRoot;

        [Header("Character Dialogue UI")]
        [SerializeField] private Text characterDialogueText;
        [SerializeField] private TMP_Text characterDialogueTmpText;
        [SerializeField] private GameObject characterDialogueRoot;

        [Header("Case File")]
        [SerializeField] private CaseFileDocumentPanel caseFilePanel;
        [SerializeField] private CaseFileDataSO caseFile;

        [Header("Character Lines")]
        [SerializeField] private string[] characterLines;
        [SerializeField] private bool randomizeCharacterLines;

        private OfficeHotspot2D _hoveredHotspot;
        private int _nextCharacterLineIndex;

        private void Awake()
        {
            AutoBindReferences();
            SetHoverVisible(false);
            SetCharacterDialogueVisible(false);
        }

        private void Update()
        {
            AutoBindReferences();
            UpdateHoveredHotspot();

            if (UnityEngine.Input.GetMouseButtonDown(0) && !IsPointerOverUI())
                HandleClick(_hoveredHotspot);
        }

        private void AutoBindReferences()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
        }

        private void UpdateHoveredHotspot()
        {
            OfficeHotspot2D hotspot = IsPointerOverUI() ? null : RaycastHotspot();
            if (_hoveredHotspot == hotspot)
                return;

            _hoveredHotspot = hotspot;
            if (_hoveredHotspot == null)
            {
                SetHoverVisible(false);
                return;
            }

            SetText(hoverLabelText, hoverLabelTmpText, _hoveredHotspot.HoverText);
            SetHoverVisible(!string.IsNullOrEmpty(_hoveredHotspot.HoverText));
        }

        private OfficeHotspot2D RaycastHotspot()
        {
            if (targetCamera == null)
                return null;

            Ray ray = targetCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray);
            if (hit.collider == null)
                return null;

            return hit.collider.GetComponentInParent<OfficeHotspot2D>();
        }

        private void HandleClick(OfficeHotspot2D hotspot)
        {
            if (hotspot == null)
                return;

            switch (hotspot.Type)
            {
                case OfficeHotspotType.CharacterDialogue:
                    ShowCharacterDialogue();
                    break;
                case OfficeHotspotType.CaseBookshelf:
                    if (caseFilePanel != null)
                        caseFilePanel.Open(caseFile);
                    else
                        Debug.LogWarning("[OfficeMenuController] CaseFileDocumentPanel is not assigned.", this);
                    break;
                case OfficeHotspotType.Custom:
                    Debug.Log($"[OfficeMenuController] Custom hotspot clicked: {hotspot.HotspotId}", hotspot);
                    break;
            }
        }

        private void ShowCharacterDialogue()
        {
            if (characterLines == null || characterLines.Length == 0)
            {
                SetCharacterDialogueVisible(false);
                return;
            }

            string line;
            if (randomizeCharacterLines)
            {
                line = characterLines[Random.Range(0, characterLines.Length)];
            }
            else
            {
                line = characterLines[_nextCharacterLineIndex % characterLines.Length];
                _nextCharacterLineIndex++;
            }

            SetText(characterDialogueText, characterDialogueTmpText, line);
            SetCharacterDialogueVisible(!string.IsNullOrEmpty(line));
        }

        private void SetHoverVisible(bool visible)
        {
            if (hoverLabelRoot != null)
                hoverLabelRoot.SetActive(visible);
        }

        private void SetCharacterDialogueVisible(bool visible)
        {
            if (characterDialogueRoot != null)
                characterDialogueRoot.SetActive(visible);
        }

        private static void SetText(Text text, TMP_Text tmpText, string value)
        {
            if (text != null)
                text.text = value;

            if (tmpText != null)
                tmpText.text = value;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
