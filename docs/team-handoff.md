# PARKSIDE 개발 인수인계

현재 소스 기준이다. PC에서 Unity로 편집하고, Pleos 입력을 확인할 때 Android APK를 빌드해 AVD에 설치한다.

파일 역할만 빠르게 보려면 [기능별 파일 요약](file-guide.md)을 먼저 읽는다.

**1. 현재 구현된 범위**

Pleos AVD의 핸들·페달 값을 Kotlin에서 읽고 검사한 뒤 Unity가 JSON 스냅샷으로 가져온다. Unity에는 작은 주차장, 차량 프리팹, 전진·후진 미리보기, 3종 시점, 진단 HUD와 Figma 기반 메뉴 5개 화면이 있다. 정밀 차량 물리·충돌·주차 성공 판정은 앞으로 구현한다. 앱 진입은 `MainMenu.unity`이며 [UI 실행과 Scene 구성 안내](ui-and-scenes.md)에 화면별 범위를 정리했다.

연속 가속·브레이크 비율은 현재 AVD 콘솔의 HTTP 어댑터로 읽는다. 실차 SDK에서 연속 페달 비율을 확보한 상태를 뜻하지 않는다.

**2. 저장소의 세 부분**

| 위치 | 담당 | 결과 |
|---|---|---|
| `app/` | 기존 Kotlin 2D 입력·주차 검증 앱 | `com.example.pleosdemo` APK |
| `vehicle-bridge/` | 공용 차량 입력·검증·Unity JNI 연결 | Android AAR 라이브러리 |
| `unity/InputProbe/` | Unity 씬·차량·카메라·연습 흐름 | `com.example.pleosdemo.unityprobe` APK |

공용 Kotlin 입력 원본은 이제 `vehicle-bridge`에만 있다. 기존 앱은 `implementation(project(":vehicle-bridge"))`로 사용하고 Unity는 생성된 AAR를 포함한다. 파일을 `app`에서 복사해 컴파일하던 Sync 단계는 제거했다.

Android Studio는 저장소 루트, Unity Hub는 `unity/InputProbe` 폴더를 연다. 두 APK는 앱 ID가 달라 AVD에 나란히 설치할 수 있다. Unity APK 안에 Kotlin 라이브러리가 포함되므로 기존 2D 앱을 먼저 실행할 필요는 없다.

**3. 입력과 실행 흐름**

```text
Pleos SDK: 조향·브레이크 상태 ─┐
AAOS: 실제 기어·속도 ──────────┤→ PleosVehicleInput
AVD HTTP: 가속·브레이크 비율 ───→ AvdConsolePedals
                                  ↓
Kotlin UnityInputState: 검사·0~1 변환
                                  ↓
PleosUnityBridge.readSnapshot(): JSON → PleosInputSource
PC W·Space·좌우 방향키 ───────────────→ KeyboardInputSource
                                  ↓ 같은 InputSnapshot 계약
VehicleInputReceiver → InputSnapshotInterpreter → VehicleControlInput
                                  ↓
GameManager: 시작·정지 판단 → VehicleController: 이동·바퀴
                                  ↓
VehicleCamera / PracticeHud / InputDiagnostics
```

Kotlin의 `start`, `readSnapshot`, `stop`은 Unity C#이 JNI로 호출한다. 두 언어 사이에 별도의 네트워크 서버는 없다. HTTP는 AVD의 페달 공급자를 읽는 경로다.

실제 기어는 P(`4`), 실제 속도는 0을 유지한다. 게임 안의 `Virtual D/R`과 가상 속도는 별개다. 검증한 AVD에서 콘솔 조향 +25도는 SDK -25도로 반환된다. `UnityInputState`에서 부호를 반전해 Unity에는 +25도로 전달한다. SDK 원본과 PC 키보드 입력은 변경하지 않는다.

**4. Unity 폴더와 책임**

```text
Assets/_Parkside/
├─ Scripts/
│  ├─ Core/GameManager.cs
│  ├─ Input/
│  │  ├─ InputSnapshot.cs
│  │  ├─ VehicleControlInput.cs
│  │  ├─ InputSnapshotInterpreter.cs
│  │  ├─ VehicleInputReceiver.cs
│  │  └─ Sources/
│  │     ├─ VehicleInputSource.cs
│  │     ├─ KeyboardInputSource.cs
│  │     └─ PleosInputSource.cs
│  ├─ Vehicle/VehicleController.cs
│  ├─ Camera/VehicleCamera.cs
│  ├─ UI/PracticeHud.cs
│  └─ Diagnostics/InputDiagnostics.cs
├─ Scenes/ParkingPractice.unity
├─ Prefabs/Vehicles/PracticeCar.prefab
├─ Materials/
└─ Editor/
   ├─ Build/ParksideAndroidBuild.cs
   └─ Validation/
      ├─ PracticeSceneValidator.cs
      └─ PracticeProjectValidation.cs
```

