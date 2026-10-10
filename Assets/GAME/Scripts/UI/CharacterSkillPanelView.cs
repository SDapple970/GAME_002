using System;
using System.Collections.Generic;
using Game.NonCombat.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Serialized prefab view. Only the root router controls its visibility.</summary>
    public sealed class CharacterSkillPanelView : MonoBehaviour, ICharacterSkillView
    {
        [SerializeField] private TMP_Text selectedCharacterLabel;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private TMP_Text characterEmptyLabel;
        [SerializeField] private TMP_Text filmEmptyLabel;
        [SerializeField] private TMP_Text equippedEmptyLabel;
        [SerializeField] private TMP_Text uniqueEmptyLabel;
        [SerializeField] private TMP_Text nextUnlockLabel;
        [SerializeField] private RectTransform characterContent;
        [SerializeField] private RectTransform filmContent;
        [SerializeField] private RectTransform equippedContent;
        [SerializeField] private RectTransform uniqueContent;
        [SerializeField] private CharacterSkillRowView rowPrefab;
        [SerializeField] private Button characterButtonPrefab;
        [SerializeField] private Button closeButton;

        private int _generation;
        public event Action<string> CharacterSelected;
        public event Action<string> EquipRequested;
        public event Action<string> UnequipRequested;
        public event Action Shown;
        public event Action Hidden;
        public event Action CloseRequested;
        public Button CloseButton => closeButton;
        internal CharacterSkillViewModel LastModel { get; private set; }

        private void OnEnable()
        {
            feedbackLabel.text = "캐릭터를 선택하고 필름을 관리하세요.";
            Shown?.Invoke();
        }

        private void OnDisable()
        {
            _generation++;
            Hidden?.Invoke();
        }

        public void RequestClose() => CloseRequested?.Invoke();

        public void ShowUnavailable()
        {
            LastModel = null;
            _generation++;
            Clear(characterContent); Clear(filmContent); Clear(equippedContent); Clear(uniqueContent);
            characterEmptyLabel.gameObject.SetActive(true);
            filmEmptyLabel.gameObject.SetActive(true);
            equippedEmptyLabel.gameObject.SetActive(true);
            uniqueEmptyLabel.gameObject.SetActive(true);
            selectedCharacterLabel.text = "선택한 캐릭터 없음";
            nextUnlockLabel.text = "다음 해금 정보 없음";
            feedbackLabel.text = "스킬 정보를 불러올 수 없습니다. 화면을 닫고 다시 시도해 주세요.";
        }

        public void Render(CharacterSkillViewModel model)
        {
            LastModel = model;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool restoreFocus = selected != null && (selected.transform.IsChildOf(characterContent) ||
                selected.transform.IsChildOf(filmContent) || selected.transform.IsChildOf(equippedContent));
            int generation = ++_generation;
            Clear(characterContent); Clear(filmContent); Clear(equippedContent); Clear(uniqueContent);
            selectedCharacterLabel.text = model.SelectedCharacterId == null ? "선택한 캐릭터 없음" : $"선택: {model.SelectedCharacterId}";
            selectedCharacterLabel.overflowMode = TextOverflowModes.Ellipsis;
            selectedCharacterLabel.maxVisibleLines = 1;
            characterEmptyLabel.gameObject.SetActive(model.CharacterIds.Count == 0);
            filmEmptyLabel.gameObject.SetActive(model.SharedFilms.Count == 0);
            equippedEmptyLabel.gameObject.SetActive(model.EquippedFilmKeys.Count == 0);
            uniqueEmptyLabel.gameObject.SetActive(model.UniqueSkills.Count == 0);
            nextUnlockLabel.text = model.NextUniqueUnlockLevel.HasValue ? $"다음 해금: 레벨 {model.NextUniqueUnlockLevel}" : "다음 해금 정보 없음";

            foreach (string id in model.CharacterIds)
            {
                Button button = Instantiate(characterButtonPrefab, characterContent);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.text = id == model.SelectedCharacterId ? $"● {id}" : id;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.maxVisibleLines = 1;
                button.interactable = model.CanEdit;
                button.onClick.AddListener(() =>
                {
                    if (!IsCurrent(generation)) return;
                    CharacterSelected?.Invoke(id);
                    for (int i = 0; i < LastModel.CharacterIds.Count; i++)
                        if (LastModel.CharacterIds[i] == id) Focus(characterContent.GetChild(i).GetComponent<Button>());
                });
            }
            Dictionary<string, FilmSkillViewItem> films = new(StringComparer.Ordinal);
            foreach (FilmSkillViewItem film in model.SharedFilms)
            {
                films[film.PersistentSkillKey] = film;
                string key = film.PersistentSkillKey;
                Instantiate(rowPrefab, filmContent).Bind(film.DisplayName, film.IsEquipped ? "장착 중" : "보유", "장착",
                    model.CanEdit && !film.IsEquipped, () => RequestChange(key, true, generation));
            }
            foreach (string key in model.EquippedFilmKeys)
            {
                string name = films.TryGetValue(key, out FilmSkillViewItem film) ? film.DisplayName : "이름 없는 필름";
                Instantiate(rowPrefab, equippedContent).Bind(name, "장착 중", "해제", model.CanEdit,
                    () => RequestChange(key, false, generation));
            }
            foreach (UniqueSkillAvailability unique in model.UniqueSkills)
            {
                string status = unique.Status switch
                {
                    UniqueSkillAvailabilityStatus.Unlocked => $"해금됨 · Lv.{unique.RequiredLevel}",
                    UniqueSkillAvailabilityStatus.Locked => $"잠금 · Lv.{unique.RequiredLevel}",
                    UniqueSkillAvailabilityStatus.Unconfigured => "조건 미정",
                    UniqueSkillAvailabilityStatus.Disabled => "비활성",
                    _ => "설정 확인 필요"
                };
                Instantiate(rowPrefab, uniqueContent).Bind(unique.DisplayName, status, "", false, null);
            }
            if (model.CharacterIds.Count == 0) feedbackLabel.text = "보유한 캐릭터가 없습니다.";
            if (restoreFocus) Focus(closeButton);
        }

        public void ShowEquipResult(CharacterSkillEquipResult result)
        {
            feedbackLabel.text = result.Status switch
            {
                CharacterSkillEquipStatus.Equipped => "필름을 장착했습니다.",
                CharacterSkillEquipStatus.Unequipped => "필름을 해제했습니다.",
                CharacterSkillEquipStatus.AlreadyEquipped => "이미 장착한 필름입니다.",
                CharacterSkillEquipStatus.NotEquipped => "장착하지 않은 필름입니다.",
                CharacterSkillEquipStatus.NotAcquired => "아직 획득하지 않은 필름입니다.",
                CharacterSkillEquipStatus.EquipNotAllowed => "현재는 스킬을 변경할 수 없습니다.",
                CharacterSkillEquipStatus.NotPartyMember or CharacterSkillEquipStatus.InvalidCharacterId => "캐릭터를 다시 선택해 주세요.",
                CharacterSkillEquipStatus.NotFilm => "필름만 장착할 수 있습니다.",
                _ => "스킬 정보를 확인할 수 없습니다. 다시 시도해 주세요."
            };
        }

        private bool IsCurrent(int generation) => isActiveAndEnabled && generation == _generation;

        private void RequestChange(string key, bool equip, int generation)
        {
            if (!IsCurrent(generation)) return;
            if (equip) EquipRequested?.Invoke(key); else UnequipRequested?.Invoke(key);
            if (!isActiveAndEnabled || LastModel == null) return;
            if (equip)
            {
                for (int i = 0; i < LastModel.EquippedFilmKeys.Count; i++)
                    if (LastModel.EquippedFilmKeys[i] == key) Focus(equippedContent.GetChild(i).GetComponentInChildren<Button>());
            }
            else
            {
                for (int i = 0; i < LastModel.SharedFilms.Count; i++)
                    if (LastModel.SharedFilms[i].PersistentSkillKey == key) Focus(filmContent.GetChild(i).GetComponentInChildren<Button>());
            }
        }

        private static void Focus(Button button)
        {
            if (EventSystem.current == null || button == null || !button.isActiveAndEnabled || !button.interactable) return;
            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll != null && scroll.viewport != null && scroll.content != null)
            {
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, button.transform);
                Rect viewport = scroll.viewport.rect;
                Vector2 shift = Vector2.zero;
                if (scroll.vertical)
                    shift.y = bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y :
                        bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : 0;
                if (scroll.horizontal)
                    shift.x = bounds.min.x < viewport.xMin ? viewport.xMin - bounds.min.x :
                        bounds.max.x > viewport.xMax ? viewport.xMax - bounds.max.x : 0;
                scroll.StopMovement();
                scroll.content.anchoredPosition += shift;
            }
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static void Clear(Transform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }
    }
}
