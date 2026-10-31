# PARKSIDE 기능별 파일 요약 — 팀원 공유용

PC에서 Unity로 작업하고, Pleos 입력을 확인할 때 Android APK를 AVD에 설치한다.

**전체 구성**

| 폴더 | 무엇인가 |
|---|---|
| `unity/InputProbe/` | 지금 개발하는 Unity 프로젝트. 화면·차량·카메라·게임 진행 담당 |
| `vehicle-bridge/` | Kotlin 공용 입력 라이브러리. Pleos 입력 수신·검사·Unity 연결 담당 |
| `app/` | 이전에 만든 Kotlin 2D 검증 앱. 참고·비교용으로 유지 |
| `tools/` | APK 빌드·PC 콘솔·AVD 검사 스크립트 |
| `docs/` | 파일 설명·협업·빌드 안내 |

```text
PC 키보드 → KeyboardInputSource ─┐
Pleos/Kotlin → PleosInputSource ──┤
                               ↓ 같은 InputSnapshot
VehicleInputReceiver → InputSnapshotInterpreter → VehicleControlInput
                               ↓
                    GameManager → VehicleController
                               ↓
                  VehicleCamera / PracticeHud / InputDiagnostics
```

**1. 씬·차량·UI: Unity에서 직접 편집하는 자산**

아래 파일은 `unity/InputProbe/Assets/_Parkside/` 안에 있다.

| 파일·위치 | 역할 / 여기서 수정하는 것 |
|---|---|
| [Scenes/ParkingPractice.unity](../unity/InputProbe/Assets/_Parkside/Scenes/ParkingPractice.unity) | 시작 씬. 맵·조명·차량·카메라·입력 연결·Canvas를 배치한다. Play 전에도 보인다. |
| 씬의 `Environment` | 바닥·주차선·목표 주차면 위치와 크기 |
| 씬의 `PracticeCanvas` | UI 위치·폰트·색상·고정 문구·버튼 On Click 연결 |
| 씬의 `Systems/InputReceiver` | Desktop Source / Android Source에 입력 공급자 연결 |
| 씬의 `SpawnPoints/PracticeSpawn` | 초기화 시 차량이 돌아갈 위치·방향 |
| [Prefabs/Vehicles/PracticeCar.prefab](../unity/InputProbe/Assets/_Parkside/Prefabs/Vehicles/PracticeCar.prefab) | 차량 모양·바퀴·카메라 앵커를 묶은 재사용 자산 |
| `Materials/Ground.mat`, `ParkingLine.mat`, `ParkingTarget.mat` | 바닥·주차선·목표 주차면 재질 |
| `Materials/CarBody.mat`, `CarGlass.mat`, `WheelRubber.mat` | 차체·유리·바퀴 재질 |

맵·차량·UI를 만드는 런타임 코드는 없다. 배치는 씬·프리팹에서 하고 빌드는 저장된 씬을 사용한다.

**2. Unity C#: 입력·진행·차량·화면**

아래 파일은 `Assets/_Parkside/Scripts/` 기준이다.

