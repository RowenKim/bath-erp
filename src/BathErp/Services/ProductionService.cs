// =====================================================================
// ProductionService.cs — 생산 완료 처리 (완성본)
// DB의 저장 프로시저 dbo.usp_Production_Complete 를 호출해요.
// ★ OrderService.cs 를 쓸 때 이 파일을 옆에 띄워두고 그대로 따라 쓰면 돼요. ★
// =====================================================================

using System.Data;                   // CommandType, SqlDbType, ParameterDirection 같은 DB 공통 타입
using BathErp.Models;                // ProductionResult (Models.cs)
using Microsoft.Data.SqlClient;      // SqlConnection, SqlCommand — MSSQL 전용 드라이버 (Java의 JDBC 드라이버)

namespace BathErp.Services;          // 이 클래스는 BathErp.Services 소속 (Java의 package)

// public class = 다른 파일(컨트롤러 등)에서 이 클래스를 쓸 수 있게 공개
public class ProductionService
{
    // -----------------------------------------------------------------
    // 필드 = 이 객체가 계속 들고 있는 값
    //
    // private  → 이 클래스 안에서만 볼 수 있음. 컨트롤러 같은 바깥에서는 접근 불가
    //            (DB 접속 정보처럼 내부 사정은 숨김 = 캡슐화)
    // readonly → 생성자에서 딱 한 번만 값을 넣을 수 있고, 그 뒤로는 못 바꿈 (Java의 final)
    // string   → 문자열 타입
    // _connectionString → private 필드는 밑줄로 시작하는 게 C# 관례
    // -----------------------------------------------------------------
    private readonly string _connectionString;

    // -----------------------------------------------------------------
    // 생성자 = new 로 객체가 만들어지는 순간 딱 한 번 실행되는 코드
    //   - 이름이 클래스 이름과 같고, 반환 타입(void 등)을 쓰지 않음
    //
    // 생성자 주입(DI):
    //   이 객체는 우리가 new ProductionService(...) 로 직접 만들지 않아요.
    //   Program.cs 에 AddScoped<ProductionService>() 로 등록해뒀기 때문에,
    //   프레임워크가 필요할 때 만들어주면서 괄호 안의 IConfiguration 도 알아서 넣어줘요.
    //   (Spring의 생성자 주입 / @Autowired 와 같은 개념)
    //
    // IConfiguration config = appsettings.json 설정 파일을 읽는 도구
    //   (이름 앞의 I 는 인터페이스라는 C# 관례. Java의 Environment / @Value 역할)
    // -----------------------------------------------------------------
    public ProductionService(IConfiguration config)
    {
        // appsettings.json 의 "ConnectionStrings" → "BathErp" 값을 꺼내서 필드에 저장
        // ?? = 왼쪽이 null이면 오른쪽을 실행. 설정이 없으면 예외를 던져 앱이 바로 알려주게 함
        // readonly 필드라서 값을 넣을 수 있는 곳은 여기(생성자)뿐이에요
        _connectionString = config.GetConnectionString("BathErp")
            ?? throw new InvalidOperationException("ConnectionStrings:BathErp 설정이 없어요.");
    }

