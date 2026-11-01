// =====================================================================
// HomeController.cs — 화면(페이지)을 돌려주는 컨트롤러
// 브라우저가 "/" 주소로 들어오면 DB에서 데이터를 가져와 Index.cshtml 화면을 만들어 보내요.
// (Spring의 @Controller + JSP를 돌려주는 메서드와 같은 역할)
// =====================================================================

using BathErp.Services;              // DashboardService
using Microsoft.AspNetCore.Mvc;      // Controller, IActionResult, [HttpGet] 등 MVC 기능

namespace BathErp.Controllers;

// : Controller → Controller 클래스를 상속받음 (Java의 extends)
//   상속받으면 View(...) 같은 "화면 돌려주기" 기능을 그대로 쓸 수 있어요.
public class HomeController : Controller
{
    // private readonly → 이 클래스 안에서만, 생성자에서 한 번만 정해지는 필드
    private readonly DashboardService _dashboard;

    // 생성자 주입: Program.cs 에 등록한 DashboardService를 프레임워크가 만들어서 넣어줌
    // 우리가 new DashboardService(...) 를 쓸 일이 없어요.
    public HomeController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    // [HttpGet("/")] → 대괄호는 "속성(Attribute)". Java의 어노테이션(@GetMapping("/"))과 같아요.
    //                   브라우저가 GET / 으로 요청하면 이 메서드가 실행돼요.
    // IActionResult  → "응답"을 뜻하는 타입. 화면일 수도, JSON일 수도, 오류일 수도 있어요.
    // async Task<...> → 안에서 DB 조회를 기다려야(await) 해서 비동기 메서드
    [HttpGet("/")]
    public async Task<IActionResult> Index()
    {
        // DB에서 화면에 필요한 데이터 4종을 가져옴 (끝날 때까지 기다림)
        var vm = await _dashboard.GetAsync();

        // View(vm) → Views/Home/Index.cshtml 을 찾아서 vm 을 넘겨주고 HTML을 만들어 응답
        //   (컨트롤러 이름 Home + 메서드 이름 Index → Views/Home/Index.cshtml 로 자동 연결)
        return View(vm);
    }
}
