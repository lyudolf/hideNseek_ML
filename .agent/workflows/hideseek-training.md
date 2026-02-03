---
description: Hide N Seek ML-Agents 훈련 워크플로우
---

# Hide N Seek 프로젝트 규칙

## 프로젝트 구조
- Unity 프로젝트 경로: `c:\Users\Lyu\.gemini\antigravity\scratch\hideNSeek\hideNseek`
- 스크립트: `Assets/Scripts/`
- ML-Agents 설정: `Assets/Config/hideseek.yaml`
- 훈련 결과: `results/`
- Python 환경: `venv/`

## 훈련 시작하기

// turbo
1. 터미널에서 venv 활성화
```powershell
cd c:\Users\Lyu\.gemini\antigravity\scratch\hideNSeek\hideNseek
.\venv\Scripts\activate
```

// turbo
2. 훈련 시작 (새 run-id로)
```powershell
mlagents-learn Assets/Config/hideseek.yaml --run-id=<run_name>
```

3. Unity에서 Play 버튼 누르기 (20초 내)

4. 훈련 중단: 터미널에서 `Ctrl+C`

## 훈련된 모델 테스트

1. Unity Stop
2. Agent의 Behavior Parameters:
   - Behavior Type: `Inference Only`
   - Model: `results/<run_id>/Hider.onnx` 또는 `Seeker.onnx` 드래그
3. Play

## Unity Agent 설정 체크리스트

### Hider
- [x] Behavior Name: `Hider`
- [x] Space Size: `10`
- [x] Continuous Actions: `3`
- [x] Behavior Type: `Default` (훈련 시) / `Inference Only` (테스트 시)
- [x] Rigidbody 컴포넌트 필수
- [x] GameController 참조 연결

### Seeker
- [x] Behavior Name: `Seeker`
- [x] 나머지 Hider와 동일

## 자주 발생하는 에러

| 에러 | 원인 | 해결 |
|------|------|------|
| UnityTimeOutException | Play 안 눌렀거나 늦음 | 20초 내 Play |
| Shape mismatch | 센서 설정 불일치 | Ray Perception 삭제 or 통일 |
| NullReferenceException | Rigidbody/GameController 없음 | 컴포넌트/참조 확인 |
| TrainerConfigError | Behavior Name 불일치 | YAML과 Unity 이름 맞추기 |

## 단계별 개발 계획

1. ✅ 기본 환경 + Vector Observation만으로 훈련
2. ⬜ Ray Perception Sensor 추가 (시야 감지)
3. ⬜ 장애물 추가 (숨을 곳)
4. ⬜ 움직이는 장애물 (grab/push)
5. ⬜ 팀 환경 (여러 Hider/Seeker)

## 응답 언어
- 사용자와 **한국어**로 대화
- 코드/명령어는 영어 유지
