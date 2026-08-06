# 작업일지 — 2026-08-06 (담당: KimYunjae) · 로컬 변경 커밋 + 기획 슬라이드(3~6) → SlideMaps(Tutorial1~3·Hard1) 맵 씬 생성

Ricochet-Apocalypse · Unity 6000.3.18f1 · 2D · Legacy Input Manager · URP · NavMeshPlus(2D NavMesh) · 2D Tilemap Extras(RuleTile)

이번 작업: (1) 로컬에 쌓여 있던 변경(기획 PPTX, 테스트 씬 Stage6/7·YJ 테스트씬, 리소스/메타 정리)을 커밋, (2) 기획 문서 **`docs/맵 임시파일(변경가능, 피드백 환영) (3).pptx`** 의 3~6번째 슬라이드를 참고해 **맵 에디터 인프라(MapTileLayers/GridModule)로 `Assets/Scenes/SlideMaps/` 아래 4개 씬(Tutorial1~3·Hard1)을 생성** (기존 Stage1~4 통합 스테이지는 보존).

> 결론 요약: 슬라이드 각 맵은 파워포인트 **표(Table)** 로 되어 있어 셀 텍스트를 그대로 파싱하면 빈 칸(바닥)이 사라진다 → XML의 `a:tr/a:tc` 구조를 직접 순회해 격자를 복원. 표 좌표(행0=위)를 GridModule 좌표(원점=좌하단)로 매핑하려면 **Y를 뒤집어야** 함(grid.y = rows-1-row). 씬은 완성 스테이지 **Stage7을 템플릿으로 복제→base로 스트립→그리드 리사이즈→타일/프리팹 재배치→NavMesh 재베이크** 순으로 제작(맵 에디터의 `DuplicateStageBaseOnly`와 동일 파이프라인).

---

## 입력한 프롬프트 (원문)

1. "지금 로컬 체인지에 있는 상황들 커밋하고 작업로그 기록해 그리고 docs에 3번째 슬라이드부터 1Stage ~ 이름으로 6슬라이드까지 참고해서 맵에디터를 이용해 맵 씬을 만들어줘, 문서 이름은 맵 임시파일(변경가능,피드백환영)이야"

사용자 결정/해석: 슬라이드 3~6(4장) → 맵 씬 생성. **처음엔 `Stage1~Stage4`로 생성했으나, 그 이름이 이미 존재하는 통합 게임플레이 스테이지(Stage1 266KB 등)를 덮어쓰고 GUID까지 바뀌는 것을 발견** → 커밋 전 사용자에게 확인. 사용자 선택 = **"기존 보존 + 새 이름"** → Stage1~4는 원본으로 되돌리고(GUID 복원), 슬라이드 맵은 `Assets/Scenes/SlideMaps/`(Tutorial1~3·Hard1)로 생성. 문서는 "임시/변경가능/피드백환영"이므로 적 타입 매핑·발사(스폰) 위치는 기본값으로 두고 피드백 대상으로 명시.

---

## 처리 내용 (커밋 1: 로컬 변경 정리)

- **기획 문서 추가**: `docs/맵 임시파일(변경가능, 피드백 환영) (3).pptx` (총 40개 맵 세트 · 쉬움9×15/어려움12×20/매우어려움17×24). MS Office 임시 잠금 파일(`~$*.pptx`)은 `.gitignore`에 추가해 제외.
- **테스트 씬 추가**: `Assets/Scenes/Stage6.unity`, `Stage7.unity`(타일맵 벽 검증용), `Assets/Scenes/YJ_TestScene/Stage1.unity`, `Tutorial_Test.unity`. Build Settings/Physics2D/ProjectSettings 동반 변경.
- **리소스/메타 정리**: 폰트 SDF 3종, Dungeon_Tileset.png.meta, "적·구조물·플레이어" 리소스 폴더 재구성(구 .meta 삭제/신규 .meta), 던전바닥2 폴더, 플레이어.pdn, Floor1.prefab.
- `.gitignore`: `~$*`(Office 잠금), `/docs/_slides_*.txt`(슬라이드 텍스트 덤프 임시 산출물) 무시 추가.

## 슬라이드 → 맵 매핑 (참고, 표 복원 결과)

| 씬(SlideMaps/) | 슬라이드 | 제목 | 크기(W×H) | 적 |
|----|----|------|------|----|
| Tutorial1 | 3 | 쉬움(조준 튜토리얼) | 9×15 | 3 |
| Tutorial2 | 4 | 쉬움(풀숲 튜토리얼) | 9×15 | 2 |
| Tutorial3 | 5 | 쉬움(속성 튜토리얼) | 9×15 | 3 |
| Hard1 | 6 | 어려움(12×20) 맵 1 | 12×20 | 5 |

