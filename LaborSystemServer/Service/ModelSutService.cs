using Microsoft.EntityFrameworkCore;
using DataAccess.Data;
using ProductionPlanning.Models;
using OfficeOpenXml;

namespace LaborSystemServer.Service
{
    public interface IModelSutService
    {
        Task<List<ModelReference>> GetAllModelReferencesAsync();
        Task<UploadResult> UploadExcelAsync(IFormFile file);
    }

    public class ModelSutService : IModelSutService
    {
        private readonly ApplicationDBContext _context;
        private readonly ILogger<ModelSutService> _logger;

        public ModelSutService(ApplicationDBContext context, ILogger<ModelSutService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ModelReference>> GetAllModelReferencesAsync()
        {
            return await _context.ModelReferences
                .OrderBy(m => m.ModelName)
                .ToListAsync();
        }

        public async Task<UploadResult> UploadExcelAsync(IFormFile file)
        {
            try
            {
                ExcelPackage.License.SetNonCommercialPersonal("PMI");

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        var rowCount = worksheet.Dimension?.Rows ?? 0;

                        if (rowCount < 2)
                        {
                            return new UploadResult
                            {
                                IsSuccess = false,
                                Message = "Excel file is empty or has no data rows."
                            };
                        }

                        var newModels = new List<ModelReference>();
                        var errorCount = 0;

                        // Read all data from Excel first
                        for (int row = 2; row <= rowCount; row++)
                        {
                            try
                            {
                                var modelName = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                                var sutValue = worksheet.Cells[row, 2].Value?.ToString();
                                var headCountValue = worksheet.Cells[row, 3].Value?.ToString();

                                // Skip empty rows
                                if (string.IsNullOrWhiteSpace(modelName))
                                    continue;

                                if (!double.TryParse(sutValue, out double sut))
                                {
                                    _logger.LogWarning($"Invalid SUT value at row {row}: {sutValue}");
                                    errorCount++;
                                    continue;
                                }

                                if (!int.TryParse(headCountValue, out int headCount))
                                {
                                    _logger.LogWarning($"Invalid HeadCount value at row {row}: {headCountValue}");
                                    errorCount++;
                                    continue;
                                }

                                newModels.Add(new ModelReference
                                {
                                    ModelName = modelName,
                                    SUT = sut,
                                    HeadCount = headCount
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error processing row {row}: {ex.Message}");
                                errorCount++;
                            }
                        }

                        if (newModels.Count == 0)
                        {
                            return new UploadResult
                            {
                                IsSuccess = false,
                                Message = "No valid data found in Excel file."
                            };
                        }

                        // Delete all existing records
                        var existingAssignments = await _context.ModelDatas.ToListAsync();
                        _context.ModelDatas.RemoveRange(existingAssignments);

                        var existingModels = await _context.ModelReferences.ToListAsync();
                        _context.ModelReferences.RemoveRange(existingModels);
                        
                        // Add all new records
                        await _context.ModelReferences.AddRangeAsync(newModels);
                        
                        // Save changes
                        await _context.SaveChangesAsync();

                        var message = $"Upload completed: {newModels.Count} records added (replaced all existing data)";
                        if (errorCount > 0)
                            message += $", {errorCount} rows skipped due to errors";

                        return new UploadResult
                        {
                            IsSuccess = true,
                            Message = message,
                            AddedCount = newModels.Count,
                            ErrorCount = errorCount
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading Excel: {ex.Message}");
                return new UploadResult
                {
                    IsSuccess = false,
                    Message = $"Error processing file: {ex.Message}"
                };
            }
        }
    }

    public class UploadResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public int AddedCount { get; set; }
        public int ErrorCount { get; set; }
    }
}