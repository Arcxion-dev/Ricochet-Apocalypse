# 작업일지 — 2026-08-08~09 (담당: KimYunjae) · 모바일(Android) 터치 조작·UI 전환 + Device Simulator 검증 + Android 빌드 픽스

Ricochet-Apocalypse · Unity 6000.3.18f1 · 2D · URP · Legacy Input Manager

이번 작업: 데스크톱(마우스/키보드) 조작을 모바일 터치로 전환. 조준=꾹눌러 드래그→떼기 격발(캐릭터로 끌면 취소), 두 손가락 핀치 줌+팬(조준 중 자동 잠금), 인벤·파츠 닫기 버튼, HUD 세이프에어리어. 이후 자동 제스처 테스트로 로직 검증하고, Android APK 빌드를 막던 Kotlin 중복 클래스 문제까지 해결.

## 입력한 프롬프트 (원문)
1. "모바일로 빌드할건데 조작 및 UI면에서 손봐야할 것들 알려줘. 이건 일단 미리 생각해놓은거야. 인벤토리 닫기 버튼, 조준(한번 꾹 누르기)->누른 상태로 드래그해 위치조정->격발(손 떼기)순,확대/축소는 만약 넣는다면 조준중에는 불가능하게 만들고 기본 상태에서 두 손가락으로 벌려서 확대/줄여서 축소,조준중에 취소하고싶으면 캐릭터쪽으로 드래그하면 취소할 수 있게"
2. "po부터 구현"
3. "P1 이어서 진행해줘"
4. "커밋하고 p2 진행"
5. "Device Simulator로 터치 검증 진행해줘"
6. "B 빌드돌리면 내가 검증해볼게"
7. (빌드 실패 로그 붙여넣기: `:launcher:checkReleaseDuplicateClasses ... Duplicate class kotlin...jdk8 ...`)
8. "작업 이어서 진행해" / "작업이어서진해ㅑㅇ해"
9. (동일 Kotlin 중복 에러 재붙여넣기)

## 확정된 조작 스킴(사용자 선택)
- 카메라 팬 = **두 손가락 드래그**(핀치 줌과 함께). 이동 = **좌/우 버튼만**(상하 없음). 아이템 투척 = **총알과 동일 제스처**로 통일.

## 처리 — P0 조작 (`PlayerShooter`, `CameraPanController`, 신규 `TouchInput`)
- 조준/격발 터치: 손가락 Down(UI 밖)→홀드/드래그 임계 통과 시 `EnterBreath`(호흡·차징), 드래그로 `ApplyBreathSway(터치방향)` 추종, **손 떼면 발사**. 캐릭터 취소반경 안에서 떼면 취소. 짧은 탭=차징 없는 즉발 스냅샷.
- 아이템 투척도 동일(퀵슬롯 탭 장전→월드 꾹눌러 드래그→떼면 투척/취소).
- 핀치 줌 + 두 손가락 팬을 `CameraPanController.HandleTouch`에 추가. `EnterBreath`가 이미 `ControlsEnabled=false`로 화면을 잠가 **조준 중 줌/팬 자동 비활성**(설계 그대로). 멀티터치 중재: 확정 전 2번째 손가락이 오면 조준 양보→핀치.
- `ControlScheme.Auto`(모바일만 터치, 에디터는 마우스 유지 → 에디터 Play 테스트 지속 가능). `MobileControls` 정적 플래그로 HUD 문구 분기.

## 처리 — P1 UI
- 인벤토리 우상단 ✕ 닫기 버튼 코드 생성(`InventoryUI.EnsureCloseButton`), 무기 파츠 패널 ✕ 닫기 버튼(파츠는 인벤 PARTS 탭으로도 관리 가능).
- `StageHud`를 런타임에 `SafeAreaFitter`로 래핑(위젯 10개는 안전영역 안, 배경 스크림은 전체화면 유지) — 프리팹 무수정.

## 처리 — P2 폴리시
- 조준 중 캐릭터 둘레 **취소존 링** 표시(손가락 들어오면 진하게), 총알·아이템 공통.
- `ChargeShotEffects`가 연출 종료 시 base 직교크기 캡처를 리셋 → 조준 사이 **핀치 줌값이 유지**됨(이전엔 최초값으로 되돌아가는 버그).
- 데스크톱 문구 정리(퀵슬롯 1/2/3 숨김, 파츠 O키·아이템 조준 힌트 모바일화).

