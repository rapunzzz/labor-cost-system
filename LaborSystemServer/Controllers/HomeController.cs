using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Models;
using LaborSystemServer.Service;
using ProductionPlanning.Models;
using DataAccess.Data;

namespace LaborSystemServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ExcelService _excelService;
        private readonly IProductionPlanningService _productionPlanningService;
        private readonly ApplicationDBContext _context;

        public HomeController(
            ILogger<HomeController> logger, 
            ExcelService excelService,
            IProductionPlanningService productionPlanningService,
            ApplicationDBContext context)
        {
            _logger = logger;
            _excelService = excelService;
            _productionPlanningService = productionPlanningService;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file, int month, int year)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Please upload a valid Excel file.";
                return View("Index");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var dataExcel = _excelService.ReadExcel(stream, month, year);
                var hariKerja = _excelService.GetHariKerja(month, year);
                
                // Save Excel data to database first
                await _context.SaveChangesAsync();
                
                // Automatically generate production plan after successful upload
                var productionPlan1 = await _productionPlanningService.GenerateProductionPlanAsync(month, year, AllocationMethod.NonShiftWithOvertime);
                var productionPlan2 = await _productionPlanningService.GenerateProductionPlanAsync(month, year, AllocationMethod.MultiShift);
                
                ViewBag.Results = new {
                    Data = dataExcel,
                    Month = month,
                    Year = year,
                    HariKerja = hariKerja
                };
                
                ViewBag.ProductionPlan1 = productionPlan1;
                ViewBag.ProductionPlan2 = productionPlan2;

                ViewBag.Success = "Excel file uploaded and production plan generated successfully!";
                
                return View("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading Excel file and generating production plan");
                ViewBag.Error = $"Error processing Excel file: {ex.Message}";
                return View("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerateProductionPlan(int month, int year)
        {
            try
            {
                var productionPlan = await _productionPlanningService.GenerateProductionPlanAsync(month, year);
                
                ViewBag.ProductionPlan = productionPlan;
                ViewBag.Success = $"Production plan generated successfully for {month:D2}/{year}";

                return View("ProductionPlan", productionPlan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating production plan for {Month}/{Year}", month, year);
                ViewBag.Error = $"Error generating production plan: {ex.Message}";
                return View("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ViewProductionPlan(int month, int year)
        {
            try
            {
                var productionPlan = await _productionPlanningService.GenerateProductionPlanAsync(month, year);
                return View("ProductionPlan", productionPlan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing production plan for {Month}/{Year}", month, year);
                ViewBag.Error = $"Error loading production plan: {ex.Message}";
                return View("Index");
            }
        }

    }

    // ViewModel for reports
    public class ProductionReportViewModel
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public List<LineUtilization> LineUtilizations { get; set; } = new();
        public List<UnassignedModel> UnassignedModels { get; set; } = new();
        
        public double TotalCapacityHours => LineUtilizations.Sum(l => l.MaxHours);
        public double TotalUsedHours => LineUtilizations.Sum(l => l.UsedHours);
        public double TotalAvailableHours => LineUtilizations.Sum(l => l.AvailableHours);
        public double OverallUtilizationPercentage => TotalCapacityHours > 0 ? (TotalUsedHours / TotalCapacityHours) * 100 : 0;
        public double TotalUnassignedHours => UnassignedModels.Sum(u => u.RequiredHours);
    }
}