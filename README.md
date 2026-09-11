# Codenotch for Windows 🪟⚡

> macOS 전용이었던 **Codenotch**를 역공학(Reverse Engineering)하여 Windows 네이티브 환경에 맞게 완벽하게 재구성한 AI 어시스턴트 쿼터 & 세션 모니터입니다.

![Codenotch Icon](dist/AppIcon.png)

---

## 🌟 주요 특징 (Key Features)

1. **Zero-Login Architecture (비밀번호 불필요)**
   - 별도로 계정 아이디/비밀번호를 입력받지 않습니다.
   - 로컬 PC에 이미 로그인된 CLI 설정 파일(`auth.json`), 에디터 세션 DB(`state.vscdb`), 로컬 포트(`11434` 등) 및 인증 토큰을 안전하게 재활용합니다.

2. **지원하는 AI 코딩 어시스턴트**
   - **Google Antigravity**: 주간/일간 쿼터 및 활성 세션 모니터링
   - **OpenAI Codex / ChatGPT**: 5시간/주간/월간 할당량 및 잔여량
   - **Cursor**: Pro/Team 요청 수치(`numFastRequests / maxFastRequests`) 및 월별 리셋 주기
   - **Claude Code**: 세션 쿼터, 주간 한도 및 CLI 연동
   - **GitHub Copilot**: 할당량 및 플랜 상태
   - **xAI Grok**: Grok Build 크레딧 및 주간 잔여 사용률
   - **Ollama**: 로컬 실행 중인 모델 및 오프라인 상태 감지
   - **OpenCode, Command Code, GLM (Z.ai)**

3. **모던 다이내믹 아일랜드 / 노치 바 UI (Modern Fluent Overlay)**
   - **컴팩트 알약 뷰 (Pill Bar)**: 화면 상단 중앙에 자석처럼 붙어있는 세련된 다크 글래스 알약 위젯.
   - **호버 확장 (Expand on Hover)**: 마우스를 올리면 부드러운 애니메이션과 함께 상세 카드(사용량 퍼센트, 게이지 바, 리셋까지 남은 시간, 세션 상태)가 펼쳐집니다.
   - **자유 드래그 이동**: 마우스로 어디든 원하는 위치로 드래그할 수 있으며, 더블 클릭 시 다시 화면 상단 중앙으로 복귀합니다.
   - **상시 표시 (Always Show) 핀 고정**: 📌 버튼으로 노치를 항상 펼쳐진 상태로 고정 가능.

4. **시스템 트레이 (System Tray) 상주 & 임계치 알림**
   - 작업 표시줄 트레이 아이콘 지원 (더블 클릭 시 노치 표시/숨김)
   - 쿼터 80% 및 100% 도달 시 Windows 시스템 경고 알림

5. **초경량 고성능 네이티브 앱**
   - .NET 8.0 WPF 기반으로 빌드되어 실행 파일 크기가 1MB 미만(단 740KB)이며, 메모리 사용량이 극히 적습니다.

---

## 🚀 실행 및 빌드 방법

### 1. 즉시 실행 (Pre-built Release)
배포 디렉토리 `dist/`에 단일 실행 파일로 빌드되어 있습니다.
```powershell
# 배포 폴더로 이동 후 실행
cd dist
.\Codenotch.App.exe
```

### 2. 소스 코드에서 빌드 및 디버그
```powershell
# 솔루션 빌드
dotnet build Codenotch.sln

# 바로 실행
dotnet run --project src/Codenotch.App/Codenotch.App.csproj

# 단위 테스트 실행
dotnet test
```

---

## 📁 프로젝트 구조

```
codenotch/
├── Codenotch.dmg               # 원본 macOS DMG 패키지
├── extracted/                  # 리버스 엔지니어링 분석 추출 데이터
├── strings_dump.txt            # 로컬라이제이션 및 UI 문자열 추출본
├── dist/                       # Windows 배포 실행 파일 (740KB 단일 exe)
│   ├── Codenotch.App.exe
│   ├── AppIcon.ico
│   └── AppIcon.png
├── src/
│   ├── Codenotch.Core/         # AI 어시스턴트 프로바이더 & 데이터 수집 엔진
│   │   ├── Models/             # UsageRecord, SessionStatus
│   │   ├── Interfaces/         # IUsageProvider
│   │   ├── Providers/          # Antigravity, Cursor, Codex, Claude, Copilot 등
│   │   └── Services/           # UsageManager (주기적 폴링 & 80/100% 알림)
│   └── Codenotch.App/          # WPF 모던 글래스 노치 UI & 시스템 트레이
│       ├── Controls/           # RingGaugeControl (원형 게이지 컨트롤)
│       └── Views/              # NotchWindow, SettingsWindow
└── tests/
    └── Codenotch.Tests/        # 프로바이더 단위 테스트 (xUnit)
```
