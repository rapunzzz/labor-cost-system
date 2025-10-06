using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Models;
using LaborSystemServer.Service;
using LaborSystemServer.DTOs;
using ProductionPlanning.Models;
using DataAccess.Data;

namespace LaborSystemServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ExcelService _excelService;
        private readonly IProductionPlanningService _productionPlanningService;
        private readonly IWorkTimeService _workTimeService;
        private readonly IShiftConfigurationService _shiftConfigurationService;
        private readonly ApplicationDBContext _context;

        public HomeController(
            ILogger<HomeController> logger, 
            ExcelService excelService,
            IProductionPlanningService productionPlanningService,
            ApplicationDBContext context, IWorkTimeService workTimeService,
            IShiftConfigurationService shiftConfigurationService)
        {
            _logger = logger;
            _excelService = excelService;
            _productionPlanningService = productionPlanningService;
            _workTimeService = workTimeService;
            _shiftConfigurationService = shiftConfigurationService;
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
                
                await _context.SaveChangesAsync();
                
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

                var shiftMinutes = await _workTimeService.GetWorkMinutesPerShiftAsync();
                
                var shiftCapacitiesPerLine = new Dictionary<WorkType, double>();
                foreach (var shift in shiftMinutes)
                {
                    var regularMinutes = shift.Value.regularMinutes;
                    var fridayMinutes = shift.Value.fridayMinutes;
                    
                    double hoursPerLine = ((regularMinutes * hariKerja.TotalSeninKamis) + 
                                        (fridayMinutes * hariKerja.TotalJumat)) / 60.0;
                    
                    shiftCapacitiesPerLine[shift.Key] = hoursPerLine;
                }
                
                ViewBag.ShiftMinutes = shiftMinutes;
                ViewBag.ShiftCapacities = shiftCapacitiesPerLine;
                ViewBag.LineCount = (await _productionPlanningService.GetSortedLinesAsync()).Count;

                // GUNAKAN DATA DARI SERVICE
                var shiftTimelines = await _workTimeService.GetShiftTimelinesAsync();
                ViewBag.ShiftTimelines = shiftTimelines;

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