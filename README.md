# Codenotch

Windows 데스크톱에서 여러 AI 코딩 도구의 사용량과 세션 상태를 한눈에 확인하는 가벼운 노치형 모니터입니다.

화면 가장자리에 작은 링 게이지로 상주하며, 마우스를 올리면 필요한 정보만 부드럽게 펼쳐 보여줍니다. 계정 비밀번호를 별도로 저장하지 않고, 각 도구가 이미 로컬에 남긴 CLI 설정과 세션 정보를 바탕으로 상태를 읽습니다.

![Codenotch icon](dist/AppIcon.png)

## 주요 기능

- 화면 오른쪽에 고정되는 compact notch UI
- provider별 잔여 사용량 링 게이지
- hover 시 5시간/주간 등 세부 quota와 reset 시간 표시
- 연결되지 않은 provider와 활성 세션 상태 구분
- 마우스 드래그 이동 및 더블 클릭 재정렬
- pin 기능으로 상세 패널 고정
- 시스템 트레이 상주 및 사용량 임계치 알림
- Windows 11에서도 자연스럽게 보이는 반투명 glass 스타일
- 짧고 취소 가능한 hover motion과 Windows 모션 감소 설정 대응
- 별도 서버 없이 로컬 상태만 조회하는 구조

## 지원 provider

현재 다음 provider를 탐색합니다.

- OpenAI Codex / ChatGPT
- Claude Code
- Cursor
- GitHub Copilot
- Google Antigravity
- xAI Grok
- Ollama
- OpenCode, Command Code, GLM(Z.ai) 및 기타 provider

provider별로 읽을 수 있는 quota와 세션 정보는 설치 상태와 로컬 설정 형식에 따라 달라질 수 있습니다.

## 빠른 실행

### 패키지 사용

최신 Windows x64 패키지를 내려받아 압축을 풀고 `Codenotch.App.exe`를 실행합니다.

[Codenotch-win-x64-09c98f9.zip 다운로드](dist/Codenotch-win-x64-09c98f9.zip)

이 패키지는 self-contained single-file publish 결과물이라 별도의 .NET 런타임 설치가 필요하지 않습니다.

### 소스에서 실행

필요한 환경:

- Windows 10 또는 Windows 11
- .NET 8 SDK
- Windows 데스크톱 개발 환경(WPF)

```powershell
dotnet run --project src/Codenotch.App/Codenotch.App.csproj
```

앱은 일반 창 대신 시스템 트레이와 화면 가장자리 노치로 실행됩니다. 종료하려면 트레이 아이콘의 `Codenotch 종료`를 선택합니다.

## 사용 방법

| 동작 | 결과 |
| --- | --- |
| 노치에 마우스 올리기 | 상세 사용량 패널 확장 |
| 노치 밖으로 이동 | 잠시 후 compact 상태로 축소 |
| provider 링 클릭 | 상세 정보 provider 변경 |
| 노치 드래그 | 세로 위치 이동 |
| 노치 더블 클릭 | 화면 중앙 높이로 복귀 |
| `고정` 버튼 | 상세 패널을 계속 펼쳐 둠 |
| `새로고침` 버튼 | 모든 provider 다시 조회 |
| 트레이 아이콘 더블 클릭 | 노치 표시/숨김 |

## 프라이버시와 권한

Codenotch는 provider의 로컬 CLI 설정, 세션 데이터베이스, 로컬 포트 등 필요한 상태를 읽어 사용량을 표시합니다. 계정 비밀번호를 입력받거나 별도 계정 서버로 전송하지 않습니다.

사용하는 provider의 인증 파일과 토큰은 각 provider의 보안 정책을 따릅니다. 공유 PC에서는 로컬 계정과 설정 파일의 접근 권한을 확인한 뒤 사용하세요.

## 개발 및 테스트

솔루션 빌드:

```powershell
dotnet build Codenotch.sln
```

단위 테스트:

```powershell
dotnet test
```

Release 패키지 생성:

```powershell
dotnet publish src/Codenotch.App/Codenotch.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o dist/package
```

## 프로젝트 구조

```text
Codenotch/
├── src/
│   ├── Codenotch.Core/       provider 연동, 모델, 사용량 서비스
│   └── Codenotch.App/        WPF 노치 UI와 시스템 트레이
├── tests/                    provider 단위 테스트
├── dist/                     Windows 배포 패키지
├── Codenotch.sln
└── README.md
```

### 기술 구성

- .NET 8
- WPF
- Windows Forms NotifyIcon
- provider adapter 구조
- 로컬 데이터 기반 polling
- WPF animation과 Windows composition을 고려한 glass UI

## 문제 해결

### 노치가 보이지 않는 경우

시스템 트레이에서 Codenotch 아이콘을 확인한 뒤 `노치 보이기`를 선택하세요. 그래도 보이지 않으면 앱을 종료하고 다시 실행합니다.

### 특정 provider가 `미연결`로 표시되는 경우

해당 provider의 CLI 또는 데스크톱 앱에 먼저 로그인되어 있는지 확인하세요. provider가 설치되지 않았거나 예상 경로에 로컬 세션 정보가 없으면 미연결로 표시될 수 있습니다.

### 사용량이 최신 값이 아닌 경우

상세 패널의 `새로고침`을 누르거나 트레이 메뉴에서 `전체 새로고침`을 선택하세요. provider 자체의 reset 지연이나 로컬 캐시 때문에 값이 늦게 반영될 수도 있습니다.

## License

현재 저장소에는 별도 라이선스 파일이 포함되어 있지 않습니다. 사용·배포 정책을 정하기 전 저장소 소유자의 안내를 확인하세요.
