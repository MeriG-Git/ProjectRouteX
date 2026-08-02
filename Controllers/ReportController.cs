using Microsoft.AspNetCore.Mvc;

namespace RouteXWms.Controllers
{
    /// <summary>
    /// 各種帳票・レポート画面表示用コントローラー
    /// </summary>
    public class ReportController : Controller
    {
        /// <summary>
        /// レポートトップ画面を表示します。
        /// </summary>
        /// <returns>レポート画面ビュー</returns>
        public IActionResult Index()
        {
            return View();
        }
    }
}