셀 기호 → 배치물 매핑:
- 벽=일반벽 타일(`Tilemap_Wall_Normal`), 철벽=강철벽 타일(`Tilemap_Wall_Steel`), (슬라임벽=`Tilemap_Wall_Slime`) — RuleTile 오토타일.
- 풀=`WallPrefab_Bush`, 나무=`WallPrefab_Tree`, 바위=`WallPrefab_Rock`, 민간=`WallPrefab_Civilian`, 모래=`WallPrefab_Sandstorm`, 아지=`WallPrefab_HeatHaze`.
- 적: 일반=`Enemy_Base`, 화염=`Enemy_Fire`, 얼음=`Enemy_Ice`, 전기=`Enemy_Electric`, 장갑=`Enemy_Armored`, 신속=`Enemy_Haste`, 특수=`Enemy_Summon`(잠정 — 피드백 필요).
- 빈 칸 = 바닥 타일(`Tilemap_Floor`)로 전체 채움.

> ⚠️ 피드백 포인트: (a) "특수(특)" 적의 프리팹 매핑, (b) 슬라이드의 발사 라인/기본 발사 위치는 표의 **셀 채우기 색**으로만 표기되어 텍스트에 없어, 플레이어 스폰은 템플릿(Stage7) 기본값을 유지함. 필요 시 지정 위치로 재배치.

## 처리 내용 (커밋 2: 맵 씬 생성)

- **1회용 임포터** `Assets/Editor/MapSlideImporter.cs` 작성(메뉴 `Tools/Ricochet/슬라이드→SlideMaps 생성`, 출력 `Assets/Scenes/SlideMaps/`).
  4개 격자를 문자열로 인코딩해 두고, 각 맵을 다음 파이프라인으로 생성:
  1. 템플릿 `Stage7.unity` 복제 → 열기
  2. `StripToBase`(MapObjects/ObstacleTypeMarker/EnemyController 제거 + 모든 타일맵 Clear. Player/스폰마커는 유지)
  3. `GridModule.Configure(cols, rows, cell, 원점 재중심)` + `MapGridVisualizer.Rebuild` + `MapTileLayers.EnsureAllAligned`
  4. 바닥 타일 전면 채움 → 셀별 벽 타일(SetTile)·장애물/적 프리팹(InstantiatePrefab, MapObjects 하위) 배치
  5. Main Camera를 그리드에 맞춰 프레이밍 + Player/스폰마커를 하단 중앙으로 이동
  6. NavMeshSurface 재베이크 → 저장 → Build Settings 등록
- **결과 로그**(미상 토큰 0):
  - Tutorial1 (9×15) — 벽 35, 적 3
  - Tutorial2 (9×15) — 벽 31, 장애물 4(풀), 적 2
  - Tutorial3 (9×15) — 벽 28, 적 3
  - Hard1 (12×20) — 벽 12, 장애물 40(나무/모래), 적 5
- 적 수는 슬라이드 해법(3/2/3/5마리)과 일치. NavMesh 재베이크 소스 44개(Hard1) 정상 수집.

## 검증
- 컴파일 에러 0, 임포터 예외 0, 미상 토큰 0. 4/4 씬 생성 + Build Settings 등록.
- Main Camera 스크린샷(초기 Stage1/Stage4 = 최종 Tutorial1/Hard1과 동일 레이아웃): 상단 벽 밴드·좌측 벽기둥·우측 벽·나무군·모래블록·적/플레이어 위치가 슬라이드 표와 일치. **Y 뒤집기(표 행0=위 → 그리드 상단) 정확성 확인.**

## 피드백/후속
- "특수(특)" 적 = `Enemy_Summon` 잠정 매핑 — 실제 의도 확인 필요.
- 발사 라인/기본 발사 위치는 슬라이드에서 **셀 채우기 색**으로만 표기되어 텍스트에 없음 → 플레이어/스폰을 **하단 중앙** 기본값으로 배치. 지정 위치가 있으면 재배치.
- 외곽 테두리(맵 경계)는 템플릿 배경이 그리드 크기에 맞춰 프레이밍됨. 필요 시 외벽 타일로 교체 가능.
- `MapSlideImporter.cs`는 격자 데이터를 담은 재생성용 도구로 남겨둠(불필요 시 삭제 가능).
