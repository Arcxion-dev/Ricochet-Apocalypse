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

## 추가 (2026-08-06) — 테스트 가능화 + 바깥 튕기는 외벽 필수
1. **[시작] 버튼 노출**(프롬프트: "시작버튼이 없어서 테스트할 수가 없어"): `StageReadyUI.Evaluate`가 이름 규칙(Stage{n}) 밖 맵도 **GridModule 존재 시** 준비 오버레이를 띄우도록 수정 → SlideMaps에서 ▶Play → [시작]으로 조준/격발 테스트 가능. 진행/세이브(IsStageScene) 로직은 불변. 적 "특수(특)"=`Enemy_Summon` 사용자 확정.
2. **바깥 튕기는 외벽 필수**(프롬프트: "맵 바깥에는 튕기는 외벽이 무조건 존재해야해"): `MapSlideImporter`가 각 맵 **테두리 전체를 강철벽(ArmoredWall)** 으로 채움. 강철벽만 **모든 탄종 무조건 튕김**(일반벽=철갑탄 관통, 슬라임=Wall타입이라 철갑탄 관통 → "무조건"엔 부적합)이라 공이 맵 밖으로 못 나감. 재생성: 외벽 강철 = Tutorial 9×15→52칸, Hard1 12×20→60칸, NavMesh Steel 콜라이더 정상 수집. 스크린샷으로 4면 폐곡선 확인. ※ 외벽 타입은 슬라임(탄력) 등으로 한 줄 교체 가능.

## 추가 (2026-08-06) — 슬라이드 7~16 맵 10개 추가
- 프롬프트: "저번에 작업했던 슬라이드부터 그 이후 슬라이드 맵 10개 더 만들어줘" → 슬라이드 3~6(기존) 다음인 **7~16**을 `Assets/Scenes/SlideMaps/Slide07~Slide16`으로 생성(전부 12×20, 강철 외벽 포함).
- 임포터 개선: `Build(bool onlyMissing)`로 분리, 메뉴 2종(**전체** / **없는 것만**) 추가. 이번엔 `BuildMissing()`로 **기존 Tutorial1~3·Hard1은 재생성하지 않고 보존**(GUID 유지), 새 10개만 생성("완료: 10 생성, 4 건너뜀").
- ⚠️ 표 충실 반영 원칙: 일부 슬라이드(예: 7)는 표엔 `일`(일반) 마커만 있는데 해법 텍스트엔 다른 적 타입/추가 적이 적혀 있어 **표 불일치**가 있음. 배치는 **표(테이블)를 기준**으로 그대로 옮김(해법과 적 수가 다를 수 있음). 문서가 "임시/변경가능"이라 피드백으로 조정 가능.
- 슬라이드→씬: 7→Slide07(어려움맵2) … 16→Slide16(중간2). 원 문서의 내부 라벨(중간3/4, 어려움1 등)이 슬라이드 순서와 어긋나 **혼동 방지를 위해 슬라이드 번호로 명명**. Slide15(민간인 다수) 스크린샷으로 배치·외벽 검증.
- Build Settings에 10개 씬 등록. 총 SlideMaps = 14개(Tutorial1~3, Hard1, Slide07~16).
