# Hide N Seek ML-Agents 프로젝트

Unity ML-Agents를 활용한 강화학습 기반 숨바꼭질 AI

## 빠른 시작

### 1. Python 환경 설정
```powershell
# 가상환경 생성 및 활성화
python -m venv venv
.\venv\Scripts\activate

# 패키지 설치
pip install mlagents torch numpy tensorboard
```

### 2. Unity 설정
1. Unity Hub에서 이 프로젝트 열기
2. **Window > Package Manager** 열기
3. **+** 버튼 > **Add package by name**
4. 입력: `com.unity.ml-agents`

### 3. 씬 구성
1. 빈 GameObject 생성 → `GameController` 스크립트 추가
2. Capsule 2개 생성 → `HideSeekAgent` 스크립트 추가
   - 하나는 Team = Hider
   - 하나는 Team = Seeker
3. 각 Agent에 `Behavior Parameters` 컴포넌트 추가
   - Behavior Name: `Hider` 또는 `Seeker`
   - Vector Observation: 10
   - Continuous Actions: 3
4. `Ray Perception Sensor 3D` 추가 (선택사항)

### 4. 훈련
```powershell
# Python 환경 활성화 후
mlagents-learn Assets/Config/hideseek.yaml --run-id=test1

# Unity에서 Play 버튼 누르기
```

### 5. TensorBoard로 모니터링
```powershell
tensorboard --logdir results
```

## 파일 구조
```
Assets/
├── Scripts/
│   ├── HideSeekAgent.cs    # 에이전트 로직
│   └── GameController.cs    # 게임 관리
├── Config/
│   └── hideseek.yaml        # 훈련 설정
└── Scenes/
    └── SampleScene.unity
```

## 실험 아이디어
- 에이전트 수 조절 (1v1, 2v2, 3v1...)
- 장애물 추가해서 환경 복잡도 높이기
- 보상 함수 수정해보기
- Prep Phase 시간 조절
