# 작업일지 — 2026-08-07 (담당: KimYunjae) · 총알 확인 UI(인게임 HUD) 영구 자동생성 + 타이틀 PLAY 버튼 이어하기/새로하기 라벨 토글

Ricochet-Apocalypse · Unity 6000.3.18f1 · 2D · URP

이번 작업: 씬 연결(40개 SlideMaps → Stage_NN 본게임 스테이지 연결, 커밋 a5ced99) 후속. ① 옛 씬에만 있던 "총알 확인 UI"(StageHud의 리볼버 실린더)를 새 스테이지들에 다시 넣기, ② 타이틀 PLAY 버튼이 세이브 삭제 후에도 "이어하기"에서 "새로하기"로 안 바뀌던 문제 확인·수정.

## 입력한 프롬프트 (원문)
1. "이전에 씬 연결하던 작업 이어해. 기존씬에 있던 총알 확인 ui 넣고 이어하기 버튼 세이브 삭제했음에도 새로하기로 변경안되는거 고쳤는지 확인하고 안됐으면 이어해"

## 구조 파악(핵심)
- **씬 파일은 바이너리 직렬화** → grep 무의미, Unity MCP로만 조사.
- 씬 연결 작업은 이미 커밋됨: `Stage_01~40`은 각각 Player/GameManager/TileMap/Enemies/매니저들까지 완전 배치돼 있음. **단, 인게임 HUD가 하나도 없음**(어느 스테이지에도 StageHud 미배치).
- "총알 확인 UI" = `StageHud` 프리팹 안의 `RevolverCylinderUI`(잔탄 + 탄환 선택 실린더). 옛 개발 씬 `Scenes/Stage1.unity`만 StageHud를 직접 배치해 쓰고 있었음(25 refs).
- 타이틀 PLAY 버튼(`TitleScreen/SafeArea/PlayButton`)은 색 패널(스프라이트 없음) + 자식 TMP 2개: **Label="이어하기"**, **Tag="▶ STAGE 3"** 가 씬에 **하드코딩**. `TitleMenuUI`는 이 텍스트를 전혀 갱신하지 않아 세이브 삭제 후에도 그대로 "이어하기/STAGE 3" 로 남던 것이 원인(클릭 동작 자체는 `LoadStage(CurrentStageIndex)`로 이미 정상).

## 처리 — ① 총알 확인 UI(영구 자동생성 싱글톤; 사용자 선택)
- `Assets/Prefabs/UI/StageHud.prefab` → `Assets/Resources/UI/StageHud.prefab` 로 **git mv**(GUID 보존 → 옛 Stage1 참조 유지). 코드 전용 부트스트랩이 Resources.Load로 집기 위함.
- 신규 `Scripts/UI/Stage/StageHudBootstrap.cs`: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 게임 시작 시 1회 `Resources.Load<GameObject>("UI/StageHud")` → Instantiate → `DontDestroyOnLoad`. StageHud가 매 씬 Player/PlayerShooter를 폴링해 스테이지 밖(타이틀/상점/결과)에선 스스로 숨으므로 단일 영구 인스턴스로 전 씬 커버. 씬에 이미 StageHud가 있으면(옛 씬) 스폰 스킵.
- EventSystem 보강: 타이틀 경유 시 SceneNavigatorUI가 영구 EventSystem을 만들지만, 스테이지로 바로 진입한 경우 대비해 없을 때만 하나 생성(중복 가드).
- **40개 씬 개별 편집 불필요.**

## 처리 — ② 타이틀 PLAY 라벨 토글(동적 라벨; 사용자 선택)
- `TitleMenuUI`에 `_playLabel`/`_playTag`(TMP_Text) 필드 추가(비우면 PlayButton 자식 "Label"/"Tag" 자동 탐색) + `RefreshPlayButtonLabel()`을 Start의 세이브 로드 직후에 호출.
  - 세이브 있음 → Label="이어하기", Tag="▶ STAGE {복원된 index+1}".
  - 세이브 없음 → Label="새로하기", Tag="▶ STAGE 1".
- 세이브 삭제(SettingsUI) → `SceneLoader.LoadTitle()`로 타이틀 재로드 → Start 재실행 → 라벨 자동 재계산되어 "새로하기/STAGE 1"로 갱신(버그 해소).

## 검증(에디트타임, script-execute)
- HudProbe: `Resources.Load("UI/StageHud")` 성공, hasStageHud=True, hasCylinder=True, root=Canvas → DontDestroyOnLoad 적합.
- LabelProbe: PlayButton의 Label/Tag 자식 해석 성공, 포맷 문자열 정상(no-save 시 "새로하기"/"▶ STAGE 1"). ※ 에디트모드에선 SaveManager.Instance=null이라 HasSave=False로 보이는 건 정상(런타임엔 파일 존재 시 True).
- 컴파일 에러 없음. `set-state`(플레이모드) MCP 미제공 + 도메인리로드로 연결 끊길 위험 → 런타임 육안 확인은 미실시.

## 후속/확인 필요(실제 플레이에서)
- 스테이지 진입 시 HUD 실린더가 화면에 뜨는지, 타이틀/상점에선 숨는지 육안 확인.
- 인게임에서 "세이브 삭제" → 타이틀 복귀 시 PLAY가 "새로하기/STAGE 1"로 바뀌는지 확인.
- 옛 개발 씬 `Scenes/Stage1`에서만 (배치본+영구본) 중복 가능 — 본게임 흐름(Title+Stage_NN)엔 무관.

## 변경 파일
- 신규: `Assets/Scripts/UI/Stage/StageHudBootstrap.cs`
- 수정: `Assets/Scripts/UI/TitleMenuUI.cs`
- 이동: `Assets/Prefabs/UI/StageHud.prefab` → `Assets/Resources/UI/StageHud.prefab` (+.meta, GUID 보존)
