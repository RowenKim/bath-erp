// =====================================================================
// OrderApiController.cs — 화면의 JS가 fetch로 부르는 API
// 주문 등록(POST /api/orders), 생산 완료(POST /api/production) 두 개를 받아요.
// (Spring의 @RestController + @RequestMapping("/api") 와 같은 역할)
// =====================================================================

using BathErp.Models;                // OrderRequest, ProductionRequest
using BathErp.Services;              // OrderService, ProductionService
using Microsoft.AspNetCore.Mvc;      // ControllerBase, IActionResult, [HttpPost] 등
using Microsoft.Data.SqlClient;      // SqlException (DB에서 난 오류)

namespace BathErp.Controllers;

// [ApiController] → "이건 JSON을 주고받는 API 컨트롤러" 표시 (Spring의 @RestController)
// [Route("api")]  → 이 클래스의 모든 주소 앞에 /api 가 붙음 (Spring의 @RequestMapping("/api"))
// : ControllerBase → 화면(View) 기능 없이 API용 기능만 있는 부모 클래스를 상속
[ApiController]
[Route("api")]
public class OrderApiController : ControllerBase
{
    // 이 클래스 안에서만 쓰고(private), 생성자에서 한 번만 정해지는(readonly) 서비스 두 개
    private readonly OrderService _orders;
    private readonly ProductionService _production;

    // 생성자 주입: Program.cs 에 등록해둔 서비스 두 개를 프레임워크가 넣어줌
    public OrderApiController(OrderService orders, ProductionService production)
    {
        _orders = orders;
        _production = production;
    }

    // -----------------------------------------------------------------
    // POST /api/orders
    //   [HttpPost("orders")] → POST 요청 + 주소 /api/orders (Spring의 @PostMapping("orders"))
    //   [FromBody] OrderRequest req → 요청 본문의 JSON을 OrderRequest로 자동 변환 (Spring의 @RequestBody)
    //     화면이 보낸 { "custCode": "C001", "itemCode": "TC-100", "qty": 10 } 이 req 가 됨
    // -----------------------------------------------------------------
    [HttpPost("orders")]
    public async Task<IActionResult> PlaceOrder([FromBody] OrderRequest req)
    {
        try
        {
            // 주문 처리 → 결과를 Ok(...)로 감싸면 HTTP 200 + JSON 으로 응답
            //   응답 예: { "result": "OK", "orderNo": "SO-0001", "shortage": 0 }
            return Ok(await _orders.PlaceAsync(req));
        }
        // catch (타입 ex) when (조건) → 조건이 맞는 예외만 잡음 (Java엔 없는 "예외 필터")
        //   ex.Number = 프로시저에서 THROW 할 때 정한 오류 번호
        //   is 50001 or 50002 → 50001 또는 50002일 때만
        catch (SqlException ex) when (ex.Number is 50001 or 50002)
        {
            // BadRequest(...) → HTTP 400 + JSON { "error": "메시지", "code": 50001 }
            // new { ... } → 이름 없는 임시 객체(익명 타입). JSON 응답 만들 때 편해요
            return BadRequest(new { error = ex.Message, code = ex.Number });
        }
        // OrderService가 아직 TODO 상태일 때 던지는 예외 → HTTP 501(아직 구현 안 됨)
        catch (NotImplementedException ex)
        {
            return StatusCode(501, new { error = ex.Message });
        }
        // 그 외 예상 못 한 예외는 잡지 않음 → 프레임워크가 HTTP 500 으로 응답
    }

    // -----------------------------------------------------------------
    // POST /api/production
    //   요청 예: { "itemCode": "TC-100", "shortage": 8 }
    //   응답 예: { "prodNo": "PR-0001", "producedQty": 18 }
    // -----------------------------------------------------------------
    [HttpPost("production")]
    public async Task<IActionResult> CompleteProduction([FromBody] ProductionRequest req)
    {
        try
        {
            return Ok(await _production.CompleteAsync(req.ItemCode, req.Shortage));
        }
        // 50002 = 입력 오류, 50003 = 단종 품목이라 생산 불가 (usp_Production_Complete 에서 THROW)
        catch (SqlException ex) when (ex.Number is 50002 or 50003)
        {
            return BadRequest(new { error = ex.Message, code = ex.Number });
        }
    }
}
