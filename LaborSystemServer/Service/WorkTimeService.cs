using ProductionPlanning.Models;
using DataAccess.Data;
using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace LaborSystemServer.Service
{
    public interface IWorkTimeService
    {
        Task<Dictionary<WorkType, (int regularMinutes, int fridayMinutes)>> GetWorkMinutesPerShiftAsync();
    }

    public class WorkTimeService : IWorkTimeService
    {
        private readonly ApplicationDBContext _context;
        
        public WorkTimeService(ApplicationDBContext context)
        {
            _context = context;
        }
        
        public async Task<Dictionary<WorkType, (int regularMinutes, int fridayMinutes)>> GetWorkMinutesPerShiftAsync()
        {
            var shiftConfigs = await _context.ShiftWorkConfigurations
                .Include(s => s.TimeDeductions)
                .ToListAsync();
            
            var result = new Dictionary<WorkType, (int regularMinutes, int fridayMinutes)>();
            
            foreach (var config in shiftConfigs)
            {
                var regularMinutes = CalculateNetMinutes(config, isFriday: false);
                var fridayMinutes = CalculateNetMinutes(config, isFriday: true);
                
                result[config.WorkType] = (regularMinutes, fridayMinutes);
            }
            
            return result;
        }
        
        private int CalculateNetMinutes(ShiftWorkConfiguration config, bool isFriday)
        {
            // Hitung grossMinutes dengan logic lintas hari
            var duration = config.EndTime - config.StartTime;
            if (duration.TotalMinutes < 0)
            {
                duration += TimeSpan.FromDays(1);
            }
            var grossMinutes = (int)duration.TotalMinutes;
            
            // Hitung deductions dengan logic lintas hari dan filter dalam range shift
            var deductions = config.TimeDeductions?
                .Where(d => IsDeductionInShiftRange(d, config, isFriday) && d.IsActive)
                .Sum(d =>
                {
                    var dedDuration = d.EndTime - d.StartTime;
                    if (dedDuration.TotalMinutes < 0)
                    {
                        dedDuration += TimeSpan.FromDays(1);
                    }
                    return (int)dedDuration.TotalMinutes;
                }) ?? 0;
            
            return grossMinutes - deductions;
        }

        private bool IsDeductionInShiftRange(WorkTimeDeduction deduction, ShiftWorkConfiguration config, bool isFriday)
        {
            // Jika bukan Friday dan deduction adalah "Friday Prayer", skip
            if (!isFriday && deduction.Name?.Equals("Friday Prayer", StringComparison.OrdinalIgnoreCase) == true)
            {
                return false;
            }
            
            if (isFriday && deduction.Name?.Equals("Friday Prayer", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (!IsAffectedByFridayPrayer(config.WorkType))
                {
                    return false;
                }
            }
            
            // Cek apakah deduction berada dalam range shift time
            return IsTimeInRange(deduction.StartTime, config.StartTime, config.EndTime);
        }
        
        private bool IsTimeInRange(TimeSpan timeToCheck, TimeSpan startTime, TimeSpan endTime)
        {
            // Handle kasus lintas hari (misal shift malam 22:00 - 06:00)
            if (endTime < startTime)
            {
                // Shift lintas hari: timeToCheck valid jika >= startTime ATAU < endTime
                return timeToCheck >= startTime || timeToCheck < endTime;
            }
            else
            {
                // Shift normal: timeToCheck harus >= startTime DAN < endTime
                return timeToCheck >= startTime && timeToCheck < endTime;
            }
        }
        
        private bool IsAffectedByFridayPrayer(WorkType workType)
        {
            return workType == WorkType.NonShift || workType == WorkType.Shift1;
        }
    }
}