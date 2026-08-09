# 작업일지 — 2026-07-24 (담당: KimYunjae / Player 파트) · 탄환 이펙트(EffectHandler) 스테이지 통합 + 병합 컴파일 오류 수정

Ricochet-Apocalypse · Unity 6000.3.18f1 · 2D · Legacy Input Manager · URP · NavMeshPlus(2D NavMesh)

이번 작업: 다른 작업자(Effect 파트)가 만든 **탄환 파티클 이펙트 시스템(`EffectHandler`)** 을 `BulletTest` 씬 설정을 참고해 **실제 사용 중인 Stage1/2/3 씬에 통합**. 그리고 origin/main 병합으로 들어온 **`BulletController` 컴파일 오류를 수정**.

> 결론 요약: origin/main의 `BulletController.HandleObstacleHit`이 Effect 담당자의 **잘못된 수동 병합으로 컴파일 깨진 채 커밋**돼 있어(중복 `destructible`/`explosiveEffect` 선언 전 사용/본문 없는 `if`) 재구성. 이펙트 호출은 **null-safe 헬퍼 `PlayHitEffect(EffectKind)`** 로 정리(EffectHandler 없거나 목록 비면 무시). EffectHandler는 씬 배치 싱글톤이라 BulletTest의 "Effect Manager" GO를 **`EffectHandler.prefab`** 으로 만들어 Stage1/2/3에 배치.

---

## 입력한 프롬프트 (원문, 시간순)

1. "지금 다른 작업자가 탄환에 이펙트를 작업했어 BulletTest scene을 참고하고 기존 사용하던 stage 씬에 기능을 통합해줘. 그리고 컴파일러에 오류가 뜨는데 이것도 확인해서 고쳐줘"
2. "커밋 및 작업로그 작성"

---

## 요청과 처리

### 1) 컴파일 오류 수정 — `BulletController.HandleObstacleHit`
- **원인**: origin/main 병합(9d86a0f)으로 들어온 코드에서 Effect 담당자가 이펙트 호출을 끼워 넣다 **수동 병합이 깨진 상태**로 커밋됨. 증상(CS1023/CS0103/CS0136): `destructible` 이중 선언, `explosiveEffect`를 선언 전에 사용, 본문 없는 `if (destructible != null)`, `isNewContact` 가드 밖으로 흩어진 문장. → origin/main 그 커밋 자체가 **빌드 불가** 상태.
- **수정**: 메서드를 원래 흐름(`isNewContact` 가드 → 파괴 장애물 처리 → `OnHitObstacle` 훅 → `switch(result)`)으로 재구성. `explosiveEffect`를 사용 전에 선언, `destructible` 단일 선언.
- 이펙트 호출을 **null-safe 헬퍼**로 정리:
  ```csharp
  private enum EffectKind { Hit, Bounce, Explosion }
  private void PlayHitEffect(EffectKind kind) {
      var handler = EffectHandler.Instance;
      if (handler == null) return;                 // 씬에 EffectHandler 없어도 안전
      var names = kind==Hit?handler.hitName : kind==Bounce?handler.bounceName : handler.explosionName;
      if (names == null || names.Count == 0) return; // 목록 비어도 안전
      handler.Play(names[Random.Range(0, names.Count)], transform.position);
  }
  ```
  적 피격→`Hit`, 벽 충돌→`Bounce`(가드 안으로 이동해 관통벽 매프레임 스팸 방지), 파괴형 장애물→`Explosion`.

### 2) EffectHandler를 Stage 씬에 통합
- `EffectHandler`는 **씬 배치 싱글톤**(자동 부트스트랩 없음, `Instance` + 오브젝트 풀링). 원본은 `BulletTest.unity`의 "Effect Manager" GO(등록: hit 4 / bounce 6 / explosion 4, 이펙트 프리팹 = `Assets/Prefabs/Effects/{Hit,Bounce,Explosion,Combo}` CartoonFX Remaster).
- 이 GO를 **`Assets/Prefabs/EffectHandler.prefab`** 로 저장(`PrefabUtility.SaveAsPrefabAsset`) → **Stage1/2/3에 인스턴스 배치**(각 씬 독립 인스턴스, 씬 전환 시 `OnDestroy`가 `Instance` 정리). 이미 있으면 건너뜀.

---

## 검증 (MCP)
- ✅ 컴파일: `script-execute`로 `typeof(BulletController)` + `PlayHitEffect` 리플렉션 확인 → **Assembly-CSharp 정상 빌드**, `PlayHitEffect exists=True`. (초기에 콘솔이 옛 오류를 계속 보여준 건 MCP 로그 파일 잠금으로 clear가 안 된 캐시.)
- ✅ 런타임: Play → Stage1 로드 후 `EffectHandler.Instance` 정상, `Play("Bounce1")` 풀에서 정상 반환(NRE/IndexOutOfRange 0건).

## 파일
| 구분 | 경로 |
|---|---|
| 수정 | `Assets/Scripts/Bullets/BulletController.cs` — 병합 컴파일 오류 수정 + `PlayHitEffect` null-safe 헬퍼 |
| 신규 | `Assets/Prefabs/EffectHandler.prefab` — BulletTest "Effect Manager" 설정을 프리팹화 |
| 수정 | `Assets/Scenes/Stage1.unity`, `Stage2.unity`, `Stage3.unity` — EffectHandler 인스턴스 배치 |

## ⚠️ 교훈 / 비고
- **origin/main에 빌드 불가 커밋이 올라와 있었음** — Effect 브랜치 병합(PR #4) 시 `HandleObstacleHit` 충돌 해소가 깨진 채 커밋됨. 팀에 공유 필요(다른 브랜치가 origin/main을 받으면 동일 오류).
- `DestructibleObstacle`(Bullets/Testing, 타 작업자 파일)은 `EffectHandler.Instance.Play`를 가드 없이 호출 → 이제 모든 스테이지에 EffectHandler가 있어 안전하나, 근본적으론 그쪽도 null-guard 권장.
- EffectHandler는 씬별 독립 인스턴스. 이후 스테이지가 늘면 각 씬에 `EffectHandler.prefab`을 배치해야 이펙트가 나온다.
- 상세는 CC 메모리 `bullet-effects-integration`에도 기록.
