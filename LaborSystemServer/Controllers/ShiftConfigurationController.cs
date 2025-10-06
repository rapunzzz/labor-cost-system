using Microsoft.AspNetCore.Mvc;
using LaborSystemServer.Service;
using LaborSystemServer.DTOs;
using ProductionPlanning.Models;

namespace LaborSystemServer.Controllers
{
    public class ShiftConfigurationController : Controller
    {
        private readonly IShiftConfigurationService _shiftConfigService;
        private readonly ILogger<ShiftConfigurationController> _logger;

        public ShiftConfigurationController(
            IShiftConfigurationService shiftConfigService,
            ILogger<ShiftConfigurationController> logger)
        {
            _shiftConfigService = shiftConfigService;
            _logger = logger;
        }

        // GET: ShiftConfiguration/Index
        public async Task<IActionResult> Index()
        {
            try
            {
                var configs = await _shiftConfigService.GetAllShiftConfigurationsAsync();
                var viewModels = configs.Select(c => new ShiftConfigurationViewModel
                {
                    Id = c.Id,
                    WorkType = c.WorkType,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    TimeDeductions = c.TimeDeductions.Select(d => new WorkTimeDeductionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime,
                        IsActive = d.IsActive,
                        ShiftWorkConfigurationId = d.ShiftWorkConfigurationId
                    }).ToList()
                }).ToList();
                
                // Load WorkTypes untuk modal Create & Edit
                ViewBag.WorkTypes = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
                
                return View(viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shift configurations");
                ViewBag.Error = "Failed to load shift configurations";
                ViewBag.WorkTypes = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
                return View(new List<ShiftConfigurationViewModel>());
            }
        }

        // POST: ShiftConfiguration/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShiftConfigurationDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Invalid input data. Please check your entries.";
                ViewBag.WorkTypes = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
                
                var configs = await _shiftConfigService.GetAllShiftConfigurationsAsync();
                var viewModels = configs.Select(c => new ShiftConfigurationViewModel
                {
                    Id = c.Id,
                    WorkType = c.WorkType,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    TimeDeductions = c.TimeDeductions.Select(d => new WorkTimeDeductionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime,
                        IsActive = d.IsActive,
                        ShiftWorkConfigurationId = d.ShiftWorkConfigurationId
                    }).ToList()
                }).ToList();
                
