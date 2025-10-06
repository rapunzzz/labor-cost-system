using ProductionPlanning.Models;
using DataAccess.Data;
using Microsoft.EntityFrameworkCore;

namespace LaborSystemServer.Service
{
    public class TimeBlockDto
    {
        public string Type { get; set; } // "Work" or "Deduction"
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Name { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class ShiftTimelineDto
    {
        public WorkType WorkType { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public string ClassName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool HasFriday { get; set; }
        public List<TimeBlockDto> NormalDaysBlocks { get; set; }
        public List<TimeBlockDto> FridayBlocks { get; set; }
        public int NormalWorkMinutes { get; set; }
        public int FridayWorkMinutes { get; set; }
    }

    public interface IWorkTimeService
    {
        Task<Dictionary<WorkType, (int regularMinutes, int fridayMinutes)>> GetWorkMinutesPerShiftAsync();
        Task<List<ShiftTimelineDto>> GetShiftTimelinesAsync();
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

        public async Task<List<ShiftTimelineDto>> GetShiftTimelinesAsync()
        {
            var shiftConfigs = await _context.ShiftWorkConfigurations
                .Include(s => s.TimeDeductions)
                .OrderBy(s => s.WorkType)
                .ToListAsync();
            
            var result = new List<ShiftTimelineDto>();
            
            foreach (var config in shiftConfigs)
            {
                var displayInfo = GetShiftDisplayInfo(config.WorkType);
                var hasFriday = IsAffectedByFridayPrayer(config.WorkType);
                
                // Get valid deductions
                var normalDeductions = config.TimeDeductions?
                    .Where(d => IsDeductionInShiftRange(d, config, isFriday: false) && d.IsActive)
                    .OrderBy(d => d.StartTime)
                    .ToList() ?? new List<WorkTimeDeduction>();
                
                var fridayDeductions = config.TimeDeductions?
                    .Where(d => IsDeductionInShiftRange(d, config, isFriday: true) && d.IsActive)
                    .OrderBy(d => d.StartTime)
                    .ToList() ?? new List<WorkTimeDeduction>();
                
                // Calculate blocks
                var normalBlocks = CalculateTimeBlocks(config, normalDeductions, isFriday: false);
                var fridayBlocks = CalculateTimeBlocks(config, fridayDeductions, isFriday: true);
                
                // Calculate work minutes
                var normalWorkMinutes = CalculateNetMinutes(config, isFriday: false);
                var fridayWorkMinutes = CalculateNetMinutes(config, isFriday: true);
                
                result.Add(new ShiftTimelineDto
                {
                    WorkType = config.WorkType,
                    Title = displayInfo.title,
                    Icon = displayInfo.icon,
                    ClassName = displayInfo.className,
                    StartTime = config.StartTime,
                    EndTime = config.EndTime,
                    HasFriday = hasFriday,
                    NormalDaysBlocks = normalBlocks,
                    FridayBlocks = fridayBlocks,
                    NormalWorkMinutes = normalWorkMinutes,
                    FridayWorkMinutes = fridayWorkMinutes
                });
            }
            
            return result;
        }

        private List<TimeBlockDto> CalculateTimeBlocks(ShiftWorkConfiguration config, List<WorkTimeDeduction> deductions, bool isFriday)
        {
            var blocks = new List<TimeBlockDto>();
            var currentTime = config.StartTime;
            
            // Handle Friday Prayer overlap logic
            var fridayPrayer = isFriday 
                ? deductions.FirstOrDefault(d => d.Name?.Equals("Friday Prayer", StringComparison.OrdinalIgnoreCase) == true)
                : null;
            
            if (fridayPrayer != null)
            {
                // Filter out deductions that overlap with Friday Prayer
                var nonOverlappingDeductions = deductions
                    .Where(d => d == fridayPrayer || !IsOverlappingWithFridayPrayer(d, fridayPrayer))
                    .OrderBy(d => d.StartTime)
                    .ToList();
                
                deductions = nonOverlappingDeductions;
            }
            
            foreach (var deduction in deductions)
            {
                // Add work block before deduction
                if (currentTime < deduction.StartTime || 
                    (currentTime > deduction.StartTime && config.StartTime > config.EndTime))
                {
                    var workDuration = CalculateDuration(currentTime, deduction.StartTime);
                    blocks.Add(new TimeBlockDto
                    {
                        Type = "Work",
                        Start = currentTime,
                        End = deduction.StartTime,
                        Name = "Work",
                        DurationMinutes = workDuration
                    });
                }
                
                // Add deduction block
                var deductionDuration = CalculateDuration(deduction.StartTime, deduction.EndTime);
                blocks.Add(new TimeBlockDto
                {
                    Type = "Deduction",
                    Start = deduction.StartTime,
                    End = deduction.EndTime,
                    Name = deduction.Name,
                    DurationMinutes = deductionDuration
                });
                
                currentTime = deduction.EndTime;
            }
            
            // Add final work block
            if (currentTime != config.EndTime)
            {
                var workDuration = CalculateDuration(currentTime, config.EndTime);
                blocks.Add(new TimeBlockDto
                {
                    Type = "Work",
                    Start = currentTime,
                    End = config.EndTime,
                    Name = "Work",
                    DurationMinutes = workDuration
                });
            }
            
            return blocks;
        }

        private int CalculateDuration(TimeSpan start, TimeSpan end)
        {
            var duration = end - start;
            if (duration.TotalMinutes < 0)
            {
                duration += TimeSpan.FromDays(1);
            }
            return (int)duration.TotalMinutes;
        }
        
        private int CalculateNetMinutes(ShiftWorkConfiguration config, bool isFriday)
        {
            var duration = config.EndTime - config.StartTime;
            if (duration.TotalMinutes < 0)
            {
                duration += TimeSpan.FromDays(1);
            }
            var grossMinutes = (int)duration.TotalMinutes;
            
            var validDeductions = config.TimeDeductions?
                .Where(d => IsDeductionInShiftRange(d, config, isFriday) && d.IsActive)
                .ToList() ?? new List<WorkTimeDeduction>();
            
            var fridayPrayer = isFriday 
                ? validDeductions.FirstOrDefault(d => 
                    d.Name?.Equals("Friday Prayer", StringComparison.OrdinalIgnoreCase) == true)
                : null;
            
            int deductions = 0;
            
            if (fridayPrayer != null)
            {
                var fridayPrayerDuration = fridayPrayer.EndTime - fridayPrayer.StartTime;
                if (fridayPrayerDuration.TotalMinutes < 0)
                {
                    fridayPrayerDuration += TimeSpan.FromDays(1);
                }
                deductions += (int)fridayPrayerDuration.TotalMinutes;
                
                foreach (var d in validDeductions.Where(d => d != fridayPrayer))
                {
                    if (!IsOverlappingWithFridayPrayer(d, fridayPrayer))
                    {
                        var dedDuration = d.EndTime - d.StartTime;
                        if (dedDuration.TotalMinutes < 0)
                        {
                            dedDuration += TimeSpan.FromDays(1);
                        }
                        deductions += (int)dedDuration.TotalMinutes;
                    }
                }
            }
            else
            {
                deductions = validDeductions.Sum(d =>
                {
                    var dedDuration = d.EndTime - d.StartTime;
                    if (dedDuration.TotalMinutes < 0)
                    {
                        dedDuration += TimeSpan.FromDays(1);
                    }
                    return (int)dedDuration.TotalMinutes;
                });
            }
            
            return grossMinutes - deductions;
        }

        private bool IsOverlappingWithFridayPrayer(WorkTimeDeduction deduction, WorkTimeDeduction fridayPrayer)
        {
            var dedStart = deduction.StartTime;
            var dedEnd = deduction.EndTime;
            var fpStart = fridayPrayer.StartTime;
            var fpEnd = fridayPrayer.EndTime;
            
            if (dedEnd < dedStart) dedEnd += TimeSpan.FromDays(1);
            if (fpEnd < fpStart) fpEnd += TimeSpan.FromDays(1);
            
            var dedStartNorm = dedStart;
            var dedEndNorm = dedEnd < dedStart ? dedEnd + TimeSpan.FromDays(1) : dedEnd;
            var fpStartNorm = fpStart;
            var fpEndNorm = fpEnd < fpStart ? fpEnd + TimeSpan.FromDays(1) : fpEnd;
            
            return dedStartNorm < fpEndNorm && dedEndNorm > fpStartNorm;
        }

        private bool IsDeductionInShiftRange(WorkTimeDeduction deduction, ShiftWorkConfiguration config, bool isFriday)
        {
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
            
            return IsTimeInRange(deduction.StartTime, config.StartTime, config.EndTime);
        }
        
        private bool IsTimeInRange(TimeSpan timeToCheck, TimeSpan startTime, TimeSpan endTime)
        {
            if (endTime < startTime)
            {
                return timeToCheck >= startTime || timeToCheck < endTime;
            }
            else
            {
                return timeToCheck >= startTime && timeToCheck < endTime;
            }
        }
        
        private bool IsAffectedByFridayPrayer(WorkType workType)
        {
            return workType == WorkType.NonShift || workType == WorkType.Shift1;
        }

        private (string className, string title, string icon) GetShiftDisplayInfo(WorkType workType)
        {
            return workType switch
            {
                WorkType.NonShift => ("nonshift", "NonShift (Regular Hours)", "fas fa-sun"),
                WorkType.Shift1 => ("shift1", "Shift 1 (Morning Shift)", "fas fa-cloud-sun"),
                WorkType.Shift2 => ("shift2", "Shift 2 (Afternoon Shift)", "fas fa-cloud-moon"),
                WorkType.Shift3 => ("shift3", "Shift 3 (Night Shift)", "fas fa-moon"),
                _ => ("", "", "")
            };
        }
    }
}