| 파일 | 역할·핵심 동작 |
|---|---|
| [Core/GameManager.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Core/GameManager.cs) | 연습의 시작·정지·리셋·가상 D/R 관리. 입력이 유효할 때 Controller를 실행한다. |
| [Input/InputSnapshot.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Input/InputSnapshot.cs) | 입력 원본 자료형. 조향·페달·실제 기어·속도 등 JSON 필드 정의 |
| [Input/VehicleControlInput.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Input/VehicleControlInput.cs) | 검사 후 차량에 전달하는 자료형. IsValid·조향·가속·제동·차단 사유 |
| [Input/InputSnapshotInterpreter.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Input/InputSnapshotInterpreter.cs) | JSON 파싱과 입력 검사. 잘못된 값·오래된 값·P/정차 조건 위반을 차단한다. |
| [Input/VehicleInputReceiver.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Input/VehicleInputReceiver.cs) | 공급자를 선택하고 주기적으로 스냅샷을 읽는다. 재연결·일시정지·해석기 호출 담당 |
| [Input/Sources/VehicleInputSource.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Input/Sources/VehicleInputSource.cs) | 공급자의 공통 추상 클래스. Connect / ReadSnapshot / Disconnect 약속과 비활성화·삭제 시 연결 정리. Disconnect는 여러 번 호출해도 안전해야 한다. |
| [Input/Sources/KeyboardInputSource.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Input/Sources/KeyboardInputSource.cs) | PC 테스트 입력. W=가속, Space=브레이크, ←/→=조향을 같은 스냅샷으로 변환 |
| [Input/Sources/PleosInputSource.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Input/Sources/PleosInputSource.cs) | Android APK용 입력. JNI로 Kotlin을 호출해 JSON 스냅샷 수신 |
| [Vehicle/VehicleController.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Vehicle/VehicleController.cs) | 가상 차량의 이동·회전·제동·바퀴 조향 계산. 입력 장치나 UI를 직접 읽지 않는다. |
| [Camera/VehicleCamera.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Camera/VehicleCamera.cs) | 차량 프리팹의 앵커를 이용해 뒤쪽 추적·탑뷰·운전자 시점 전환 |
| [UI/PracticeHud.cs](../unity/InputProbe/Assets/_Parkside/Scripts/UI/PracticeHud.cs) | Canvas에 연결한 수치·상태·버튼 활성 여부만 갱신. UI 생성·배치 코드는 없음 |
| [Diagnostics/InputDiagnostics.cs](../unity/InputProbe/Assets/_Parkside/Scripts/Diagnostics/InputDiagnostics.cs) | FPS·기기·입력 로그 기록. Kotlin 원본 allowed와 C# 검사 후 inputReady를 구분해 출력 |

**입력을 바꿀 때:** Receiver의 Inspector에 다른 `VehicleInputSource` 컴포넌트를 지정한다. 기본값은 Desktop=Keyboard, Android=Pleos다. Editor는 Desktop 필드, Android APK는 Android 필드를 사용한다. GameManager·Controller를 수정하지 않는다.

**UI를 바꿀 때:** Canvas 자산을 편집한다. 버튼은 Inspector에서 GameManager·VehicleCamera의 공개 메서드에 연결한다. Space 제동과 충돌하지 않도록 EventSystem의 키보드 탐색은 꺼두었다.

**3. Kotlin: 실제 입력 수신·검사·Unity 연결**

아래 파일은 `vehicle-bridge/src/main/java/com/example/pleosdemo/` 기준이다. Unity APK에 라이브러리 형태로 포함된다.

| 파일 | 역할·핵심 동작 |
|---|---|
| [VehicleInput.kt](../vehicle-bridge/src/main/java/com/example/pleosdemo/VehicleInput.kt) | 신호 값·수신 시각 저장. 실제 P단·정지와 SDK 입력 신선도 검사 |
| [PleosVehicleInput.kt](../vehicle-bridge/src/main/java/com/example/pleosdemo/PleosVehicleInput.kt) | Pleos SDK의 조향·브레이크 상태와 AAOS 기어·속도 구독·조회·해제 |
| [AvdPedals.kt](../vehicle-bridge/src/main/java/com/example/pleosdemo/AvdPedals.kt) | 페달 비율 자료형. 0~100 범위와 갱신 시간 검사 |
| [AvdConsolePedals.kt](../vehicle-bridge/src/main/java/com/example/pleosdemo/AvdConsolePedals.kt) | AVD 내부 콘솔 HTTP에서 가속·브레이크 비율 읽기 |
| [unity/UnityInputState.kt](../vehicle-bridge/src/main/java/com/example/pleosdemo/unity/UnityInputState.kt) | 입력 합치기·검사·0~1 변환. 예: 가속 37% → throttle=0.37 |
| [unity/PleosUnityBridge.kt](../vehicle-bridge/src/main/java/com/example/pleosdemo/unity/PleosUnityBridge.kt) | Unity 호출 창구. start=수신 시작, readSnapshot=JSON 반환, stop=수신 종료 |

현재 연속 페달 비율은 AVD 전용 HTTP 입력이다. 실제 차량 페달 지원은 별도 검증이 필요하다. 실제 기어는 P·속도 0을 유지하고 게임의 가상 D/R을 따로 사용한다.

**4. 빌드·검사·설정**

