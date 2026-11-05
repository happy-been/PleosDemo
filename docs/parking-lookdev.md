# 작은 아파트 주차장 시각 검증

`unity/InputProbe/Assets/_Parkside/Scenes/ParkingLookdev.unity`를 Unity 2022.3.16f1에서 열고 Play한다. 메뉴·기존 연습장과 분리된 새 Scene이다. 앱의 시작 Scene과 Android 빌드 설정은 바꾸지 않는다.

## 보기와 조작

- `1`: 운전자 높이. W/S 전진·후진, 좌우 방향키 또는 A/D로 방향 변경, Space로 정지한다.
- `2`: 주차면·화단을 보는 시점.
- `3`: 낮은 높이에서 아스팔트·도색을 보는 시점.
- `4`: 주차장 전체를 보는 시점.
- `R`: 현재 시점의 시작 위치로 복귀. `Tab`: 화면 안내 표시·숨김.

이동은 그래픽 검토용이다. 차량 충돌·그랜저 물리·Pleos SDK를 연결한 연습 모드는 아직 이 Scene에 넣지 않았다. 실내 사진도 아직 포함하지 않는다.

## 구성

36×32m 구간 안에 8개 주차면, 스캔한 아스팔트와 광학 재질, 닳은 도색·번호·균열·얼룩, 모서리가 깎인 연석, 고무 휠 스토퍼, 배수구·벤치·볼라드, 나무·관목과 커버가 씌워진 주차 차량을 배치했다. 주변 아파트는 간단한 3D 외벽·창틀·창유리·창턱으로 구성했다.

배경은 베이크한 간접광·접촉 음영과 실시간 태양 그림자를 사용한다. 하늘과 반사에는 HDR 환경 이미지를 사용한다. 재질과 조명은 PC에서 화면 품질을 확인하는 설정이며, Pleos에서의 프레임·메모리는 별도 측정이 필요하다.

원본과 파생 에셋의 출처는 `Assets/_Parkside/Lookdev/CREDITS.md`와 `AssetSources.json`에 있다. 생성 도구는 런타임이나 일반 APK 빌드에서 자동 실행되지 않는다.

## 다시 만드는 도구

1. `tools/fetch-parking-lookdev.ps1`: 공개 CC0 원본과 PBR 텍스처 다운로드.
2. `tools/convert-parking-models.py`: Blender 배치로 FBX를 경량화.
3. `tools/prepare-parking-lookdev.py`: 재질 채널·나뭇잎 투명도·도로 마스크 준비.
4. `PARKSIDE → Lookdev → Create courtyard scene`: 저장된 새 Scene 작성. 이 명령은 해당 Scene의 직접 편집 내용을 초기 배치로 다시 작성하므로 초기 생성용으로만 사용한다.

이미 작성된 Scene은 `PARKSIDE → Lookdev → Open courtyard scene`으로 연다. Inspector에서 지형 배치·재질·조명·Saved viewpoints를 직접 수정할 수 있다. 로컬 렌더와 검증 기록은 `build/parking-lookdev/`에 저장한다.

## 확인한 결과

- Unity Windows 대상 컴파일, 재질·셰이더·참조 검사 통과.
- 조명 베이크와 1개 라이트맵 생성 확인.
- 실제 Play에서 4개 시점 전환 및 1920×1080 렌더 저장 통과.
- 현재 활성 렌더러 669개, 약 123만 삼각형이다. 나무의 형태를 유지하기 위한 PC 시안 설정이며 모바일용 최적화가 끝난 상태는 아니다.
- 검증 로그는 `build/parking-lookdev/unity-verification.log`, 장면 통계는 `scene-report.txt`에 있다.
- 현재 외벽·조경·차량은 첫 시안이다. 실사 완성도와 Pleos에서의 프레임·메모리 검증은 추가 작업 범위다.
