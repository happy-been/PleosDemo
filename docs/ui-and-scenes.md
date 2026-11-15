# Figma UI와 Scene 구성

기준: 2026-11-02, [PARKSIDE Figma 원본](https://www.figma.com/design/sMflH33Zf9FQlv7R3Qasmx?node-id=3-2).

## 현재 UI 실행

Unity 2022.3.16f1에서 `Assets/_Parkside/Scenes/MainMenu.unity`를 열고 Play한다. 저장된 uGUI Canvas의 Home / Practice / Setup / Maps / MapDetail 패널을 전환한다. 각 패널은 Figma의 3:2 / 3:69 / 3:138 / 5:56 / 5:126에 대응한다.

- 홈 → 연습 → 연습 선택(후진·평행·전진·좁은 공간 탈출, 도로 연습 탭) → 시작 시점 선택 → 연습 시작. 어떤 연습을 골라도 바로 시작 화면으로 가고, 제목·설명만 고른 연습으로 바뀐다.
- 연습은 모두 `ParkingLotPractice` 씬에서 한다. 이 씬은 운전 장치(Systems·카메라·HUD·PracticeCar)만 담고, 시작하면 `ParklingLot`(골목형 야외 주차장, 연습 칸 T1–T3·P1–P2)을 Additive로 불러와 활성 Scene으로 만든다(`AdditiveEnvironment`). `ParklingLot.unity` 파일은 고치지 않는다.
- 연습장은 입력이 들어오면 바로 출발한다. PC에서는 W 가속, Space 제동, 좌우 방향키로 조향한다. 차는 주차된 차·벽·기둥에 닿으면 멈춘다(`VehicleController.blockOnObstacles`, 이 씬에서만 켬).
- 오른쪽 아래 **마치기 → 나가기**로 홈에 돌아간다. 연습장은 이동 전에 연습을 정지한다(PC에서는 Escape도 된다).
- 맵 목록은 부산(큰 카드)·제주·파리 카드와 지역 필터·검색을 지원한다. 카드를 누르면 코스 상세가 그 맵 내용으로 바뀌고, **여행 시작**으로 맵 씬을 연다. 고른 1·3인칭 시점이 맵에도 적용된다.
- 네 주행 장면(연습장·부산·제주·파리)은 시연용 HUD `DriveHud`를 쓴다. 장면이 열릴 때 스스로 붙으므로 씬 파일은 고치지 않는다. 배치는 Figma "시연 HUD 시안 v1"(1600×900 기준, 시뮬레이터 2560×1440)을 따른다.
  - 왼쪽 위 1인칭·3인칭·탑뷰, 왼쪽 R·N·D 기어 슬라이더(멈춰 있을 때만 바뀜)와 속도, 오른쪽 위 지도(제주·파리는 경로 지도, 연습장·부산은 위에서 본 실시간 화면)와 맵 이름, 오른쪽 아래 마치기(확인 창).
  - 1인칭은 운전석 그림(지붕·A필러·대시보드) 위에 핸들이 조향값×450°로 돌고, 손은 ±25°까지만 따라간다(핸들 위 30%만 보여서 더 돌면 손이 화면 밖으로 나간다). 룸미러·양쪽 사이드미러는 작은 카메라 3대를 번갈아 그린다.
  - 실제 차가 P단·속도 0이 아니어서 입력이 막히면 가운데에 "주차 중에만 연습할(여행할) 수 있어요"를 띄운다. 다른 이유로 1.5초 넘게 끊기면 위쪽에 "차량 입력을 기다리는 중"을 띄운다. 마치기 버튼은 그대로 누를 수 있다.
  - 예전 디버그 패널(INPUT PROBE), 메뉴로 버튼, 제주·파리의 OnGUI 안내는 끈다. 제주·파리도 Pleos 입력(PC는 키보드)으로 운전한다.
  - 그림과 글꼴, 투싼은 `Resources/ParksideHud/HudAssets`에 있다. Figma에서 그림을 바꾸면 PNG를 같은 폴더에 덮어쓰고 `PARKSIDE → Update HUD assets`를 실행한다.
- 멀티플레이, 난이도별 과제, 주차 판정·기록, 음성 안내는 구현 전이다. 해당 동작은 준비 안내를 표시한다. 기록 점수나 실제 차량 입력 상태를 만들어 표시하지 않는다.

화면은 1440×1000 기준이며 CanvasScaler의 Expand 모드로 비율을 유지한다. 화면 비율이 다르면 주변 여백을 둔다. 텍스트·버튼·사진·아이콘은 개별 오브젝트이며 Inspector에서 편집할 수 있다. Figma 화면 전체 이미지를 UI로 사용하는 방식은 아니다.

## 파일 역할

| 파일 | 역할 |
|---|---|
| `Scripts/UI/ParksideMenu.cs` | 패널 전환, 카메라 선택, 맵 검색·필터, 준비 안내, 연습장 로드 |
| `Scripts/UI/PracticeMenuLink.cs` | 선택한 시점 적용, 연습을 멈추고 홈으로 복귀 |
| `Scripts/UI/Hud/DriveHud.cs` | 네 주행 장면의 시연용 HUD(카메라·기어·지도·마치기·1인칭 운전석·미러) |
| `Scripts/UI/Hud/DriveTarget.cs` | 장면별 차 연결: 연습장·부산(`VehicleController`), 제주·파리(`JejuDriveController`) |
| `Editor/HudAssetsSetup.cs` | HUD 그림을 스프라이트로 가져오고 `HudAssets`를 채움 |
| `Scripts/Core/AdditiveEnvironment.cs` | 연습 씬이 `ParklingLot`을 함께 불러오고 그 하늘·조명·카메라 보정을 쓰게 함 |
| `UI/Art` | 내려받은 Figma 원본 이미지·SVG와 SVG의 PNG 렌더 |
| `UI/Fonts` | 공식 Asta Sans 글꼴과 OFL 라이선스 |
| `UI/Generated` | 일반 UI용 모서리·명암 텍스처 |
| `Editor/ParksideMenuAuthoring.cs` | 명시적으로 실행하는 씬 저작 도구와 미리보기 렌더 |
| `Editor/Validation/MenuPlayVerification.cs` | 실제 Play에서 연습 선택·맵 선택·검색·필터 → 야외 주차장 → 홈 전환 검사 |
| `Editor/Validation/LotPracticeVerification.cs` | 야외 주차장에서 출발·직진·회전·주차된 차에 막힘·후진 탈출 검사와 화면 저장 |
| `Editor/Validation/DriveMapsVerification.cs` | 메뉴에서 연습장·부산·제주·파리를 차례로 열어 HUD·예전 UI 숨김·시점·기어를 확인하고 1인칭·3인칭·탑뷰 게임 화면을 캡처한 뒤 마치기로 돌아오는 검사(Unity 창이 보여야 함) |

레이아웃 원본은 `ParksideMenuAuthoring.cs`이고 저장된 Scene은 그 결과다. 런타임이나 APK 빌드가 UI를 재생성하지 않는다. `PARKSIDE → Author Figma menu scene`은 `MainMenu`와 `ParkingLotPractice`를 다시 만들고 빌드 목록을 메뉴가 여는 6개 씬(MainMenu, ParkingLotPractice, ParklingLot, BusanLandmarks, JejuCoastalDrive, ParisEiffelDrive)으로 맞춘다. Inspector에서 직접 고친 레이아웃은 이 명령을 실행하면 사라지므로, 바꿀 내용은 스크립트에 넣는다. 맵 카드·코스 상세의 글과 숫자는 `ParksideMenu.Maps`, 연습 문구는 `ParksideMenu.Practices`에 있다.

원본 에셋 다운로드 기록은 `tools/figma-ui-assets.json`, SVG 치수·사진 SHA-256 확인은 `tools/figma-ui-art-report.json`에 있다. 다운로드 URL은 런타임에서 사용하지 않는다. SVG는 경로·색상·비율을 변경하지 않고 4배 해상도의 PNG로 렌더했다. 글꼴 출처는 [42dot/Asta-Sans](https://github.com/42dot/Asta-Sans)다.

## 맵이 늘어날 때의 추천

현재는 맵이 하나이므로 `MainMenu`와 `ParkingPractice` 두 Scene을 Single 모드 비동기 로드로 연결했다. 모든 맵을 한 Scene에 넣는 구조는 도입하지 않았다. 공통 시스템을 지속시키는 Additive 로더는 아직 구현하지 않았다.

서울·알프스·도쿄처럼 환경 자체가 달라지는 맵이 추가되면 아래 구조를 권장한다.

```text
Bootstrap / Core          입력 브리지, 세션 상태, 공통 로딩·UI
MainMenu                  메뉴 화면 패널
Maps/ParkingGround        주차장 환경, 스폰, 주차 목표
Maps/Seoul                서울 환경
Maps/Interlaken           알프스 환경
```

공통 Scene은 유지하고 현재 맵만 Additive로 로드한다. 맵 변경 시 연습 정지 → 기존 맵 해제 → 새 맵 로드 → 활성 Scene·스폰·카메라 연결 → 로딩 화면 해제 순서로 처리한다. 큰 맵 두 개를 동시에 유지하지 않아 로딩 중 최고 메모리 사용량을 관리할 수 있다. 입력 공급자, EventSystem, AudioListener, 플레이어 차량이 중복 생성되지 않도록 소유 Scene을 명확히 정한다. 언로드만으로 참조 중인 텍스처 메모리가 모두 해제되는 것은 아니므로 공통 시스템의 맵 참조도 해제해야 한다.

같은 주차장 안의 후진·전진·평행 과제는 한 맵을 공유하고 `TrainingScenario` 같은 ScriptableObject로 시작 위치, 목표칸, 장애물 배치, 완료 조건을 바꾼다. 조명·건물·도로 배치가 크게 달라지는 환경은 별도 Scene으로 분리한다.

공식 문서: [Additive 로드](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.LoadSceneMode.Additive.html), [Scene 언로드](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SceneManagement.SceneManager.UnloadSceneAsync.html).

## 이번 변경의 검증

- Unity Windows 대상 컴파일 및 기존 입력·씬 검사 163개 항목 통과.
- Play에서 패널 이동, 검색, 대한민국 필터, 1·3인칭 선택, 연습장 진입, 복귀 전 정지, 홈 복귀 통과.
- 5개 화면과 1920×1080 홈 렌더를 확인했다. 결과는 로컬 `build/ui-preview/`에 있다.
- AVD 검사 스크립트의 기존 단위 테스트 7개 통과. 새 메뉴를 거쳐 연습장으로 이동하도록 스크립트를 조정했다.
- 이번 UI 작업에서는 Android APK 재빌드와 AVD에서의 재검증은 수행하지 않았다.