| 코드 | 책임 | 담당하지 않는 것 |
|---|---|---|
| `GameManager` | 시작·일시정지·리셋·가상 D/R, 씬의 연습 범위 확인 | JSON 해석·오브젝트 생성 |
| `InputSnapshot` | Kotlin JSON 필드 정의, 스키마 v1 | 이동 계산 |
| `VehicleControlInput` | 차량이 사용할 유효한 조향·가속·제동 값 | SDK 조회 |
| `InputSnapshotInterpreter` | JSON 파싱, 스키마·입력 유효성·신선도 검사 | 화면·차량 조작 |
| `VehicleInputReceiver` | 선택한 공급자 폴링·재연결·검사 연결 | 키 처리·JNI 구현·이동 |
| `VehicleInputSource` | 입력 공급자의 공통 계약 | 차량 조작 |
| `KeyboardInputSource` | W·Space·좌우 방향키를 스냅샷으로 변환 | 차량 직접 이동 |
| `PleosInputSource` | Kotlin JNI 연결과 JSON 수신 | 키보드 입력 |
| `VehicleController` | 조향 바퀴, 가속·제동·이동, 전진·후진 | 실제 차량 상태 읽기·UI |
| `VehicleCamera` | 씬에 지정한 앵커로 추적·탑뷰·운전자 시점 전환 | 차량 이동 |
| `PracticeHud` | Canvas 텍스트·상태·버튼 활성 여부 갱신 | UI 생성·배치·직접 입력 수신 |
| `InputDiagnostics` | FPS와 검증용 로그 출력 | 게임 진행 판단 |

`GameManager`는 씬에 하나만 두고 Inspector 참조로 연결한다. 싱글턴·전역 검색을 사용하지 않는다. 입력 수신기가 먼저 갱신되고, GameManager가 검증된 입력을 Controller의 `Simulate()`에 넘긴다. 카메라는 `LateUpdate()`에서 따라간다.

**5. 맵과 차량은 씬·프리팹에서 편집한다.**

현재 진입 씬은 `Assets/_Parkside/Scenes/MainMenu.unity`다. 연습장 자체만 편집·시험할 때는 `ParkingPractice.unity`를 직접 연다. 기존 `InputProbe.unity`와 `PleosInputProbe.cs`는 제거했다.

```text
ParkingPractice
├─ Environment          # 바닥·주차선·목표 주차면
├─ Sun                  # 조명
├─ PracticeCar          # 차량 프리팹 인스턴스
│  ├─ Body / Cabin / FrontMarker
│  ├─ FrontLeft / FrontRight / RearLeft / RearRight
│  └─ CameraAnchors
│     ├─ Chase
│     ├─ Top
│     └─ Driver
├─ SpawnPoints/PracticeSpawn
├─ MainCamera
├─ Systems
│  ├─ InputReceiver
│  ├─ GameManager
│  └─ InputDiagnostics
├─ PracticeCanvas       # Text·Button·Image와 PracticeHud
└─ EventSystem          # 마우스·터치 UI 입력
```

Play 전에도 맵과 Canvas UI 배치가 보인다. UI의 위치·폰트·색상·문구와 버튼 On Click 연결은 Scene의 Canvas에서 편집한다. `PracticeHud`는 저장된 UI 참조의 값만 갱신하며 OnGUI나 런타임 UI 생성은 사용하지 않는다. 런타임 코드가 맵·차량·조명·카메라를 생성하지 않는다. 빌드는 Build Settings에 등록한 저장된 씬을 읽고 패키징하며, 씬을 다시 만들거나 저장하지 않는다.

맵 담당자는 `Environment`를 편집하고, 차량 모양과 바퀴·카메라 앵커는 `PracticeCar.prefab`에서 편집한다. 차량 전방은 로컬 +Z, 월드 단위는 1유닛=1m로 맞춘다. 차량 모델을 바꿀 때 `VehicleController`의 앞바퀴 참조와 MainCamera의 앵커 참조를 확인한다. 새 차량 인스턴스로 교체했다면 GameManager·HUD·Diagnostics 참조도 다시 연결한다.

