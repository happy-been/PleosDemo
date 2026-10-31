# PARKSIDE Unity 입력 연결 — 빌드·AVD 검증

전체 구조와 팀원 작업 순서는 [개발 인수인계](team-handoff.md)를 참고한다.

**현재 진입점**

Unity 프로젝트는 `unity/InputProbe`, 씬은 `Assets/_Parkside/Scenes/ParkingPractice.unity`다. `PracticeCar.prefab`과 맵·조명·카메라가 씬에 저장되어 Play 전에 보인다. 런타임의 맵·차량 생성 코드와 기존 단일 `PleosInputProbe.cs`는 제거했다.

`Parkside.Editor.ParksideAndroidBuild.BuildAndroid`는 Build Settings의 저장된 씬을 패키징한다. 씬을 생성·재저장하지 않는다. 빌드 전후 씬 SHA-256이 동일한 것도 확인한다. 소스가 사용자 Editor에서 열려 있는 동안의 검증 빌드는 동일 소스·자산을 복사한 격리 프로젝트에서 실행했다.

**도구 조합**

| 항목 | 설정 |
|---|---|
| Unity | 2022.3.16f1 / Built-in / GLES3 / 목표 30fps |
| APK | x86_64 / IL2CPP / min SDK 28 / compile·target 34 |
| Unity Android 도구 | 내장 OpenJDK 11 / NDK r23b / Gradle 7.2 / AGP 7.1.2 |
| SDK Build Tools | 34.0.0 |
| 공용 Kotlin | Gradle 9.6.0 / AGP 9.4.1 / compile SDK 37 |
| Vehicle SDK | 2.3.2 |
| AVD | Pleos Connect v2.1.0 / Android 14 / x86_64 |

PC 편집·키보드 미리보기는 Windows 대상으로 열면 된다. Android 모듈은 APK 빌드 시점에만 필요하다. 빌드 클래스는 Android 확장을 직접 컴파일 참조하지 않으므로 모듈 없는 Editor에서도 컴파일된다. 프로젝트를 여는 것만으로 Android로 전환하는 코드는 없다.

