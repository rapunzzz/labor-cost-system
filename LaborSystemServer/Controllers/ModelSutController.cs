using Microsoft.AspNetCore.Mvc;
using ProductionPlanning.Models;
using LaborSystemServer.Service;

namespace LaborSystemServer.Controllers
{
    public class ModelSutController : Controller
    {
        private readonly IModelSutService _modelSutService;
        private readonly ILogger<ModelSutController> _logger;

        public ModelSutController(IModelSutService modelSutService, ILogger<ModelSutController> logger)
        {
            _modelSutService = modelSutService;
            _logger = logger;
        }

        // GET: ModelSut/Index
        public async Task<IActionResult> Index()
        {
            var modelReferences = await _modelSutService.GetAllModelReferencesAsync();
            return View(modelReferences);
        }

        // POST: ModelSut/UploadExcel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                return RedirectToAction(nameof(Index));
            }

            if (!file.FileName.EndsWith(".xlsx") && !file.FileName.EndsWith(".xls"))
            {
                TempData["Error"] = "Invalid file format. Please upload an Excel file (.xlsx or .xls).";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var result = await _modelSutService.UploadExcelAsync(file);

                if (result.IsSuccess)
                {
                    TempData["Success"] = result.Message;
                    _logger.LogInformation(result.Message);
                }
                else
                {
                    TempData["Error"] = result.Message;
                    _logger.LogWarning(result.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading Excel: {ex.Message}");
                TempData["Error"] = $"Error processing file: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}