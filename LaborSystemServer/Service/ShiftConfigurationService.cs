using ProductionPlanning.Models;
using LaborSystemServer.DTOs;
using DataAccess.Data;
using Microsoft.EntityFrameworkCore;

namespace LaborSystemServer.Service
{
    public interface IShiftConfigurationService
    {
        Task<List<ShiftWorkConfiguration>> GetAllShiftConfigurationsAsync();
        Task<ShiftWorkConfiguration> GetShiftConfigurationByIdAsync(int id);
        Task<ShiftWorkConfiguration> CreateShiftConfigurationAsync(ShiftConfigurationDto dto);
        Task<ShiftWorkConfiguration> UpdateShiftConfigurationAsync(int id, ShiftConfigurationDto dto);
        Task<bool> DeleteShiftConfigurationAsync(int id);
        
        // Work Time Deduction methods
        Task<List<WorkTimeDeduction>> GetDeductionsByShiftIdAsync(int shiftId);
        Task<WorkTimeDeduction> GetDeductionByIdAsync(int id);
        Task<WorkTimeDeduction> CreateDeductionAsync(WorkTimeDeductionDto dto);
        Task<WorkTimeDeduction> UpdateDeductionAsync(int id, WorkTimeDeductionDto dto);
        Task<bool> DeleteDeductionAsync(int id);
        Task<bool> ToggleDeductionActiveAsync(int id);
    
    }

    public class ShiftConfigurationService : IShiftConfigurationService
    {
        private readonly ApplicationDBContext _context;
        private readonly ILogger<ShiftConfigurationService> _logger;

        public ShiftConfigurationService(
            ApplicationDBContext context, 
            ILogger<ShiftConfigurationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<ShiftWorkConfiguration>> GetAllShiftConfigurationsAsync()
        {
            return await _context.ShiftWorkConfigurations
                .Include(s => s.TimeDeductions)
                .OrderBy(s => s.WorkType)
                .ToListAsync();
        }

        public async Task<ShiftWorkConfiguration> GetShiftConfigurationByIdAsync(int id)
        {
            return await _context.ShiftWorkConfigurations
                .Include(s => s.TimeDeductions)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<ShiftWorkConfiguration> CreateShiftConfigurationAsync(ShiftConfigurationDto dto)
        {
            // Check if configuration for this WorkType already exists
            var existing = await _context.ShiftWorkConfigurations
                .FirstOrDefaultAsync(s => s.WorkType == dto.WorkType);
            
            if (existing != null)
            {
                throw new InvalidOperationException($"Configuration for {dto.WorkType} already exists");
            }

            var config = new ShiftWorkConfiguration
            {
                WorkType = dto.WorkType,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime
            };

            _context.ShiftWorkConfigurations.Add(config);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created shift configuration for {dto.WorkType}");
            return config;
        }

        public async Task<ShiftWorkConfiguration> UpdateShiftConfigurationAsync(int id, ShiftConfigurationDto dto)
        {
            var config = await _context.ShiftWorkConfigurations.FindAsync(id);
            if (config == null)
            {
                throw new KeyNotFoundException($"Shift configuration with id {id} not found");
            }

            config.StartTime = dto.StartTime;
            config.EndTime = dto.EndTime;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated shift configuration {id}");
            
            return config;
        }

        public async Task<bool> DeleteShiftConfigurationAsync(int id)
        {
            var config = await _context.ShiftWorkConfigurations
                .Include(s => s.TimeDeductions)
                .FirstOrDefaultAsync(s => s.Id == id);
            
            if (config == null) return false;

            _context.ShiftWorkConfigurations.Remove(config);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Deleted shift configuration {id}");
            return true;
        }

        // Work Time Deduction methods
        public async Task<List<WorkTimeDeduction>> GetDeductionsByShiftIdAsync(int shiftId)
        {
            return await _context.WorkTimeDeductions
                .Where(d => d.ShiftWorkConfigurationId == shiftId)
                .OrderBy(d => d.StartTime.ToString())
                .ToListAsync();
        }

        public async Task<WorkTimeDeduction> GetDeductionByIdAsync(int id)
        {
            return await _context.WorkTimeDeductions.FindAsync(id);
        }

        public async Task<WorkTimeDeduction> CreateDeductionAsync(WorkTimeDeductionDto dto)
        {
            var shiftConfig = await _context.ShiftWorkConfigurations
                .FirstOrDefaultAsync(s => s.WorkType == dto.WorkType);
            
            if (shiftConfig == null)
            {
                throw new InvalidOperationException($"Shift configuration not found for WorkType: {dto.WorkType}");
            }

            var deduction = new WorkTimeDeduction
            {
                Name = dto.Name,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                WorkType = dto.WorkType,
                ShiftWorkConfigurationId = shiftConfig.Id 
            };

            _context.WorkTimeDeductions.Add(deduction);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created time deduction '{dto.Name}' for WorkType {dto.WorkType} (ShiftConfigId: {shiftConfig.Id})");
            return deduction;
        }

        public async Task<WorkTimeDeduction> UpdateDeductionAsync(int id, WorkTimeDeductionDto dto)
        {
            var deduction = await _context.WorkTimeDeductions.FindAsync(id);
            if (deduction == null)
            {
                throw new KeyNotFoundException($"Time deduction with id {id} not found");
            }

            deduction.Name = dto.Name;
            deduction.StartTime = dto.StartTime;
            deduction.EndTime = dto.EndTime;
            deduction.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated time deduction {id}");
            
            return deduction;
        }

        public async Task<bool> DeleteDeductionAsync(int id)
        {
            var deduction = await _context.WorkTimeDeductions.FindAsync(id);
            if (deduction == null) return false;

            _context.WorkTimeDeductions.Remove(deduction);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Deleted time deduction {id}");
            return true;
        }

        public async Task<bool> ToggleDeductionActiveAsync(int id)
        {
            var deduction = await _context.WorkTimeDeductions.FindAsync(id);
            if (deduction == null) return false;

            deduction.IsActive = !deduction.IsActive; // Assuming IsActive is a property of WorkTimeDeduction
            _context.WorkTimeDeductions.Update(deduction);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Toggled active status for time deduction {id} to {deduction.IsActive}");
            return true;
        }
    }
}