                return View("Index", viewModels);
            }

            try
            {
                await _shiftConfigService.CreateShiftConfigurationAsync(dto);
                TempData["Success"] = "Shift configuration created successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ViewBag.Error = ex.Message;
                ViewBag.WorkTypes = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
                
                var configs = await _shiftConfigService.GetAllShiftConfigurationsAsync();
                var viewModels = configs.Select(c => new ShiftConfigurationViewModel
                {
                    Id = c.Id,
                    WorkType = c.WorkType,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    TimeDeductions = c.TimeDeductions.Select(d => new WorkTimeDeductionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime,
                        IsActive = d.IsActive,
                        ShiftWorkConfigurationId = d.ShiftWorkConfigurationId
                    }).ToList()
                }).ToList();
                
                return View("Index", viewModels);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shift configuration");
                ViewBag.Error = "Failed to create shift configuration";
                ViewBag.WorkTypes = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
                
                var configs = await _shiftConfigService.GetAllShiftConfigurationsAsync();
                var viewModels = configs.Select(c => new ShiftConfigurationViewModel
                {
                    Id = c.Id,
                    WorkType = c.WorkType,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    TimeDeductions = c.TimeDeductions.Select(d => new WorkTimeDeductionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime,
                        IsActive = d.IsActive,
                        ShiftWorkConfigurationId = d.ShiftWorkConfigurationId
                    }).ToList()
                }).ToList();
                
                return View("Index", viewModels);
            }
        }

        // POST: ShiftConfiguration/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShiftConfigurationDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.EditError = "Invalid input data. Please check your entries.";
                ViewBag.WorkTypes = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
                
                var configs = await _shiftConfigService.GetAllShiftConfigurationsAsync();
                var viewModels = configs.Select(c => new ShiftConfigurationViewModel
                {
                    Id = c.Id,
                    WorkType = c.WorkType,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    TimeDeductions = c.TimeDeductions.Select(d => new WorkTimeDeductionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime,
                        IsActive = d.IsActive,
                        ShiftWorkConfigurationId = d.ShiftWorkConfigurationId
                    }).ToList()
                }).ToList();
                
                return View("Index", viewModels);
            }

            try
            {
                await _shiftConfigService.UpdateShiftConfigurationAsync(id, dto);
                TempData["Success"] = "Shift configuration updated successfully";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Shift configuration not found";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shift configuration");
                ViewBag.EditError = "Failed to update shift configuration";
                ViewBag.WorkTypes = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
                
                var configs = await _shiftConfigService.GetAllShiftConfigurationsAsync();
                var viewModels = configs.Select(c => new ShiftConfigurationViewModel
                {
                    Id = c.Id,
                    WorkType = c.WorkType,
                    StartTime = c.StartTime,
                    EndTime = c.EndTime,
                    TimeDeductions = c.TimeDeductions.Select(d => new WorkTimeDeductionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        StartTime = d.StartTime,
                        EndTime = d.EndTime,
                        IsActive = d.IsActive,
                        ShiftWorkConfigurationId = d.ShiftWorkConfigurationId
                    }).ToList()
                }).ToList();
                
                return View("Index", viewModels);
            }
        }

        // POST: ShiftConfiguration/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var result = await _shiftConfigService.DeleteShiftConfigurationAsync(id);
                if (result)
                {
                    TempData["Success"] = "Shift configuration deleted successfully";
                }
                else
                {
                    TempData["Error"] = "Shift configuration not found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shift configuration");
                TempData["Error"] = "Failed to delete shift configuration";
            }

            return RedirectToAction(nameof(Index));
        }       

        // =========================
        // WORK TIME DEDUCTION CRUD
        // =========================

        // POST: ShiftConfiguration/CreateDeduction
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDeduction(WorkTimeDeductionDto dto)
        {
            Console.WriteLine(dto.Name);
            Console.WriteLine(dto.WorkType);
            if (!ModelState.IsValid)
            {
                 foreach (var entry in ModelState)
                {
                    var key = entry.Key;
                    var errors = entry.Value.Errors;
                    foreach (var error in errors)
                    {
                        Console.WriteLine($"Property: {key}, Error: {error.ErrorMessage}");
                    }
                }
                TempData["Error"] = "Invalid deduction data. Please check your entries.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _shiftConfigService.CreateDeductionAsync(dto);
                TempData["Success"] = "Time deduction added successfully";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating deduction");
                TempData["Error"] = $"Failed to create time deduction: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: ShiftConfiguration/EditDeduction/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditDeduction(int id, WorkTimeDeductionDto dto)
        {
            Console.WriteLine(ModelState);
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid deduction data. Please check your entries.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _shiftConfigService.UpdateDeductionAsync(id, dto);
                TempData["Success"] = "Time deduction updated successfully";
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Time deduction not found";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating deduction");
                TempData["Error"] = $"Failed to update time deduction: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: ShiftConfiguration/DeleteDeduction/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDeduction(int id, int shiftId)
        {
            try
            {
                var result = await _shiftConfigService.DeleteDeductionAsync(id);
                if (result)
                {
                    TempData["Success"] = "Time deduction deleted successfully";
                }
                else
                {
                    TempData["Error"] = "Time deduction not found";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting deduction");
                TempData["Error"] = $"Failed to delete time deduction: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: ShiftConfiguration/ToggleDeductionActive/5
        [HttpPost]
        public async Task<IActionResult> ToggleDeductionActive(int id)
        {
            try
            {
                await _shiftConfigService.ToggleDeductionActiveAsync(id);
                return RedirectToAction(nameof(Index));
            }
           catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling deduction active status");
                return Json(new { success = false, message = "Error updating status" });
            }
        }
    }
}