## 처리 — 검증용 리팩터 + Device Simulator
- **TouchInput을 `ITouchProvider`로 주입 가능화**(기본=실 Input, 테스트=FakeTouchProvider). PlayerShooter/CameraPanController의 모든 터치 읽기를 이 경로로 통일 → 합성 제스처 주입으로 자동 검증 가능.
- `UseTouch`/`MobileControls`/`SafeAreaFitter`/팬 픽셀환산을 `UnityEngine.Device.*`로 전환 → Device Simulator에서도 모바일(터치·노치)로 인식.
- **중요 제약**: Device Simulator는 new Input System에만 터치를 시뮬레이션 → 이 프로젝트(Legacy Input)에선 Simulator에서 `Input.GetTouch`에 마우스 터치가 안 들어감. Simulator는 **비주얼(노치·레이아웃)만** 검증 가능, 제스처 입력은 실기기라야 함 → 그래서 아래 자동 테스트로 대체.

## 검증 (자동 제스처 테스트, 플레이모드)
- 라이브 PlayerShooter에 FakeTouchProvider로 합성 제스처 주입: **홀드→드래그→떼기=발사(ammo 5→4, breath 도달), 캐릭터로 끌어 떼기=취소(ammo 4→4)** — pass=True.
- CameraPanController 독립 테스트: 두 손가락 벌리면 **줌 in(size 8→4)**, `ControlsEnabled=false`면 **핀치 차단(불변)** — pass=True.
- P1 스모크: InventoryUI CloseButton/WeaponPartsUI 닫기/StageHud SafeArea(위젯 10개 래핑, 스크림은 전체화면) 생성 확인.

## Android 빌드 픽스 (Kotlin 중복 클래스)
- 증상: `:launcher:checkReleaseDuplicateClasses` 실패 — Play Billing/IAP 플러그인이 끌어온 `kotlin-stdlib-jdk7/jdk8:1.6.21`이 Unity의 `kotlin-stdlib:1.8.22`와 중복. **터치 코드와 무관**(컴파일·IL2CPP·Burst 통과, gradle 패키징만 실패).
- 1차 시도(실패): `mainTemplate.gradle`(=`:unityLibrary` 모듈)에 force → 중복 검사는 `:launcher` 모듈이라 안 미침(force는 모듈별).
- 해결: **`baseProjectTemplate.gradle`(신규)** 의 `allprojects { configurations.all { resolutionStrategy { force kotlin-stdlib(-jdk7/-jdk8):1.8.22 } } }` → 모든 모듈(:launcher 포함) 적용. `ProjectSettings.useCustomBaseGradleTemplate` 토글이 꺼져 있어 파일이 무시되던 것도 켬(에디터 인메모리에도 SerializedObject로 반영).
- **함정**: 에디터가 비포커스/유휴면 메인스레드 틱이 멈춰 MCP 빌드 트리거가 'Failed after 10 retries'로 디스패치 실패·delayCall 미발화 → 빌드가 시작조차 안 됨. 빌드는 에디터 포커스 상태에서 실행해야 함. 빌드 진행/결과는 `Editor.log`(파일시스템)로 추적.

## 후속/확인 필요
- 실기기(Android)에서 터치 제스처(꾹눌러 드래그·취소존·핀치·투척) 실동작 검증. `PlayerShooter._controlScheme=ForceTouch`+Device Simulator로는 비주얼(세이프에어리어)만 확인.
- 패키지명 기본값 `com.defaultcompany.rirochet` → 배포 전 변경 권장.
- `baseProjectTemplate.gradle`/`ProjectSettings`/Billing 파일은 팀원(Billing 담당) 영역과 겹침 — 이력 정리 시 조율 필요.

## 변경 파일 (커밋 dfb5d1c, b2c171b, 895e0e0, db838ad)
- 신규: `Assets/Scripts/Input/TouchInput.cs`, `Assets/Plugins/Android/baseProjectTemplate.gradle`
- 수정: `PlayerShooter.cs`, `CameraPanController.cs`, `Inventory/InventoryUI.cs`, `Weapons/WeaponPartsUI.cs`, `UI/Stage/StageHud.cs`, `UI/Stage/QuickSlotView.cs`, `UI/SafeAreaFitter.cs`, `ChargeShotEffects.cs`, `ProjectSettings.asset`
