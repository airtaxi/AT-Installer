# AT Installer

한국어 | **[English](README.md)**

WinUI 3로 구축된 경량 Windows 인스톨러 프레임워크<br><br>
<img width="1178" height="589" alt="image" src="https://github.com/user-attachments/assets/e20ab765-88ab-44c0-837c-f40041b209d0" />

## 개요

AT Installer는 Windows 애플리케이션을 위한 현대적이고 경량화된 인스톨러 프레임워크입니다. 두 가지 주요 구성 요소로 이루어져 있습니다:

1. **Installer Composer** - 설치 패키지를 생성하는 GUI 도구
2. **Installer** - 애플리케이션을 추출하고 설치하는 런타임 인스톨러

이 프레임워크는 Mica 배경이 적용된 WinUI 3를 사용하여 Windows 11의 현대적인 외관을 제공하며, 다국어(영어, 한국어, 일본어, 중국어)를 지원합니다.

## 기능

- Mica 배경이 적용된 WinUI 3 인터페이스의 현대적인 UI
- 다국어 지원 (영어, 한국어, 일본어, 중국어)
- ZIP 압축을 사용한 `.atp` (AT Package) 형식의 간단한 패키지 포맷
- 설치/제거 시 커스텀 스크립트 실행 가능
- 멀티 아키텍처 지원 (x64, ARM64)
- 명령줄을 통한 자동 설치
- Windows 제거 프로그램 레지스트리 통합
- 시작 메뉴 바로가기 자동 생성
- 7z SFX를 사용한 자동 압축 해제 인스톨러 생성

## 프로젝트 구조

솔루션은 세 개의 프로젝트로 구성되어 있습니다:

### Installer
다음을 처리하는 런타임 인스톨러 애플리케이션:
- 패키지 추출 및 설치
- 레지스트리 등록
- 시작 메뉴 바로가기 생성
- 다국어 UI

### InstallerComposer
다음 기능을 제공하는 패키지 생성 도구:
- 애플리케이션 매니페스트 구성
- 아이콘 임베딩
- 커스텀 스크립트 구성
- 설정 저장/로드 기능

### InstallerCommons
다음을 포함하는 공유 라이브러리:
- 진행률 리포팅 기능이 있는 ZIP 압축 유틸리티
- 설치 및 제거 매니페스트 정의
- 공통 헬퍼 클래스

## 시작하기

### 필수 요구 사항

- Windows 10 (1809) 이상
- .NET 10.0 SDK
- WinUI 3 워크로드가 설치된 Visual Studio 2022 / 2026

### 소스에서 빌드

1. 리포지토리 클론
2. Visual Studio 2022 / 2026에서 `AT Installer.sln` 열기
3. 솔루션 빌드

### 설치 패키지 만들기

1. **InstallerComposer** 실행
2. 애플리케이션 매니페스트 작성:
   - **Application ID**: "생성" 버튼을 클릭하여 고유한 GUID 생성
   - **Application Name**: 애플리케이션 표시 이름
   - **Application Publisher**: 게시자/회사 이름
   - **Installation Folder Name**: `%AppData%` 내 폴더 이름
3. 애플리케이션 아이콘 설정 (PNG 형식)
4. 애플리케이션 파일이 포함된 **Application Root Directory** 선택
5. 메인 **Executable File** 선택
6. (선택사항) 설치 후/제거 시 실행할 스크립트 구성
7. 출력 `.atp` 파일의 **Package File Path** 설정
8. **파일 > Export Package** 실행

### 애플리케이션 설치

`.atp` 파일을 더블클릭하여 인스톨러를 실행하거나, 명령줄을 통해 실행:

```
Installer.exe "경로\패키지.atp"
```

자동 설치:
```
Installer.exe "경로\패키지.atp" /silent
```

### 자동 설치 종료 코드

| 코드 | 설명 |
|------|-------------|
| 0 | 성공 |
| 24 | 다운그레이드 불가 (설치된 버전이 더 최신) |
| 25 | 압축 해제 실패 |
| 26 | 레지스트리 등록 실패 |

## 패키지 형식 (.atp)

`.atp` 패키지는 다음을 포함하는 ZIP 아카이브입니다:
- `manifest.json`: 설치 매니페스트
- `data.bin`: 압축된 애플리케이션 파일

## 구성 파일

### Composer Config (.aticconfig)

반복적인 빌드를 위한 패키지 구성 저장/로드:
- **파일 > 설정 저장**
- **파일 > 설정 로드**

명령줄 인수로 구성 파일을 전달하여 패키지 생성 자동화:
```
InstallerComposer.exe "경로\구성파일.aticconfig"
```

## 다운로드