    // -----------------------------------------------------------------
    // 메서드: 생산 완료 처리
    //
    // public              → 컨트롤러에서 호출할 수 있게 공개 (창구 역할)
    // async               → 이 메서드 안에서 await(기다리기)를 쓰겠다는 표시
    // Task<ProductionResult> → "작업이 끝나면 ProductionResult를 돌려줌"
    //                          (비동기 메서드는 반환 타입을 Task<T>로 감싸요.
    //                           Java의 CompletableFuture<T>와 비슷하지만 훨씬 간단)
    // CompleteAsync       → 비동기 메서드 이름은 끝에 Async를 붙이는 게 관례
    // (string itemCode, int shortage) → 파라미터 2개
    // -----------------------------------------------------------------
    public async Task<ProductionResult> CompleteAsync(string itemCode, int shortage)
    {
        // [1] DB 연결 만들기
        //   new SqlConnection(...) → 접속 정보로 연결 객체 생성 (아직 연결 전)
        //   await using → 이 메서드가 끝날 때 연결을 자동으로 닫아줌
        //                 (Java의 try-with-resources. close()를 직접 안 불러도 됨)
        await using var conn = new SqlConnection(_connectionString);

        // [2] 실제로 DB에 연결
        //   OpenAsync() → 연결하는 동안 서버를 붙잡고 있지 않도록 비동기로 실행
        //   await → 연결이 끝날 때까지 기다렸다가 다음 줄로 넘어감
        await conn.OpenAsync();

        // [3] 실행할 명령 만들기
        //   new SqlCommand("프로시저 이름", 연결) → 이 연결로 이 프로시저를 실행할 명령
        //   { CommandType = ... } → 객체를 만들면서 바로 속성 값도 넣는 문법 (객체 초기화)
        //   CommandType.StoredProcedure → "이건 SQL 문장이 아니라 저장 프로시저 이름이에요"
        //   [Java] CallableStatement cs = conn.prepareCall("{call dbo.usp_Production_Complete(?, ?, ?, ?)}");
        await using var cmd = new SqlCommand("dbo.usp_Production_Complete", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // [4] 입력 파라미터 넣기
        //   Parameters.Add("@이름", 타입, 길이) → 프로시저의 파라미터와 이름·타입을 맞춰서 추가
        //   .Value = 값 → 실제로 넘길 값
        //   SqlDbType.VarChar, 20 → 프로시저에 @ItemCode VARCHAR(20) 으로 선언돼 있어서 맞춤
        //   [Java] cs.setString(1, itemCode); cs.setInt(2, shortage);
        cmd.Parameters.Add("@ItemCode", SqlDbType.VarChar, 20).Value = itemCode;
        cmd.Parameters.Add("@Shortage", SqlDbType.Int).Value = shortage;

        // [5] OUTPUT 파라미터 만들기 — 프로시저가 실행 후 값을 "돌려주는" 파라미터
        //   Add(...) 의 결과(파라미터 객체)를 변수에 받아둬야 나중에 값을 꺼낼 수 있어요
        //   Direction = ParameterDirection.Output → "이건 넣는 게 아니라 받아오는 칸"
        //   [Java] cs.registerOutParameter(3, Types.VARCHAR);
        var prodNo = cmd.Parameters.Add("@ProdNo", SqlDbType.VarChar, 20);
        prodNo.Direction = ParameterDirection.Output;

        var producedQty = cmd.Parameters.Add("@ProducedQty", SqlDbType.Int);
        producedQty.Direction = ParameterDirection.Output;

        // [6] 실행
        //   ExecuteNonQueryAsync() → 결과 표(SELECT 결과)를 돌려주지 않는 프로시저를 실행할 때 사용
        //   (조회 결과가 있으면 ExecuteReaderAsync() — DashboardService 참고)
        //   프로시저 안에서 THROW 가 나면 여기서 SqlException 이 발생해 컨트롤러까지 올라가요
        //   [Java] cs.execute();
        await cmd.ExecuteNonQueryAsync();

        // [7] OUTPUT 값 꺼내서 결과 만들기
        //   prodNo.Value 의 타입은 object(무엇이든 될 수 있는 타입)라서 원하는 타입으로 바꿔야 해요.
        //   DB가 NULL을 돌려주면 C#의 null이 아니라 DBNull.Value 라는 특별한 값이 들어 있어서,
        //   (string)prodNo.Value 처럼 강제로 바꾸면 예외가 날 수 있어요. 그래서 안전한 방법을 씀:
        //
        //   prodNo.Value as string ?? ""
        //     as string → 문자열이면 그 값, 아니면(DBNull 등) null. 실패해도 예외 안 남
        //     ?? ""     → null이면 빈 문자열로
        //
        //   producedQty.Value is int n ? n : 0
        //     is int n  → 값이 int면 true이고, 그 값을 n 이라는 변수에 담아줌 (패턴 매칭)
        //     ? n : 0   → 삼항 연산자. int였으면 n, 아니면 0
        //
        //   new ProductionResult(...) → Models.cs 의 record 생성자
        //   return → async 메서드라도 Task로 감싸지 않고 그냥 값을 return 하면 됨
        return new ProductionResult(
            prodNo.Value as string ?? "",
            producedQty.Value is int n ? n : 0);
    }
    // 메서드가 끝나는 이 지점에서 await using 으로 만든 cmd, conn 이 자동으로 닫혀요.
}
