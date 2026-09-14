using Microsoft.AspNetCore.Mvc;

namespace Warehouse3D.Controllers;

// Trang chọn kho — hiện tại chỉ có PMC, sau này thêm kho khác thì thêm card ở Views/Home/Index.cshtml.
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