[Releases](https://github.com/airtaxi/AT-Installer/releases) 페이지에서 최신 릴리스를 다운로드하세요.

### 사용 가능한 다운로드

릴리스에는 자동 압축 해제 인스톨러를 생성하는 도구가 포함된 압축 파일(`Release.7z`)이 포함되어 있습니다.

> **참고**: 현재 **x64** 및 **ARM64** 아키텍처를 지원합니다. x86 지원은 향후 릴리스에서 추가될 예정입니다. 현재 x86이 필요한 경우, Installer 프로젝트 자체는 x86을 지원하므로 소스에서 빌드하여 출력물을 사용할 수 있습니다.

## 자동 압축 해제 인스톨러 만들기

Release 폴더에는 AT Installer와 `.atp` 패키지를 단일 `.exe` 파일로 번들링하는 자동 압축 해제(SFX) 인스톨러를 생성하는 도구가 포함되어 있습니다.

### Release 폴더 구조

```
Release/
├── Archive/                    # x64 Installer 빌드
│   ├── Installer.exe
│   └── (기타 런타임 파일)
├── Archive-arm64/              # ARM64 Installer 빌드
│   ├── Installer.exe
│   └── (기타 런타임 파일)
├── 7zS.sfx                     # 7-Zip SFX 모듈
├── bz.exe                      # 반디집 명령줄 도구 (선택사항)
├── config.txt                  # 일반 인스톨러 구성
├── config_silent.txt           # 자동 인스톨러 구성
├── createArchive.bat           # Archive 폴더에서 Archive.7z 생성
├── createArchive-arm64.bat     # Archive-arm64 폴더에서 Archive-arm64.7z 생성
├── composeInstaller.bat        # Installer.exe 생성 (x64)
├── composeInstaller-arm64.bat  # Installer-arm64.exe 생성
├── composeSilentInstaller.bat  # 자동 Installer.exe 생성 (x64)
├── composeSilentInstaller-arm64.bat  # 자동 Installer.exe 생성 (ARM64)
├── composeByName.bat           # 사용자 지정 이름 인스톨러 생성 (x64)
└── composeByName-arm64.bat     # 사용자 지정 이름 인스톨러 생성 (ARM64)
```

> **참고**: `Archive` 및 `Archive-arm64` 폴더에는 게시된 Installer 런타임만 포함되어 있습니다. InstallerComposer는 별도의 개발 도구이며 이 폴더에 포함되어 있지 않습니다.

> **팁**: 시스템에 반디집이 설치되어 있는 경우, 배치 파일이 시스템에 설치된 반디집 명령줄 도구를 사용할 수 있으므로 `bz.exe`를 삭제할 수 있습니다.

### 단계별 가이드

#### 1. Release 아카이브 다운로드 및 압축 해제

GitHub Releases에서 `Release.7z`를 다운로드하고 폴더에 압축 해제 (예: `C:\ATInstaller\Release\`)

#### 2. 패키지 준비

`.atp` 패키지 파일을 적절한 폴더에 배치하고 `Package.atp`로 이름 변경:
- x64용: `Release\Archive\Package.atp`로 복사
- ARM64용: `Release\Archive-arm64\Package.atp`로 복사

> **중요**: 파일 이름은 정확히 `Package.atp`여야 합니다.

#### 3. 7z 아카이브 생성

적절한 배치 파일을 실행하여 압축 아카이브 생성:

```batch
# x64용
createArchive.bat

# ARM64용
createArchive-arm64.bat
```

이렇게 하면 Release 폴더에 `Archive.7z` 또는 `Archive-arm64.7z`가 생성됩니다.

> **참고**: 이 배치 파일은 `bz.exe` (반디집 CLI) 또는 시스템에 설치된 반디집을 사용하여 7z 아카이브를 생성합니다.

#### 4. 자동 압축 해제 인스톨러 생성

다음 옵션 중 하나를 선택:

**옵션 A: 표준 인스톨러**
```batch
# x64용
composeInstaller.bat

# ARM64용
composeInstaller-arm64.bat
```
설치 UI를 표시하는 `Installer.exe` 생성

**옵션 B: 자동 인스톨러**
```batch
# x64용
composeSilentInstaller.bat

# ARM64용
composeSilentInstaller-arm64.bat
```
자동 모드로 실행되는 `Installer.exe` 생성 (UI 없음)

**옵션 C: 사용자 지정 이름 인스톨러**
```batch
# x64용
composeByName.bat

# ARM64용
composeByName-arm64.bat
```
사용자 지정 파일 이름을 입력하면 `사용자이름.exe` 생성

#### 5. 배포

생성된 `.exe` 파일은 다음을 수행하는 독립 실행형 인스톨러입니다:
1. 번들 파일을 임시 위치에 추출
2. `Package.atp`와 함께 `Installer.exe` 실행
3. 애플리케이션 설치

### Config 파일 설명

**config.txt** (일반 설치):
```
;!@Install@!UTF-8!
RunProgram="Installer.exe Package.atp"
;!@InstallEnd@!
```

**config_silent.txt** (자동 설치):
```
;!@Install@!UTF-8!
RunProgram="Installer.exe Package.atp /silent"
;!@InstallEnd@!
```

이러한 구성 파일은 7z SFX 모듈에 압축 해제 후 실행할 명령을 알려줍니다.

### 사용 예시

```batch
# 1. Release.7z 압축 해제
7z x Release.7z -oC:\ATInstaller\Release

# 2. 패키지 복사
copy MyApp.atp C:\ATInstaller\Release\Archive\Package.atp

# 3. 아카이브 생성
cd C:\ATInstaller\Release
createArchive.bat

# 4. 인스톨러 생성
composeInstaller.bat

# 5. 사용자에게 Installer.exe 배포
```

## 기술 세부 사항

- **프레임워크**: .NET 10.0
- **UI 프레임워크**: WinUI 3 (Windows App SDK 1.8)
- **압축**: 최적 압축을 사용하는 ZIP
- **설치 위치**: `%AppData%\{InstallationFolderName}`
- **레지스트리**: `HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`
- **SFX 모듈**: 7-Zip SFX (7zS.sfx)
- **아카이브 도구**: 반디집 CLI (bz.exe) 또는 시스템에 설치된 반디집
- **지원 아키텍처**: x64, ARM64 (x86 지원 예정)

## 저자

**이호원 (airtaxi)**

- GitHub: [@airtaxi](https://github.com/airtaxi)

## 기여

기여를 환영합니다! Pull Request를 자유롭게 제출해주세요.
