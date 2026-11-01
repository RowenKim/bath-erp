// =====================================================================
// OrderService.cs — ★ 직접 작성할 곳 ★
// 주문 처리 저장 프로시저 dbo.usp_Order_Process 를 호출해요.
// ProductionService.cs 가 같은 패턴의 완성본이에요. 옆에 띄워두고 보면서 쓰세요.
// (그 파일의 [1]~[7] 주석 순서와 아래 TODO 1~6 순서가 같아요)
// =====================================================================

using System.Data;                   // CommandType, SqlDbType, ParameterDirection
using BathErp.Models;                // OrderRequest, OrderResult
using Microsoft.Data.SqlClient;      // SqlConnection, SqlCommand, SqlCommandBuilder

namespace BathErp.Services;

public class OrderService
{
    // private  → 이 클래스 안에서만 사용 (바깥에서 접속 정보를 못 보게)
    // readonly → 생성자에서 한 번만 값을 넣고 이후엔 못 바꿈 (Java의 final)
    private readonly string _connectionString;

    // 생성자 — 프레임워크가 이 객체를 만들 때 설정(IConfiguration)을 넣어줌 (생성자 주입)
    public OrderService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("BathErp")
            ?? throw new InvalidOperationException("ConnectionStrings:BathErp 설정이 없어요.");
    }

    // public async Task<OrderResult>
    //   → 컨트롤러가 호출할 수 있고(public), 안에서 await를 쓰며(async),
    //     작업이 끝나면 OrderResult를 돌려줌(Task<OrderResult>)
    // OrderRequest req
    //   → 화면에서 보낸 주문 정보. req.CustCode, req.ItemCode, req.Qty 로 꺼내 씀
    public async Task<OrderResult> PlaceAsync(OrderRequest req)
    {
        // TODO 1. DB 연결 만들고 열기
        //   - await using var 로 SqlConnection 만들기 (메서드 끝나면 자동으로 닫힘)
        //   - await conn.OpenAsync();
        //   → ProductionService [1], [2] 참고

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();


        // TODO 2. 실행할 명령 만들기
        //   - 프로시저 이름: "dbo.usp_Order_Process"
        //   - CommandType = CommandType.StoredProcedure
        //   → ProductionService [3] 참고
        await using var cmd = new SqlCommand("dbo.usp_Order_Process", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // TODO 3. 입력 파라미터 3개 (프로시저에 선언된 이름·타입과 똑같이)
        //   @CustCode  VarChar, 20   ← req.CustCode
        //   @ItemCode  VarChar, 20   ← req.ItemCode
        //   @Qty       Int           ← req.Qty
        //   → ProductionService [4] 참고
        cmd.Parameters.Add("@CustCode", SqlDbType.VarChar, 20).Value = req.CustCode;
        cmd.Parameters.Add("@ItemCode", SqlDbType.VarChar, 20).Value = req.ItemCode;
        cmd.Parameters.Add("@Qty", SqlDbType.Int).Value = req.Qty;

        // TODO 4. OUTPUT 파라미터 3개 (Add 결과를 변수에 받아두고 Direction = Output)
        //   @Result    VarChar, 20   → 'OK' 또는 'NEED_PRODUCTION'
        //   @OrderNo   VarChar, 20   → 'SO-0001' 형식 (OK일 때만 값이 있음)
        //   @Shortage  Int           → 부족 수량 (NEED_PRODUCTION일 때만 의미 있음)
        //   → ProductionService [5] 참고
        cmd.Parameters.Add("@Result", SqlDbType.VarChar, 20).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("@OrderNo", SqlDbType.VarChar, 20).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("@Shortage", SqlDbType.Int).Direction = ParameterDirection.Output;

        // TODO 5. 실행
        //   - await cmd.ExecuteNonQueryAsync();
        //   → ProductionService [6] 참고
        await cmd.ExecuteNonQueryAsync();

        // TODO 6. OUTPUT 값을 꺼내서 OrderResult로 돌려주기
        //   - 문자열은  변수.Value as string            (OrderNo는 null이어도 됨: string?)
        //   - Result는  변수.Value as string ?? ""      (null이면 빈 문자열)
        //   - 숫자는    변수.Value is int n ? n : 0
        //   - return new OrderResult(결과, 주문번호, 부족수량);
        //   ⚠ 같은 메서드 안에서 is int n 의 변수 이름 n 을 두 번 쓰면 안 돼요. 하나만 쓰면 괜찮아요.
        //   → ProductionService [7] 참고
        return new OrderResult(
            cmd.Parameters["@Result"].Value as string ?? "",
            cmd.Parameters["@OrderNo"].Value as string ?? "",
            cmd.Parameters["@Shortage"].Value is int n ? n : 0
        );

        // 오류(프로시저의 THROW 50001 등)는 여기서 try/catch로 잡지 마세요.
        // SqlException으로 그대로 올라가서 OrderApiController가 오류 번호별로 구분해 화면에 보내줘요.
        throw new NotImplementedException("OrderService.PlaceAsync를 아직 작성하지 않았어요.");
    }
}
