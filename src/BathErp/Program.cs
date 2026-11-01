// =====================================================================
// Program.cs — 앱이 시작될 때 가장 먼저 실행되는 파일
// Java의 public static void main() + Spring 설정 클래스 역할을 한 파일에서 해요.
// 특이하게 class나 main 메서드가 안 보이는데, C#은 이렇게 "맨 위에 바로 코드"를
// 써도 컴파일러가 알아서 main 메서드로 감싸줘요. (top-level statements)
// =====================================================================

// using = Java의 import. 다른 폴더(네임스페이스)의 클래스를 이 파일에서 쓰겠다는 선언
// BathErp.Services 안에 있는 DashboardService, OrderService 등을 쓰기 위해 가져옴
using BathErp.Services;

// var = "타입은 오른쪽 보고 알아서 정해줘" (Java 10+의 var와 같음)
// WebApplication.CreateBuilder(args) = 웹 앱을 만들 "준비 도구(builder)"를 생성
//   - static 메서드라서 new 없이 클래스이름.메서드() 로 바로 호출
//   - args = 실행할 때 넘겨준 인자 (Java main의 String[] args)
var builder = WebApplication.CreateBuilder(args);

// builder.Services = 프레임워크가 대신 만들어서 관리해줄 객체 목록 (Spring의 빈 컨테이너)
// AddControllersWithViews() = "컨트롤러 + Razor 화면(View)을 쓸게요" 라고 등록
builder.Services.AddControllersWithViews();

// ---------------------------------------------------------------------
// ★ 의존성 주입(DI) 등록
//   "OrderService가 필요하면 프레임워크가 new 해서 넣어줘" 라고 미리 알려두는 곳.
//   Spring은 클래스에 @Service만 붙이면 자동 등록되지만, ASP.NET Core는 여기서 직접 등록해요.
//
//   AddScoped<T>() = 요청(브라우저의 요청 1번)마다 객체를 새로 하나 만들어서 그 요청 동안 재사용
//     (다른 선택지: AddSingleton = 앱 전체에 딱 하나, AddTransient = 필요할 때마다 매번 새로)
//   <T> 안의 꺾쇠 = 제네릭. "어떤 타입을 등록할지"를 알려주는 자리 (Java의 List<String>과 같은 문법)
// ---------------------------------------------------------------------
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ProductionService>();
builder.Services.AddScoped<OrderService>();

// 준비가 끝났으니 실제 앱 객체를 만듦
var app = builder.Build();

// 미들웨어 등록 — 요청이 컨트롤러에 도착하기 전에 거쳐 가는 단계들 (Spring의 Filter와 비슷)
app.UseStaticFiles();   // wwwroot 폴더의 css, js 파일을 브라우저가 받아갈 수 있게 열어줌
app.MapControllers();   // 컨트롤러에 붙은 [HttpGet("/")], [HttpPost("orders")] 같은 주소를 연결

// 웹 서버(Kestrel) 실행. 이 줄에서 프로그램이 멈춰서 요청을 계속 기다려요.
// (FastAPI의 uvicorn 실행, Spring Boot의 내장 톰캣 기동에 해당)
app.Run();