스폰 위치는 `PracticeSpawn`, 연습 범위는 GameManager의 `Practice Bounds`에서 조정한다. 범위를 벗어나면 해당 스폰으로 리셋된다. 재질은 `Materials`의 자산으로 공유한다.

**6. Kotlin 주요 파일**

원본 위치는 `vehicle-bridge/src/main/java/com/example/pleosdemo/`다.

| 파일 | 역할 |
|---|---|
| `VehicleInput.kt` | 값·앱 수신 시각, 실제 P·정차·SDK 신호 검사 |
| `PleosVehicleInput.kt` | Pleos SDK 구독과 AAOS 프로퍼티 조회·해제 |
| `AvdPedals.kt` | 페달 비율·신선도 검사 |
| `AvdConsolePedals.kt` | 별도 스레드에서 AVD HTTP 페달 조회 |
| `unity/UnityInputState.kt` | 공용 입력을 Unity 계약으로 변환 |
| `unity/PleosUnityBridge.kt` | Android 메인 스레드에서 수신 시작·종료, 최신 JSON 반환 |

신호가 없으면 0으로 위장하지 않고 누락 상태와 사유를 유지한다. SDK 신호는 앱 수신 후 3초 이내, 페달은 600ms 이내여야 한다. Unity도 JSON 수신 후 650ms 이내인지 확인한다. 입력 사용 불가 시 가속 0·가상 제동 1로 차단하며, 실제 차량에 제동 명령을 보내지 않는다.

JSON 필드 이름·타입을 바꾸면 Kotlin의 직렬화 코드와 C#의 `InputSnapshot`, 입력 해석 검사도 함께 변경한다. 연속 페달 비율은 0~100, Controller용 가속·제동은 0~1이다.

**7. PC에서 작업하는 순서**

1. Unity Hub Projects에 `unity/InputProbe` 폴더를 추가한다.
2. Unity 2022.3.16f1로 연다. PC 편집·키보드 미리보기는 Windows 대상으로 가능하며 Android 모듈이 없어도 컴파일된다.
3. `Assets/_Parkside/Scenes/ParkingPractice.unity`를 연다.
4. Scene/Hierarchy에서 편집하고 저장한다.
5. Play → `Start preview`를 누른다. W로 가속, Space로 제동, 좌우 방향키로 조향한다. A/D·위아래 방향키로 차량을 직접 이동시키지 않는다.
6. Inspector 값은 Play를 정지한 뒤 편집·저장한다. Play 중 변경은 정지하면 되돌아갈 수 있다.
7. `PARKSIDE → Validate scene and input`을 실행해 참조·입력·Controller 검사를 확인한다.

현재 HUD는 씬에 저장된 uGUI Canvas다. 이벤트는 버튼의 Inspector에 연결되어 있고, Space와 방향키가 UI 확인·탐색으로 중복 처리되지 않도록 EventSystem 키보드 탐색을 끄고 버튼 Navigation을 None으로 두었다. 이동은 Transform 기반 저속 미리보기이며 최대 2m/s, 브레이크가 0.01을 넘으면 즉시 정지한다. 정밀 제동·타이어·서스펜션·충돌 물리는 미구현이다. 해당 개발은 VehicleController 중심으로 진행한다.

**8. Android 빌드와 AVD 실행**

APK를 만들 PC에는 Android Build Support, SDK/NDK/OpenJDK, Android SDK 플랫폼 34·37, Build Tools 34.0.0, Pleos AVD, Python 3.9 이상을 준비한다. Hub 로그인·라이선스 활성화가 필요하다.

검증 PC에는 Editor가 두 개 있다. Android 모듈이 설치된 실행 파일은 다음이다.

```text
C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor\Unity.exe
```

`2022.3.16f1-x86_64` 폴더의 Editor는 Android 모듈이 없고 PC 편집용으로 사용할 수 있다. Hub에서 버전뿐 아니라 경로도 확인한다.

아래는 검증 PC의 경로 예시다. 팀원 PC에서는 자신의 경로로 바꾸고 `local.properties`도 맞춘다. 배치 빌드 전 같은 Unity 프로젝트를 Editor에서 닫는다.