| 파일·위치 | 역할 |
|---|---|
| [Editor/Build/ParksideAndroidBuild.cs](../unity/InputProbe/Assets/_Parkside/Editor/Build/ParksideAndroidBuild.cs) | 저장된 씬 검사·Android 도구 설정·Gradle 프로젝트 내보내기. 씬을 재생성하지 않음 |
| [Editor/Validation/PracticeSceneValidator.cs](../unity/InputProbe/Assets/_Parkside/Editor/Validation/PracticeSceneValidator.cs) | 빌드 씬의 참조·Canvas 버튼 검사. 연습 컴포넌트가 없는 메뉴 씬에는 차량·HUD를 요구하지 않음 |
| [Editor/Validation/PracticeProjectValidation.cs](../unity/InputProbe/Assets/_Parkside/Editor/Validation/PracticeProjectValidation.cs) | 씬 검사와 입력 계약·차량 동작 회귀 검사. Editor 메뉴에서 실행 가능 |
| [tools/build-unity-input-probe.ps1](../tools/build-unity-input-probe.ps1) | Kotlin AAR 생성 → Unity 내보내기 → 최종 APK 패키징 |
| [tools/android-environment.ps1](../tools/android-environment.ps1) | SDK·JDK·Unity 경로 탐색. 빌드·콘솔 스크립트가 함께 사용 |
| [tools/validate-unity-input-probe.py](../tools/validate-unity-input-probe.py) | AVD APK 설치·실행, 콘솔 입력과 Unity 로그 비교, 캡처·결과 저장 |
| [tools/tests/test_probe_validation.py](../tools/tests/test_probe_validation.py) | 로그의 실제 입력 허용 상태 구분·ADB 경로 탐색 검사 |
| [tools/start-parking-console.ps1](../tools/start-parking-console.ps1) | ADB 포트 전달과 PC 콘솔 실행 |
| [tools/parking_console.py](../tools/parking_console.py) | 8090 콘솔 프록시. 실제 P단을 유지하며 AVD 페달을 시험하도록 지원 |
| `Assets/Plugins/Android/*Template.gradle`, `gradleTemplate.properties` | Unity APK의 Android 의존성·저장소·패키징 설정 |
| `vehicle-bridge/build.gradle.kts` | 공용 Kotlin 라이브러리 빌드와 Unity용 AAR 복사 |
| `vehicle-bridge/src/main/AndroidManifest.xml` | 차량 권한·자동차 라이브러리 선언 |
| `vehicle-bridge/src/debug/` | AVD 로컬 HTTP 권한·통신 설정 |
| `Packages/manifest.json`, `packages-lock.json` | Unity 패키지 목록·고정 버전. Canvas는 uGUI 1.0.0 사용 |
| `ProjectSettings/` | Unity 버전·빌드 씬·플랫폼·그래픽·입력 설정 |

**5. 기존 2D 앱과 테스트**

| 파일·위치 | 역할 |
|---|---|
| `app/.../MainActivity.kt` | 기존 2D 앱의 화면·시작/정지·입력 모드 연결 |
| `app/.../ParkingView.kt` | 기존 2D 주차장·차량 그리기 |
| `app/.../ParkingSimulation.kt` | 기존 2D 이동·충돌·주차 판정. Unity Controller와 별도 구현 |
| `vehicle-bridge/src/test/` | VehicleInputTest·AvdPedalsTest·UnityInputStateTest: 공용 입력 검사 |
| `app/src/test/` | ParkingSimulationTest·PedalSimulationTest 등: 기존 2D 계산 검사 |

**팀원이 처음 볼 순서**

1. Unity에서 `ParkingPractice.unity`를 열어 Hierarchy·Canvas·차량 프리팹 확인
2. `GameManager.cs`에서 전체 진행 확인
3. `VehicleInputReceiver` → 공급자 두 파일 → `InputSnapshotInterpreter`로 입력 흐름 확인
4. 담당에 따라 `VehicleController`, `VehicleCamera`, Canvas·`PracticeHud` 확인

맵·UI 담당자는 씬·프리팹·재질부터, 로직 담당자는 GameManager·입력·Controller부터 보면 된다. 현재 이동은 저속 미리보기이며 정밀 물리·충돌·주차 판정은 Unity에 아직 구현하지 않았다.

자세한 협업·첫 실행은 [개발 인수인계](team-handoff.md), APK 빌드·검증은 [Unity 입력 안내](unity-input-probe.md)를 참고한다. Git에는 원본과 `.meta`를 공유하고 `Library`, `build`, 생성 AAR·APK는 제외한다.
