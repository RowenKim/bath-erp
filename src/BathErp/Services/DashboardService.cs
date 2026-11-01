// =====================================================================
// DashboardService.cs — 화면에 보여줄 데이터 조회 (완성본)
// 저장 프로시저 dbo.usp_Dashboard_Get 하나가 결과 표 4개(재고·주문·전표·로그)를
// 차례로 돌려주고, 여기서 그걸 순서대로 읽어서 DashboardVm 상자에 담아요.
// =====================================================================

using System.Data;                   // CommandType
using BathErp.Models;                // DashboardVm, StockRow 등
using Microsoft.Data.SqlClient;      // SqlConnection, SqlCommand, SqlDataReader

namespace BathErp.Services;

public class DashboardService
{
    // private readonly → 이 클래스 안에서만 쓰고(private), 생성자에서 한 번만 정해짐(readonly)
    private readonly string _connectionString;

    // 생성자 주입: 프레임워크가 설정 읽는 도구(IConfiguration)를 넣어줌
    public DashboardService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("BathErp")
            ?? throw new InvalidOperationException("ConnectionStrings:BathErp 설정이 없어요.");
    }

    // Task<DashboardVm> → 다 읽고 나면 DashboardVm(화면용 상자)을 돌려줌
    public async Task<DashboardVm> GetAsync()
    {
        // 빈 상자 하나 준비. 안의 리스트 4개는 Models.cs에서 처음부터 빈 리스트로 만들어져 있음
        var vm = new DashboardVm();

        // DB 연결 → 열기 (메서드가 끝나면 await using 덕분에 자동으로 닫힘)
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        // 프로시저 실행 명령
        await using var cmd = new SqlCommand("dbo.usp_Dashboard_Get", conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        // ExecuteReaderAsync() → SELECT 결과 표가 있는 프로시저를 실행하고, 한 줄씩 읽을 수 있는 리더를 받음
        // r = reader. [Java] ResultSet rs = cs.executeQuery();
        await using var r = await cmd.ExecuteReaderAsync();

        // ---------------------------------------------------------------
        // 결과 표 1) 재고
        // while (await r.ReadAsync()) → 다음 줄이 있으면 true, 없으면 false
        //                                [Java] while (rs.next())
        // r["컬럼명"] → 그 컬럼 값을 꺼냄. 타입이 object라서 (int), (string)처럼 괄호로 타입을 지정(캐스팅)
        //   (bool)r["can_produce"] → DB의 BIT는 C#의 bool
        // vm.Stocks.Add(...) → 리스트에 한 줄 추가
        // ---------------------------------------------------------------
        while (await r.ReadAsync())
            vm.Stocks.Add(new StockRow(
                (string)r["item_code"], (string)r["item_name"], (decimal)r["unit_price"],
                (bool)r["can_produce"], (int)r["qty"], (int)r["txn_sum"]));

        // NextResultAsync() → 같은 프로시저가 돌려준 "다음 결과 표"로 넘어감
        // 결과 표 2) 최근 주문
        await r.NextResultAsync();
        while (await r.ReadAsync())
            vm.Orders.Add(new OrderRow(
                (string)r["order_no"], (string)r["cust_name"], (string)r["item_code"],
                (int)r["qty"], (decimal)r["total_amount"], (string)r["status"], (DateTime)r["created_at"]));

        // 결과 표 3) 전표 분개
        // r["order_no"] as string → NULL일 수 있는 컬럼은 강제 캐스팅 대신 as 로 (NULL이면 null이 됨)
        await r.NextResultAsync();
        while (await r.ReadAsync())
            vm.Vouchers.Add(new VoucherRow(
                (string)r["voucher_no"], r["order_no"] as string, (string)r["account_name"],
                (decimal)r["debit"], (decimal)r["credit"]));

        // 결과 표 4) 로그. (long) → DB의 BIGINT
        await r.NextResultAsync();
        while (await r.ReadAsync())
            vm.Logs.Add(new LogRow(
                (long)r["log_id"], r["ref_no"] as string, (string)r["action"], (string)r["result"],
                r["message"] as string, (DateTime)r["created_at"]));

        // 다 채운 상자를 돌려줌 → HomeController가 받아서 화면에 넘김
        return vm;
    }
}