빌드·콘솔 스크립트는 `tools/android-environment.ps1`로 SDK·JDK·Unity 경로를 탐색한다. 기본 설치가 아니면 `build-unity-input-probe.ps1 -UnityEditor <Unity.exe> -AndroidSdk <SDK> -JavaHome <JDK 25>`로 지정한다. 아래 환경 변수 예시는 수동 Gradle 실행에도 사용할 수 있다.

```powershell
Set-Location 'D:\Android\Projects\PleosDemo'
$env:ANDROID_HOME = 'D:\Android\Sdk'
$env:JAVA_HOME = 'D:\Android\AndroidStudio\jbr'
& .\tools\build-unity-input-probe.ps1
```

출력은 `build/unity-probe/parkside-input-probe.apk`다. Kotlin 라이브러리 → Unity AAR 복사 → 저장된 씬 내보내기 → APK 패키징 순서다. C#·씬만 바뀌었더라도 APK를 다시 만들어야 AVD에 반영된다.

Pleos AVD를 Cold Boot하고 P·속도 0에서 자동 검사한다.

```powershell
py -3 .\tools\validate-unity-input-probe.py
```

기본 대상은 `emulator-5554`이며 `--serial`, `--adb`로 바꿀 수 있다. 결과는 `build/unity-probe/validation.json`과 로그·PNG에 저장된다. 다른 콘솔 세션이 살아 있으면 연결 종료 검사를 건너뛴 사실도 기록한다.

수동 조작은 다음 명령을 실행하고 터미널을 유지한다.

```powershell
& .\tools\start-parking-console.ps1
```

PC 브라우저의 `http://127.0.0.1:8090/`을 열고 AVD의 `Pleos Unity Input Probe` 앱에서 시작한다. 콘솔의 실제 기어는 P를 유지하고 가상 전진·후진은 앱에서 바꾼다. 자동 검사 종료 뒤 콘솔 세션이 닫혀 앱이 대기하는 것은 정상이다.

PC의 8090 프록시는 기본 콘솔의 P단 악셀 제한을 시험용으로 바꾼 화면을 전달한다. `adb forward tcp:8088 tcp:8088`로 AVD 서버와 연결한다. APK는 AVD 내부의 `127.0.0.1:8088/vehicle/state`를 직접 읽는다.

**9. 도구 버전과 검사**

| 구분 | 고정한 조합 |
|---|---|
| Unity | 2022.3.16f1, Built-in, GLES3, x86_64, IL2CPP |
| Kotlin 라이브러리 빌드 | Gradle 9.6.0 / AGP 9.4.1, 검증 PC JBR 25.0.3 |
| Unity APK 패키징 | 내장 Gradle 7.2 / AGP 7.1.2 / OpenJDK 11 / NDK r23b |
| Android SDK | 공용 Kotlin compile 37, Unity compile/target 34, Build Tools 34.0.0 |
| Pleos | Vehicle SDK 2.3.2, Connect v2.1.0 / Android 14 / x86_64 AVD |

Kotlin 단위 테스트는 입력 라이브러리 21개·기존 2D 앱 13개, 총 34개다. Unity의 `Validate scene and input`은 씬 참조·JSON·잘못된 입력 차단·전진·후진·제동·리셋을 검사한다. Editor 검사와 APK의 실제 SDK 입력 검사는 별도다.

`PracticeSceneValidator`는 저장된 씬 연결을 검사하며 빌드에서도 사용한다. `PracticeProjectValidation`은 여기에 입력·차량 회귀 검사를 더한다. 연습 컴포넌트가 없는 메뉴 씬은 차량·HUD가 없어도 된다. 공급자는 비활성화·삭제 시 연결을 정리하며, 새 공급자도 `Disconnect()`를 여러 번 호출해도 안전하게 구현한다. 진단 로그의 `allowed`는 Kotlin 원본, `inputReady`는 C# 검사까지 마친 결과다.

기존 AVD 입력 검사에서는 가속 37%, 콘솔 +25도에 대한 SDK -25도, 브레이크 50%, 입력 해제·연결 종료 시 차단을 확인했다. 최신 실행 결과는 [입력 검증 기록](unity-input-probe.md)에 기록한다. 최종 월드의 성능과 장시간 안정성, 실차 지원은 별도 검증 대상이다.

**10. 협업 규칙**

Unity 경험자는 입력·Controller·GameManager·카메라와 APK 통합을, 입문자는 맵·모델·재질·프리팹·UI 배치를 우선 맡는 구성이 적절하다. 같은 씬 파일을 동시에 수정하지 않도록 담당을 정하고, 맵이 커지면 별도 맵 씬·프리팹으로 나눈다.

