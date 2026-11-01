# BathERP — 주문·생산·전표 연동 데모

**데모: https://erp.jongwonkim.dev**

C#을 처음 써보면서, 채용공고의 UNI ERP 업무(FI·SD·MM, MSSQL 저장 프로시저, 연계 인터페이스, 정합성)를
가장 작은 흐름 하나로 직접 구현해본 학습 프로젝트입니다. (2026년 11월 01일)

## 만든 방식

| 구분 | 내용 |
|---|---|
| 직접 작성 | 주문 처리 저장 프로시저 `usp_Order_Process` (입력 검증, 재고 잠금 조회, 주문·재고·수불·전표 트랜잭션, 롤백 후 실패 로그) |
| 직접 작성 | C#에서 그 프로시저를 호출하는 `OrderService` (SqlClient 연결, 입력·OUTPUT 파라미터) |
| AI 활용 | 테이블 설계 초안, 프로젝트 뼈대(컨트롤러·화면·조회), 생산 프로시저, Docker·Nginx 배포 설정. 작성된 코드는 Java·Spring과 비교하며 읽고 이해한 뒤 적용 |

가상의 욕실 건자재 회사에서 대리점 주문이 들어오면
**주문(SD) → 재고 확인·출고(MM) → 매출 전표(FI)** 가 저장 프로시저 하나의 트랜잭션 안에서 처리되고,
재고가 부족하면 **생산(PP, 시뮬레이션)** 으로 재고를 채운 뒤 이어서 처리합니다.
단종 품목은 생산할 수 없어 주문 전체가 롤백되고 실패 로그만 남습니다.

## 기술 스택

| 영역 | 기술 |
|---|---|
| 백엔드 | C# / .NET 10, ASP.NET Core MVC (Kestrel) |
| 화면 | Razor(.cshtml) + 약간의 JS |
| DB 접근 | Microsoft.Data.SqlClient (ADO.NET) |
| DB | SQL Server 2022 Express, T-SQL 저장 프로시저 |
| 배포 | Docker, docker-compose |

## 폴더 구조

```
bath-ERP/
├─ db/
│  ├─ 01_schema.sql        테이블 10개 · 시퀀스
│  ├─ 02_seed.sql          품목 3(1개 단종) · 거래처 2 · 계정 2 · 기초 재고
│  ├─ 03_procedures.sql    ★ 직접 작성: 주문 처리 · 생산 완료 프로시저
│  └─ 04_dashboard.sql     화면 조회 프로시저
├─ src/BathErp/
│  ├─ Program.cs           시작점 · 의존성 주입 등록
│  ├─ Controllers/         HomeController(화면) · OrderApiController(주문·생산 API)
│  ├─ Services/            DashboardService · ProductionService(참고 완성본) · ★ OrderService(직접 작성)
│  ├─ Models/              요청·응답·화면 모델
│  ├─ Views/Home/          Index.cshtml (한 페이지 화면)
│  └─ wwwroot/             css · js (생산 중 연출)
├─ Dockerfile
└─ docker-compose.yml
```

## 직접 작성하는 부분 ★

1. `db/03_procedures.sql` 의 `usp_Order_Process`
2. `src/BathErp/Services/OrderService.cs` 의 `PlaceAsync`

## 맥에서 처음 실행하기

### 1. 설치 (한 번만)

```bash
uname -m      # arm64면 Apple Silicon, x86_64면 Intel
```

- **Docker Desktop**: docker.com에서 맥용(본인 칩에 맞게) 설치.
  Apple Silicon이면 Docker Desktop 설정 → General → "Use Rosetta for x86_64/amd64 emulation on Apple Silicon" 켜기
  (SQL Server 이미지가 인텔용이라 필요)
- **.NET 10 SDK**: Microsoft .NET 다운로드 페이지에서 macOS용 .NET 10 SDK 설치 파일(pkg) 설치.
  Homebrew를 쓰면 `brew install --cask dotnet-sdk`

```bash
docker --version
dotnet --version   # 10.x 로 나오면 OK
```

### 2. DB 띄우고 스크립트 실행

```bash
cd ~/Desktop/Developer/project/daelim-bath-interview/bath-ERP
docker compose up -d db
# 20초 정도 기다린 뒤 (SQL Server 기동 시간)
for f in 01_schema 02_seed 03_procedures 04_dashboard; do
  docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'BathErp!2026' -C -i /db/$f.sql
done
```

프로시저를 고친 뒤에는 `03_procedures` 만 다시 실행하면 돼요 (`CREATE OR ALTER`라 덮어써짐).
DB를 처음 상태로 되돌리려면 `docker compose down -v` 후 위 과정을 다시.

### 3. 앱 실행

```bash
cd src/BathErp
dotnet run
```

터미널에 나오는 주소(보통 `http://localhost:5000`)를 브라우저로 열면 돼요.
