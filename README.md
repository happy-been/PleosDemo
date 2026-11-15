# 파킹트래블

**잠자는바보들**이 만든 차량용 운전 연습 앱입니다. 주차한 차 안에서 핸들과 페달로 가상 주차장에서 연습하거나 부산·제주·파리의 도로를 달릴 수 있습니다.

## 심사용 APK

[GitHub Release에서 APK 내려받기](https://github.com/happy-been/PleosDemo/releases/tag/parking-travel-2026). 첨부 파일 이름은 **잠자는바보들_파킹트래블_release.apk**입니다.

## 테스트 환경

| 항목 | 내용 |
|---|---|
| 에뮬레이터 | Pleos Connect v2.1.0 · Android 14 (API 34) · x86_64 |
| 기본 화면 | 가로 2560 × 세로 1440픽셀 · 16:9 · 160 dpi |
| 개발 환경 | Windows 11 · Unity 2022.3.16f1 · Pleos Vehicle SDK 2.3.2 |
| APK | x86_64 · IL2CPP · OpenGL ES 3 · Android 대상 API 34 |

## 설치와 실행

1. PC에서 Pleos Connect 에뮬레이터를 켜고 차량 상태를 **P단, 속도 0**으로 맞춥니다.
2. APK를 내려받은 폴더에서 아래 명령을 실행합니다. Android SDK의 `platform-tools`가 PATH에 있어야 합니다.

   ```powershell
   adb devices
   adb install -r "잠자는바보들_파킹트래블_release.apk"
   adb shell am start -n com.example.pleosdemo.unityprobe/com.unity3d.player.UnityPlayerActivity
   ```

3. 홈에서 **연습 → 연습 항목 선택 → 연습 시작**을 누르면 야외 주차장에 들어갑니다. **주행 → 부산·제주·파리 중 선택 → 여행 시작**을 누르면 해당 도시를 달릴 수 있습니다.
4. 에뮬레이터에서 핸들과 페달을 조작하려면 저장소 루트에서 `.\tools\start-parking-console.ps1`을 실행하고 PC 브라우저로 `http://127.0.0.1:8090/`을 엽니다. PC 콘솔에는 Python 3.9 이상이 필요합니다.

## PC에서 시작

1. Unity Hub의 **Add → Add project from disk**에서 `unity/InputProbe` 폴더를 등록한다.
2. Unity **2022.3.16f1**, PC 작업은 **Windows 64-bit** 대상으로 연다.
3. `Assets/_Parkside/Scenes/MainMenu.unity`를 열고 Play한다.
4. **연습 → 원하는 연습의 선택 → 연습 시작** 순서로 진행하면 입력이 들어오는 대로 바로 출발한다. PC에서는 **W 가속 / Space 제동 / ←·→ 조향**으로 확인한다. 연습은 모두 야외 주차장(`ParklingLot`)에서 하며, 주행 탭에서는 부산·제주·파리 맵을 고르고 **여행 시작**으로 연다.
5. `PARKSIDE → Validate scene and input`으로 씬 연결·입력·차량 동작을 검사한다.

맵은 씬, 차량은 `PracticeCar.prefab`, UI는 `PracticeCanvas`에서 편집한다. PC에서는 키보드, Android APK에서는 Pleos 공급자를 사용한다. `Systems/InputReceiver`의 Desktop Source / Android Source 참조로 공급자를 교체한다.

Figma 기반 메뉴 5개 화면은 `PARKSIDE → Author Figma menu scene`이 `MainMenu`에 생성한다. 연습 5종 선택, 맵 검색·필터, 시점 선택, 야외 주차장 연습 진입, 부산·제주·파리 주행 맵 진입이 연결되어 있다. 네 주행 장면은 같은 시연용 HUD(`DriveHud`)를 쓴다. 왼쪽 위 1인칭·3인칭·탑뷰, 왼쪽 R·N·D 기어 슬라이더와 속도, 오른쪽 위 지도와 맵 이름, 오른쪽 아래 **마치기**(확인 후 홈으로)이고, 1인칭에서는 운전석 그림·조향에 맞춰 도는 핸들과 손·실시간 룸미러와 사이드미러가 보인다. 차량은 모두 투싼이다. 멀티·난이도별 코스·점수 판정은 준비 중이다. [UI 실행과 Scene 구성 안내](docs/ui-and-scenes.md)를 참고한다.

실사 재질과 조명을 확인하는 별도 Scene은 `Assets/_Parkside/Scenes/ParkingLookdev.unity`다. Play 후 1~4로 시점을 바꾸며 볼 수 있다. [작은 주차장 시각 검증 안내](docs/parking-lookdev.md)에 조작과 에셋 출처를 정리했다.

제주 주행 시안은 별도 Scene `Assets/_Parkside/Scenes/JejuCoastalDrive.unity`다. 예래·중문 해안, 월평·강정 연결도로, 1100고지 부근을 실제 도로·고도로 구성했으며, 플레이어 주변에 각자 목적지를 가진 차량이 다닌다. [제주 Scene 실행·조작 안내](docs/jeju-drive.md)를 참고한다.
- [파리 에펠탑 주행 씬](docs/paris-drive.md)
- [부산 명소 순환 주행 씬](docs/busan-drive.md): `Assets/BusanEnvironment/Landmarks/BusanLandmarks.unity`. 해운대에서 출발해 광안대교 또는 광안리 해안을 지나 한 바퀴 도는 코스이며, 움직이는 차와 신호가 있다.

## 소스에서 APK 빌드

Windows PowerShell 7 기준이다. APK 빌드에는 Unity Android Build Support·SDK/NDK/OpenJDK, Android SDK 플랫폼 34·37 및 Build Tools 34.0.0, 루트 Gradle용 JDK 25가 필요하다. 자동 검사와 PC 콘솔에는 Python 3.9 이상을 사용한다. 같은 프로젝트를 Unity Editor에서 닫고 저장소 루트에서 실행한다.

```powershell
.\tools\build-unity-input-probe.ps1
py -3 .\tools\validate-unity-input-probe.py
```

개발용 APK는 `build/unity-probe/parkside-input-probe.apk`에 생성된다. 제출용 APK를 다시 만들 때는 본인의 서명 키 경로와 별칭을 `PARKSIDE_KEYSTORE`, `PARKSIDE_KEY_ALIAS`에, 키 저장소와 키의 비밀번호를 `PARKSIDE_STORE_PASSWORD`, `PARKSIDE_KEY_PASSWORD` 환경 변수에 설정한다. 그런 다음 `.\tools\build-unity-input-probe.ps1 -Release`를 실행하면 `apk/잠자는바보들_파킹트래블_release.apk`가 생성된다.

도구는 Unity 기본 설치 경로, `ANDROID_HOME` 또는 `local.properties`의 `sdk.dir`, Android Studio JBR 등을 탐색한다. 다른 경로는 `-UnityEditor`, `-AndroidSdk`, `-JavaHome`으로 지정한다. 루트 Kotlin 빌드의 JDK 25와 Unity APK 패키징의 내장 JDK 11을 구분한다. 자세한 설치·경로·실행 순서는 [빌드·AVD 검사 안내](docs/unity-input-probe.md)를 따른다.

## 구조와 팀원 안내

| 위치 | 역할 |
|---|---|
| `unity/InputProbe/` | 현재 Unity 앱: 씬·차량·카메라·게임 진행·UI |
| `vehicle-bridge/` | Pleos 입력 수신·검사·Unity JNI 연결 Kotlin 라이브러리 |
| `app/` | 기존 Kotlin 2D 검증 앱 |
| `tools/` | APK 빌드·PC 콘솔·AVD 검사 |
| `docs/` | 파일 역할·작업 순서·검사 기록 |

- [기능별 파일 요약](docs/file-guide.md): 파일별 역할과 읽는 순서
- [팀원 인수인계](docs/team-handoff.md): 입력 흐름·Unity 편집·협업 방식
- [빌드·AVD 검사 안내](docs/unity-input-probe.md): 도구 조합·명령·검사 범위
- [기존 2D 앱 안내](docs/legacy-2d.md): 이전 프로토타입 실행·구현

Git에는 원본과 Unity `.meta`를 함께 공유한다. `Library`, 로컬 설정, 생성 AAR·APK, 빌드 결과는 제외한다. 입력 라이브러리는 빌드 스크립트가 다시 만든다. 연속 페달 비율은 현재 AVD 전용 HTTP 입력이며, 실제 차량 지원은 별도 검증이 필요하다.