Git에는 Kotlin 소스·테스트, Unity `Assets/Packages/ProjectSettings`, 모든 `.meta`, Gradle 설정·Wrapper, `tools`, `docs`를 공유한다. `.meta`에는 자산 참조 ID가 있으므로 함께 이동·커밋한다. 현재 Unity는 Visible Meta Files / Force Text로 설정되어 있다.

`Library/Temp/Logs/UserSettings`, `build`, 생성 AAR·APK·SDK 사본, `local.properties`는 공유하지 않는다. 생성된 `android-export`를 직접 고치면 다음 빌드에 덮일 수 있으므로 수정은 원본 Gradle 템플릿에 반영한다. 현재 작업은 로컬 변경이며 이 정리에서 커밋·푸시는 수행하지 않았다.

**11. 문제를 만났을 때**

| 증상 | 확인할 곳 |
|---|---|
| 예전 씬이 사라졌거나 화면이 비어 있음 | 새 `ParkingPractice.unity`를 열기 |
| 차량이 움직이지 않음 | Start preview, Game 창 키보드 포커스, InputReady·차단 사유 |
| APK에서 입력 대기 | 8090 콘솔 세션, P·속도 0, 권한, 샘플 경과 시간 |
| P에서 악셀 0 | 브라우저가 8090 프록시를 사용하는지 |
| AndroidPlayer 미지원 | PC는 Windows 대상으로 열기, APK는 모듈이 설치된 Editor 사용 |
| Safe Mode 컴파일 오류 | 최신 소스 적용 후 Ctrl+R, Console의 첫 컴파일 오류 확인 |
| 씬 검사 실패 | Inspector에서 누락된 컴포넌트·앞바퀴·스폰·카메라 참조 확인 |
| 빌드 뒤 콘솔 연결 끊김 | ADB 대상 확인 후 콘솔 시작 스크립트 재실행 |

자세한 빌드 재현 절차는 [Unity 입력 테스트 안내](unity-input-probe.md), 기존 2D 앱은 [README](../README.md)를 참고한다.
**입력 공급자 교체 방법**

`Systems/InputReceiver`의 `VehicleInputReceiver` 컴포넌트에서 `Desktop Source`와 `Android Source`를 지정한다. 기본 연결은 Desktop=KeyboardInputSource, Android=PleosInputSource다. Editor에서는 빌드 타깃과 관계없이 Desktop을 사용하고, Android APK에서만 Android 필드를 사용한다.

두 공급자는 모두 `VehicleInputSource`를 상속하며 `Connect`, `ReadSnapshot`, `Disconnect`를 구현한다. `ReadSnapshot`은 같은 `InputSnapshot`을 반환한다. 다른 입력 장치를 붙일 때 이 계약을 구현한 컴포넌트를 만든 뒤 해당 필드에 끼우면 된다. GameManager·Interpreter·VehicleController는 수정하지 않는다.

실행 중 공급자 참조를 바꾸거나 비활성화하면 현재 연습을 일시정지한다. 새 공급자가 정상이어도 자동으로 주행을 재개하지 않는다. 연결이 실패했을 때 키보드로 자동 대체하지 않는다.

KeyboardInputSource의 기본 키는 W=가속, Space=브레이크, Left/Right Arrow=조향이다. 키는 컴포넌트 Inspector에서 바꿀 수 있다. W와 Space를 동시에 누르면 제동이 우선한다. 가상 후진은 UI의 Virtual D/R로 고른다.

**Canvas 편집 위치**

`PracticeCanvas/DebugPanel` 아래의 Text·Image·Button을 Scene에서 편집한다. 위치·크기·폰트·색상·고정 문구는 각 컴포넌트의 Inspector 값이다. `PracticeHud`는 런타임 수치·상태만 갱신한다. 버튼의 On Click은 GameManager 또는 VehicleCamera의 공개 메서드로 씬에 연결되어 있다.

`EventSystem.Send Navigation Events=false`, 버튼 Navigation=None은 Space 제동·좌우 조향이 UI 버튼 제출·탐색과 충돌하지 않기 위한 설정이다. UI를 바꿀 때도 이 키를 차량 입력과 중복 사용하지 않는다. uGUI 1.0.0 패키지는 Packages 목록과 함께 공유한다.
