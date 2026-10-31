# 기존 Kotlin 2D 프로토타입

[현재 Unity 개발 안내](../README.md) · [팀원 인수인계](team-handoff.md)

AVD는 **P단 / 속도 0**을 유지하고, PC의 Virtual Console에서 ACCEL·BRAKE·조향을 조작해 앱 안의 자동차를 움직이는 2D 프로토타입입니다.

## 실행

1. Android Studio에서 프로젝트를 열고 Pleos Connect v2.1.0 AVD를 **Cold Boot**로 실행합니다.
2. 앱을 Run하고 속도 조회 권한을 허용합니다.
3. 프로젝트 폴더의 PowerShell에서 아래 명령을 실행합니다. Python 3.9 이상을 사용하며 추가 패키지는 필요 없습니다.

```powershell
& "D:\Android\Sdk\platform-tools\adb.exe" forward tcp:8088 tcp:8088
py -3 .\tools\parking_console.py
```

`adb`가 PATH에 있으면 첫 줄은 `adb forward tcp:8088 tcp:8088`로 입력해도 됩니다.
SDK 경로가 다른 PC에서는 설치한 경로로 바꿉니다.
PowerShell 스크립트를 사용할 수 있는 환경에서는 `./tools/start-parking-console.ps1`로 두 단계를 한 번에 실행할 수도 있습니다.

4. PC 브라우저에서 **<http://127.0.0.1:8090/>**을 엽니다. 실행한 터미널을 유지합니다. 종료는 Ctrl+C입니다.
5. 앱에서 **AVD 입력 → 시작**을 누릅니다.
6. PC 콘솔의 **ACCEL**을 누르는 동안 가속하고, 놓으면 감속합니다. **BRAKE**는 정지, 핸들은 조향입니다.
7. 전진/후진은 앱의 **가상 D / 가상 R**을 정지 상태에서 선택합니다. PC 콘솔의 기어는 **P**를 유지합니다.

**이번 프로토타입은 8090 주소를 사용합니다.** 8088 기본 콘솔은 P단에서 ACCEL 입력을 0으로 처리합니다.
8090은 같은 콘솔 화면을 전달하면서 이 가속 조건만 바꿉니다. AVD 설치 파일은 수정하지 않습니다.
두 콘솔을 동시에 조작하면 값이 충돌할 수 있으므로 8090 창 하나로 조작합니다.

## 조작

- **AVD 입력**: 조향/브레이크 상태/기어/속도는 기존 Pleos SDK·차량 프로퍼티로 읽습니다. 가속페달과 브레이크 비율은 AVD 안의 콘솔 서버에서 읽습니다.
- 앱 오른쪽 **가속페달**과 아래 **가속 입력: AVD 페달**에 0~100%가 표시됩니다. AVD 입력 모드에서는 앱 안의 가속 버튼이 잠깁니다.
- **화면 입력**: 콘솔 없이 앱의 슬라이더·가속·브레이크 버튼으로 시험하는 모드입니다.
- 초록색 구역 안에 차체 전체를 넣고 평행하게 정지한 상태를 1초 유지하면 성공합니다.
- 충돌하면 **초기화**를 눌러 다시 시작합니다.
- 페달 값 갱신이 0.6초를 넘게 멈추거나, 콘솔이 닫히거나, AVD의 P단/정지 조건이 풀리거나, 앱이 백그라운드로 가면 연습이 멈춥니다. 다시 시작하려면 **시작**을 누릅니다.

## 코드 구조

| 파일 | 역할 |
| --- | --- |
| `tools/parking_console.py` | PC용 8090 콘솔. 8088 AVD 서버를 전달하고 P단 ACCEL 조건을 변경 |
| `vehicle-bridge/.../AvdConsolePedals.kt` | 공용 AVD 로컬 페달 조회 |
| `vehicle-bridge/.../AvdPedals.kt` | 페달 비율, 유효성·신선도 검사 |
| `vehicle-bridge/.../PleosVehicleInput.kt` | 공용 조향/브레이크 SDK 구독 및 AAOS 상태 조회 |
| `vehicle-bridge/.../VehicleInput.kt` | 입력 형식과 P단/정차 조건 |
| `ParkingSimulation.kt` | 저속 이동 계산, 차체 충돌, 성공 판정 |
| `ParkingView.kt` | 2D 주차장과 자동차 그리기 |
| `MainActivity.kt` | 입력 모드, 시작/초기화, 입력과 시뮬레이션 연결 |

페달 HTTP 연결 권한과 로컬 평문 통신 설정은 공용 라이브러리의 `vehicle-bridge/src/debug/`에 있습니다.
AVD 내부 앱은 `127.0.0.1:8088/vehicle/state`를 읽으므로 `adb reverse`는 필요 없습니다.
SDK·AVD·Python 경로와 `local.properties`는 각 PC에서 설정합니다.

## 개발 / 검증

```text
gradlew.bat :vehicle-bridge:testDebugUnitTest :app:testDebugUnitTest :app:assembleDebug
```

현재 브랜치: `codex/parking-prototype`.
콘솔의 가속 처리 코드가 AVD 업데이트로 달라지면 8090 도구는 오류를 표시합니다.
현재 AVD에서 콘솔 조향 +25는 SDK에서 -25로 반환되며, 앱은 SDK 반환값 기준으로 표시/계산합니다.

### AVD에서 확인한 결과

- 리팩토링 후 단위 테스트 34개(공용 입력 21개·2D 앱 13개) 통과, 기존 2D APK 빌드 성공.
- P단 / AVD 속도 0에서 페달 37%를 읽고 가상 차량이 3.5 km/h로 이동하는 화면 확인.
- 브레이크 50% 입력 시 가상 속도 0으로 정지 확인.
- PC 콘솔을 닫으면 연습 정지 및 시작 버튼 비활성화 확인.
