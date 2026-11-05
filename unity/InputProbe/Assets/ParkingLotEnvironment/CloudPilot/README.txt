PARKSIDE CloudPilot — 재개용 변경분 / Unity 실행·렌더 미검증

실제 준비한 것
- OBJ22종/18,286삼각형: 3층·4층 창 구멍/창틀/창턱/건물 측후면,
  모따기 경계석, 실제 배수구 구멍/개별 그레이팅 바, 표지판과 기둥.
- 20×28m 배치 JSON, 통로6m, 면2.5×5m, 좌측7대 슬롯, 우측 비움.
  치수는 설계 가정이며 법령 검증/현장 실측 완료가 아니다.
- Standard 재질/기존 촬영 텍스처/기존 수목4개/박스 프로젝션 프로브/
  보행자 카메라/후보 주광을 조립하는 수동 Editor 도구.
- 현재 새 Standard 재질의 텍스처 연결은 diffuse만이다. normal/roughness 맵은
  연결하지 않은 임시 재질이며, 완성된 PBR 재질로 표현하지 않는다.
- 원본3,712개 해시 게이트, 씬+meta 백업, v35 토글, 보호 검사/재열기/복구 도구.
- Editor 파일2개의 오프라인 C# 컴파일 및 메시 검사. Unity 내 실행 검사는 아니다.
- Inspection~ PNG3개는 별도 3면 모듈의 소프트웨어 OpenGL 형상 검사다.
  전체20×28m, Unity 결과, 차량·수목·그림자·반사 검사 이미지가 아니다.

적용 순서 — 정상 Unity 환경에서 수동 실행
1. 전체 원본 저장소 루트에 변경분 unity 폴더를 합친다. 신규 CloudPilot만 추가한다.
   Unity2022.3.16f1/Built-in/Gamma, 원본 프로젝트 구조를 유지한다.
2. ParklingLot.unity를 열고 미저장 변경이 없는지 확인한다.
3. ParkingLot > Cloud pilot > Prepare isolated prefab (no scene changes).
   원본 해시 확인 후 PreviewScene에서 새 재질/프리팹만 만든다. 원본 씬은 그대로다.
4. Apply prepared pilot to saved v35.
   정확한 v35에서만 동작하며 저장소명-cloud-backups/시간 폴더에 씬+meta를 먼저 복사한다.
   원본16루트는 ParkingLot_V35_Preserved, 새 구역은 ParkingLot_CornerPilot_Root로 분리한다.
   이 씬 하나만 저장한다. 기존 builder 전체 재생성은 실행하지 않는다.
5. Add existing vehicles for comparison only.
   원본 프리팹7대를 읽기 전용 참조하고 가상 번호판을 별도 부착한다.
   저폴리 비교 차량이다. 새 고품질 차량 확보/근경 품질 합격으로 세지 않는다.
6. Show pilot / Show preserved v35로 비교한다. 토글은 미저장 변경이다.
   검증할 상태를 해당 씬에 저장하고 Verify protected files and saved reopen을 실행한다.
7. Capture stills plus forward10m side3m (offline).
   프로브 완료 후 1920×1080 FOV37/44 정지2장+전진10m60장+좌우3m60장=122장.
   CAPTURE_COMPLETE가 없는 실행은 완료가 아니다. 프레임 속도/Android 성능 검사가 아니다.
   Cancel offline capture로 중단. Play 진입/오류/리로드 때 자원을 해제한다.

복구
- 미저장 변경이 없을 때 Restore original v35 from backup.
  원본 씬 SHA256 확인 후 해당 씬만 복원하고 보호파일을 재검사한다.
- 도구가 실패하면 Unity를 닫고 외부 백업의 ParklingLot.unity를 원래 경로에 복사한다.
  meta는 그대로 유지한다. 훼손된 경우 함께 보관한 원본 meta를 복원한다.
- ZIP에는 원본 씬 교체 파일이 없다. 원본 전체 ZIP은 비공개 Drive에 별도 보존돼 있다.

미완료 / 시각 품질 게이트
- Unity 임포트·프리팹 생성·씬 적용·저장 재열기·프로브·정지/이동 캡처·Android 성능 미실행.
- 근경 고품질 현대차 미확보. 기존 차량 실루엣/곡면·유리·도장·번호판 배치 검사 필요.
- 건물 유리는 초기 불투명 비교 재질이다. 최종 유리 표현이 아니다.
- 태양43도/초기 강도/색은 미검증 시작값이다. 하늘 채도·노출·바닥 톤은 실제 화면으로 조정한다.
  기존 RenderSettings를 자동 변경하지 않는다. 커스텀 셰이더/고정 그림자 쿠키는 없다.
- 수목/차량 실제 그림자, 가림/시차/접지, 앞/옆/뒷면과 틈새를 검증해야 한다.
- 눈높이1.65m/FOV37~44도/차량에서3~4m를 실제 구도에서 확정한다.
  JSON44도/18도 조망은 시작 후보다. AI 이미지 번호판 오류는 복제하지 않는다.
- '전보다 덜 CG'는 합격이 아니다. 오프라인 영상은 실시간 성능 증거가 아니다.
- 실제 장애: Unix 소켓 EPERM, Unity CDN 정책 차단, 라이선스 없음.
  정상 IPC와 공식 인증/라이선스 통신 지원이 먼저 필요하다. 최신 공식 CLI에도
  Personal 활성화 경로가 있으며 2022.3.16f1과의 실제 호환성은 여기서 미검증이다.
