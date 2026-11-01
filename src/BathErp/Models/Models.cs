// =====================================================================
// Models.cs — 데이터를 담는 "그릇" 모음
// 로직은 없고, 값만 들고 다니는 용도예요. (Java의 DTO / VO)
// =====================================================================

// namespace = Java의 package. 이 파일의 클래스들이 "BathErp.Models" 소속이라는 뜻
// 다른 파일에서 쓰려면 using BathErp.Models; 로 가져와요.
namespace BathErp.Models;

// ---------------------------------------------------------------------
// record = 값만 담는 불변(바꿀 수 없는) 클래스를 한 줄로 만드는 문법 (Java 16+의 record와 같음)
//
//   public record StockRow(string ItemCode, ...);
//   이 한 줄이 아래를 전부 자동으로 만들어줘요.
//     - 생성자:  new StockRow("TC-100", "양변기", ...)
//     - 읽기 전용 프로퍼티:  row.ItemCode, row.ItemName ...  (getter만 있고 setter는 없음)
//
//   public = 어디서든 이 타입을 쓸 수 있음
//   괄호 안 = (타입 이름, 타입 이름, ...) 순서대로 생성자 파라미터이자 프로퍼티
// ---------------------------------------------------------------------

// ---- 화면 표시용: DB 조회 결과 한 줄씩을 담음 ----

// 재고 한 줄. bool = true/false (Java의 boolean)
// decimal = 돈 계산용 숫자 (Java의 BigDecimal인데, + - * 연산자를 그대로 쓸 수 있음)
public record StockRow(string ItemCode, string ItemName, decimal UnitPrice, bool CanProduce, int Qty, int TxnSum);

// 주문 한 줄. DateTime = 날짜+시간 (Java의 LocalDateTime)
public record OrderRow(string OrderNo, string CustName, string ItemCode, int Qty, decimal Amount, string Status, DateTime CreatedAt);

// 전표 분개 한 줄.
// string? 의 물음표 = "이 값은 null일 수도 있다"는 표시 (Java의 @Nullable)
// 전표에 주문번호가 없는 경우도 있을 수 있어서 ? 를 붙임
public record VoucherRow(string VoucherNo, string? OrderNo, string AccountName, decimal Debit, decimal Credit);

// 로그 한 줄. long = 큰 정수 (DB의 BIGINT)
public record LogRow(long LogId, string? RefNo, string Action, string Result, string? Message, DateTime CreatedAt);

// ---------------------------------------------------------------------
// class = 일반 클래스. record와 달리 안의 내용을 바꿀 수 있음
// 화면(Index.cshtml)에 넘겨줄 데이터 4종을 한 상자에 담는 용도 (Spring MVC의 Model 객체 같은 것)
// ---------------------------------------------------------------------
public class DashboardVm
{
    // { get; } = 읽기 전용 프로퍼티 (getter만 있음)
    //   → Stocks 라는 "리스트 자체"를 다른 리스트로 바꿔치기는 못 하지만,
    //     리스트 안에 항목을 Add() 하는 건 가능
    // = new(); = 처음부터 빈 리스트를 하나 만들어 둠 (new List<StockRow>()의 줄임)
    // List<StockRow> = StockRow만 담는 리스트 (Java의 List<StockRow>, ArrayList)
    public List<StockRow> Stocks { get; } = new();
    public List<OrderRow> Orders { get; } = new();
    public List<VoucherRow> Vouchers { get; } = new();
    public List<LogRow> Logs { get; } = new();
}

// ---- API 요청·응답: 화면(JS)과 서버가 JSON으로 주고받는 형태 ----
// C# 쪽 이름은 대문자(CustCode)지만, JSON으로 오갈 땐 자동으로 camelCase(custCode)로 바뀌어요.
//   화면이 보내는 JSON:  { "custCode": "C001", "itemCode": "TC-100", "qty": 10 }

// 주문 요청: 어느 거래처가, 어떤 품목을, 몇 개
public record OrderRequest(string CustCode, string ItemCode, int Qty);

// 주문 결과
//   Result   = "OK"(완료) 또는 "NEED_PRODUCTION"(재고 부족, 생산 필요)
//   OrderNo  = 완료됐을 때의 주문번호. 생산 필요일 땐 없으니 string? (null 가능)
//   Shortage = 부족한 수량
public record OrderResult(string Result, string? OrderNo, int Shortage);

// 생산 요청: 어떤 품목이 몇 개 부족한지
public record ProductionRequest(string ItemCode, int Shortage);

// 생산 결과: 생산번호, 실제로 만든 수량(부족분 + 안전재고)
public record ProductionResult(string ProdNo, int ProducedQty);