Unity의 Android 에뮬레이터 공식 지원과 이번 조합의 실제 실행 결과는 구분한다. Unity 6.3·URP·실기기 ABI·최종 월드 성능까지 검증한 결과는 아니다. [Unity Android 호환성 안내](https://docs.unity3d.com/2022.3/Documentation/Manual/android-requirements-and-compatibility.html)

**PC 검사**

Unity에서 `ParkingPractice.unity`를 열고 `PARKSIDE → Validate scene and input`을 실행한다. Canvas·버튼 연결·입력 공급자·JSON·차량 동작을 확인한다. 씬 참조 검사는 `PracticeSceneValidator`, 입력·차량 회귀 검사는 `PracticeProjectValidation`에 분리되어 있다. 연습 컴포넌트가 없는 메뉴 씬도 빌드에 추가할 수 있다. Play는 W=가속, Space=브레이크, 좌우 방향키=조향이다. APK는 Pleos 입력을 사용한다.

Kotlin 검사는 다음과 같다.

```powershell
.\gradlew.bat :vehicle-bridge:testDebugUnitTest :app:testDebugUnitTest :app:assembleDebug
```

공용 입력 라이브러리 21개·2D 앱 13개로 총 34개 테스트가 있다. 2D 계산에 의존하는 테스트는 app에 두고, 공용 입력 테스트는 라이브러리에 둔다.

**APK 빌드**

1. Android Build Support·SDK/NDK·OpenJDK가 있는 Unity를 준비하고 Hub 라이선스를 활성화한다.
2. Android SDK 플랫폼 34·37, Build Tools 34.0.0, Platform Tools·라이선스를 준비한다.
3. 루트 Gradle용 JDK 25를 준비한다. 이 PC는 Android Studio JBR 25.0.3을 사용했다. SDK·JDK·Unity 경로는 스크립트가 탐색한다. 수동 Gradle 실행에는 `ANDROID_HOME`·`JAVA_HOME`을 설정한다.
4. 같은 프로젝트가 Editor에서 열려 있으면 저장하고 닫은 뒤 저장소 루트에서 실행한다.

```powershell
& .\tools\build-unity-input-probe.ps1
```

검증 PC의 기본 Editor 경로는 아래와 같다. 다른 PC는 `-UnityEditor` 인수로 자신의 경로를 지정한다.

```text
C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor\Unity.exe
```

`android-environment.ps1`은 SDK를 명시 인수 → `ANDROID_HOME`/`ANDROID_SDK_ROOT` → `local.properties` → 기본 설치 위치 순서로 찾는다. JDK는 명시 인수·환경 변수·Android Studio JBR·PATH에서 찾는다. Unity는 명시 인수·`UNITY_EDITOR_PATH`·프로젝트 버전의 Hub 기본 설치 위치 순서다. 자동 탐색이 맞지 않으면 빌드 스크립트의 `-UnityEditor`, `-AndroidSdk`, `-JavaHome`으로 지정한다. 특정 개발자의 D 드라이브 경로는 필수 설정이 아니다. 실행 검증은 PowerShell 7에서 수행했으며 Windows PowerShell 5.1은 구문 검사를 수행했다.

`2022.3.16f1-x86_64` 설치본에는 Android 모듈이 없어 PC 편집에 사용할 수 있다. APK는 모듈이 있는 위 설치본을 사용한다.

스크립트는 Kotlin AAR를 생성·복사하고, Unity Android 프로젝트를 내보내고, 내장 JDK·Gradle로 APK를 패키징한다. Java 11 호환 명령줄 도구와 API 34를 묶은 SDK 작업 사본은 `build/unity-probe/android-sdk`에 준비한다. 설치된 Editor 자체는 수정하지 않는다.

출력 APK는 `build/unity-probe/parkside-input-probe.apk`다. `-SkipTests`는 이미 검사한 소스를 재패키징할 때만 사용한다. GUI의 `PARKSIDE → Build Android APK`는 Android Gradle 내보내기 단계이며, 최종 APK까지는 PowerShell 스크립트를 사용한다.

**AVD 자동 검사**

Pleos AVD를 Cold Boot하고 P단·속도 0에서 실행한다.

```powershell
py -3 .\tools\validate-unity-input-probe.py
```

기본 기기는 `emulator-5554`이고 `--serial`, `--adb`, `--apk`로 지정할 수 있다. 실제 에뮬레이터인지 검사한 뒤 APK를 설치·실행한다. 자체 콘솔 세션을 열어 아래 값들을 Virtual Console API로 전달하고 실제 Unity 로그와 비교한다. JSON을 Unity에 직접 주입하지 않는다.

검사는 Kotlin 원본의 `allowed`와 C# 검사 후 `inputReady`를 함께 확인한다. 정상 입력은 `inputReady=True`, 연결 종료는 `False`여야 통과한다. 이전 APK에 이 로그가 없으면 APK를 다시 빌드한다. ADB는 SDK 환경 변수·`local.properties`·PATH에서 자동 탐색한다. 검사 도구 자체의 회귀 검사는 `py -3 -m unittest discover -s tools/tests -v`로 실행한다.

| 검사 | 확인하는 값 |
|---|---|
| 연결 | P=4, 실제 속도 0, allowed=true |
| 악셀 37% | acceleratorPercent=37, throttle=0.37 |
| 콘솔 조향 +25도 | SDK -25 → Unity steeringDegrees=+25 |
| 브레이크 50% | brakePercent=50, brake=0.5 |
| 입력 해제 | 조향·가속·브레이크 0 |
| 콘솔 종료 | allowed=false, throttle=0, brake=1 |

다른 콘솔 세션이 살아 있으면 그 세션을 종료하지 않고 연결 종료 검사만 건너뛴다. 끝나면 테스트 입력을 0으로 돌리고 자신이 연 세션만 닫는다. 자동 검사는 수신 값·차단을 확인하며 가상 차량의 주행 버튼을 누르지는 않는다. Controller 이동은 Editor 회귀 검사로 별도 확인한다.

결과 파일은 `build/unity-probe/validation.json`, `unity-device.log`, `connected-steering-accelerator.png`, `connected-brake.png`, `android-meminfo.txt`다. `runtimeErrors`도 확인한다. 빌드 폴더는 Git에서 제외한다.

**수동 콘솔**

```powershell
& .\tools\start-parking-console.ps1
```

터미널을 유지하고 PC 브라우저의 `http://127.0.0.1:8090/`을 연다. AVD에서 `Pleos Unity Input Probe` → `Start preview`를 누른다. 실제 기어는 P, 가상 D/R과 시점은 Unity 버튼으로 바꾼다. 자동 검사 종료 후 앱이 대기 상태이면 브라우저 콘솔을 열어 세션을 다시 활성화한다.

**입력 경계와 제한**

Kotlin의 공용 원본은 `vehicle-bridge/src/main/java/com/example/pleosdemo`에 있다. `app`과 Unity AAR가 같은 코드를 사용한다. 조향·제동 상태·P·속도는 SDK·AAOS, 연속 페달 비율은 AVD 전용 HTTP 어댑터다. 실제 차량 페달 지원과 구분한다.

Kotlin과 C# 모두 잘못된 입력을 차단한다. 실제 P·정지, 데이터 신선도, 유한한 숫자·범위를 검사한다. Unity의 이동은 Transform 기반 미리보기이며 1%를 넘는 제동 입력에서 즉시 정지한다. 정밀 차량 물리·충돌·주차 판정·Figma 화면·장시간 안정성은 다음 개발 범위다.

리팩토링 이전 최소 화면의 짧은 측정은 약 26~30fps, TOTAL PSS 147,123 KiB(약 143.7 MiB), RSS 221,388 KiB였다. 이 기록은 최종 월드의 성능·자원 제한 충족을 의미하지 않는다. 리팩토링 후 측정은 최신 `android-meminfo.txt`와 Unity 로그를 따른다.

**2026-10-29 리팩토링 회귀 결과**

- 공용 Kotlin 21개·기존 2D 앱 13개 단위 테스트 통과. 두 모듈 Lint와 기존 2D APK 빌드도 통과했다.
- Android 모듈 없는 Windows Editor에서 씬 참조와 JSON·입력·차량 동작 검사를 실행했다. 검사 자체도 별도 Preview Scene을 사용한다.
- 정적 씬으로 x86_64 Android APK 빌드·설치·실행을 완료했고, 빌드 전후 씬 SHA-256이 같았다.
- AVD 입력 검사 6개 모두 통과했고 수집된 런타임 오류는 0개였다. 캡처에서 주차장·차량·조향·가속 비율 표시를 확인했다.
- 새 메모리 표본은 TOTAL PSS 135,108 KiB(약 131.9 MiB), RSS 249,040 KiB였다. 단일 표본이므로 리팩토링의 성능 개선으로 단정하지 않는다.
- 검사 기록: `build/refactor-validation/windows-final-validation.log`, `author-scene.log`, `android-export.log`, `gradle-apk.log`, 최신 `build/unity-probe/validation.json`.

**2026-10-29 Canvas·입력 공급자 분리 결과**

- OnGUI와 런타임 UI 생성 코드를 제거하고, Text·Image·Button·On Click 연결을 ParkingPractice 씬의 PracticeCanvas에 저장했다. uGUI 1.0.0은 Editor에 포함된 패키지를 사용한다.
- CanvasScaler는 1440×900 기준 Expand로 설정해 PC와 AVD의 서로 다른 비율에서 내용을 유지한다. 기존 Environment의 배치는 보존했다.
- Desktop Source=KeyboardInputSource, Android Source=PleosInputSource이며, 두 구현은 같은 VehicleInputSource / InputSnapshot 계약을 사용한다. Controller는 바뀌지 않았다.
- Windows Editor의 정적 검사 43개가 통과했다. W·Space·좌우 방향키, 제동 우선, 공급자 연결 종료, UI 참조와 버튼 연결을 포함한다.
- 실제 Play 모드에서 Canvas 버튼 5개·재연결 일시정지·실행 중 공급자 참조 교체를 확인했다. Play 검사와 Android 빌드 전후 씬 파일은 바뀌지 않았다.
- Canvas APK를 AVD에 설치해 입력 검사 6개를 통과했고 수집된 런타임 오류는 0개였다. 캡처에서 UI 글자·버튼·조향·페달 표시를 확인했다.

**2026-10-30 공유 전 최종 정리 결과**

- `VehicleInputSource`에 비활성화·삭제 시 연결 정리를 추가했다. 공급자 교체·해제·복구 후에는 연습이 자동 재개되지 않는다.
- 씬 연결 검사와 입력·차량 회귀 검사를 분리했다. 메뉴 씬에는 연습 차량·HUD를 요구하지 않으며, 맵 루트 이름을 바꿔도 빌드할 수 있다.
- 빌드·콘솔 도구의 SDK·JDK·Unity 경로 탐색을 공통화했다. AVD 검사는 Kotlin의 `allowed`와 Unity의 `inputReady`를 함께 확인한다.
- Windows의 Git 제외 규칙 때문에 `Editor/Build` 소스가 빠질 수 있던 문제를 수정했다. 생성 결과 제외 규칙을 루트 Gradle 모듈에 한정했고, Unity 소스·자산·`.meta`가 공유 대상에 포함되는지 검사했다.
- `local.properties`·생성 AAR·Unity 캐시 없이 공유 소스만 복사한 프로젝트에서 Kotlin 테스트 34개와 Unity Android APK 빌드를 통과했다. 공백이 있는 프로젝트 경로에서도 빌드했다.
- Android 모듈 없는 Windows Editor에서 44개 검사를 통과했다. 실제 Play 모드에서 Canvas 버튼 5개, 재연결, 실행 중 공급자 교체, 공급자 비활성화·삭제·복구를 확인했다. 검사 후 저장된 씬 SHA-256은 원본과 같았다.
- 새 APK의 Pleos AVD 입력 검사 6개가 모두 통과했고 런타임 오류는 0개였다. 정상 입력의 `inputReady=True`와 콘솔 종료 후 `False`를 확인했다. 자동 검사는 연습 주행 버튼을 누르지 않으며, 이동 동작은 Editor 검사로 확인했다.
- Python 검사 도구 테스트 7개, PowerShell 7 실행·Windows PowerShell 5.1 구문 검사, 문서 링크·Unity 자산 참조 파일 누락 검사를 통과했다. README는 Unity 시작 순서 중심으로 정리하고 기존 2D 앱 안내는 `legacy-2d.md`로 옮겼다.
- AVD 콘솔은 세션 직후 DDS가 준비되기 전 503을 반환할 수 있어, 검사 도구가 준비 완료를 기다리고 해당 응답만 제한적으로 재시도하도록 보완했다.
- 기록: build/canvas-validation/author-canvas.log, canvas-play-source-swap.log, android-export.log, gradle-apk.log 및 build/unity-probe/validation.json.

**2026-10-31 조향 방향 수정**

- UnityInputState에서 SDK 조향 부호를 한 번 반전한다. 콘솔 +25 → SDK -25 → Unity +25로 전달하며, 키보드 입력과 SDK 원본은 유지한다.
- 좌우 부호 변환 단위 검사를 포함한 Kotlin 테스트 34개, APK 빌드와 새 APK의 AVD 입력 검사 6개를 통과했다. 런타임 오류는 0개였다.
