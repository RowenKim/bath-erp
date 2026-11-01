# BathERP — 주문·생산·전표 연동 데모

C#을 처음 써보면서, 채용공고의 UNI ERP 업무(FI·SD·MM, MSSQL 저장 프로시저, 연계 인터페이스, 정합성)를
가장 작은 흐름 하나로 직접 구현해본 학습 프로젝트입니다.

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

1. `db/03_procedures.sql` 의 `usp_Order_Process` → 그다음 `usp_Production_Complete`
2. `src/BathErp/Services/OrderService.cs` 의 `PlaceAsync`

파일 안에 약속(입력·출력)과 단계별 TODO, 힌트가 적혀 있어요.
`ProductionService.cs` 가 같은 패턴의 완성본이라 OrderService를 쓸 때 참고하면 돼요.

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

## 시연 순서 (30초)

1. 양변기 10개 주문 → 재고 감소, 주문·전표 2줄(차변 외상매출금 / 대변 제품매출), 성공 로그
2. 양변기 30개 주문 → 재고 부족 → "생산팀 작업 중" 5초 → 생산 입고 → 출고·전표까지 완료
3. 수전(단종) 10개 주문 → 생산 불가 → 롤백 → 실패 로그만 남음, 나머지 표는 그대로
4. 재고 표의 "수불 합계"가 재고와 계속 "일치"인 것 확인

## 설계하면서 정한 것

- **실패 로그는 ROLLBACK 다음에 기록**: 먼저 쓰면 로그까지 롤백돼서 사라짐
- **IF_LOG에는 FK를 걸지 않음**: 실패한 주문은 주문 테이블에 남지 않으므로
- **재고(결과)와 재고 수불(근거)을 함께 관리**: 재고 = 수불 합계로 언제든 정합성 검증
- **재고 조회 시 UPDLOCK**: 동시에 들어온 주문이 둘 다 "재고 충분"으로 읽는 문제 방지
- **생산 대기는 화면에서만**: 트랜잭션 안에서 기다리면 그동안 재고 테이블이 잠김

## C# 학습 노트 (직접 채우기)

만들면서 Java/Spring과 달라서 막혔던 것, 오히려 편했던 것을 적어두세요.

| 날짜 | 막혔던 것 / 느낀 점 | Java·Spring에서는 |
|---|---|---|
|  |  |  |
|  |  |  |
|  |  |  |
