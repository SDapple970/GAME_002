# GAME_002 Item / Reward 기획 템플릿

> 최신 구현 대조: 2026-10-11 · Unity 6000.3.23f1 · 저장 schema 12. 기존 XML/완료 보고서를 확인했으며 이번 문서 갱신에서 Unity 테스트를 재실행하지 않았다. 정식 콘텐츠·Scene/Player 검증은 별도로 기록한다.

## Item

| Item 임시 ID | 이름 | 설명 | 아이콘 | 획득처 | stack 제한 | 사용 효과 | 사용 조건 | 기존 시스템 지원 | 필요 에셋 | 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | 무제한/수량 | 추가 개발 필요 여부 | | 지원/일부/미지원 | | 기획/승인/Unity/테스트 |

## Reward

| Reward 임시 ID | 지급 조건/source | item/수량 | Gold | EXP | 중복 지급 정책 | UI 표시 | Quest/Story/Combat 관계 | Save 영향 | 상태 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | existing stable ledger / 요구사항 | | | | |

| 추가 기능 요구 | 이유 | Inventory/Reward/Save/UI 영향 | 담당/승인 |
| --- | --- | --- | --- |
| 판매, item use, rarity 등 | | | |

## 전투 사용 추가 입력표 (2026-10-11)
| inventory itemId | targetRule | effectKind | hpRecovery / appliedStatus | CombatItemCatalog | Adapter 연결 | 정식/테스트 데이터 | 검수 증거 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| | Self / SingleAlly | RestoreHp / ApplyStatus | | | | | |
지원 범위 밖의 필드 사용·장비·상점 효과는 기존 추가 개발 표에 기록한다. EXP reward가 Combat 다인 지급이면 진입 참가자 snapshot과 단일 Completion 계약을 사용한다.
