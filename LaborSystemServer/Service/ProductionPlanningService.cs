using ProductionPlanning.Models;
using Microsoft.EntityFrameworkCore;
using DataAccess.Data;

namespace LaborSystemServer.Service
{
    public interface IProductionPlanningService
    {
        Task<ProductionPlanResult> GenerateProductionPlanAsync(int month, int year, AllocationMethod method = AllocationMethod.MultiShift);
        Task<List<OptimizedLineCapacity>> GetOptimizedCapacitiesAsync(int month, int year);
    }

    public class ProductionPlanningService : IProductionPlanningService
    {
        private readonly ApplicationDBContext _context;
        private readonly ILogger<ProductionPlanningService> _logger;
        private const double CHANGEOVER_TIME_HOURS = 15.0 / 60.0; 
        
        public ProductionPlanningService(ApplicationDBContext context, ILogger<ProductionPlanningService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ProductionPlanResult> GenerateProductionPlanAsync(int month, int year, AllocationMethod method = AllocationMethod.MultiShift)
        {
            // Clear existing assignments for this period
            await ClearExistingAssignmentsAsync(month, year);

            // Get work days and calculate work hours per shift
            var workDays = GetWorkDays(month, year);
            var shiftCapacities = method == AllocationMethod.NonShiftWithOvertime 
                ? CalculateNonShiftCapacities(workDays)
                : CalculateShiftCapacities(workDays);
            
            // Get data
            var models = await GetSortedModelsAsync(month, year);
            var lines = await GetSortedLinesAsync();
            var optimizedCapacities = await GetOptimizedCapacitiesAsync(month, year);

            var result = new ProductionPlanResult
            {
                Month = month,
                Year = year,
                AllocationMethod = method,
                TotalWorkHoursPerLine = shiftCapacities.Values.Sum(),
                TotalDemandHours = models.Sum(m => m.TotalWorkHours),
                TotalCapacityHours = lines.Count * shiftCapacities.Values.Sum(),
                Assignments = new List<ProductionAssignment>(),
                UnassignedModels = new List<UnassignedModel>(),
                ShiftCapacities = shiftCapacities,
                LineCount = lines.Count
            };

            // Calculate gap
            result.CapacityGap = result.TotalCapacityHours - result.TotalDemandHours;
            
            // For NonShift method, calculate required overtime upfront
            if (method == AllocationMethod.NonShiftWithOvertime)
            {
                var regularCapacity = lines.Count * shiftCapacities[WorkType.NonShift];
                result.RequiredOvertimeHours = Math.Max(0, result.TotalDemandHours - regularCapacity);
            }
            
            // Allocate models based on method
            if (method == AllocationMethod.NonShiftWithOvertime)
            {
                await AllocateModelsNonShiftWithOvertimeAsync(models, lines, shiftCapacities, result, optimizedCapacities);
            }
            else
            {
                await AllocateModelsWithMultiShiftAsync(models, lines, shiftCapacities, result, optimizedCapacities);
            }

            await OptimizeLineCapacitiesAsync(month, year, result.Assignments);

            // Save assignments to database
            if (result.Assignments.Any())
            {
                Console.WriteLine($"Regular assignments: {result.Assignments.Count}");
                _context.ProductionAssignments.AddRange(result.Assignments);
            }

            if (result.OvertimeAssignments.Any())
            {
                Console.WriteLine($"Overtime assignments: {result.OvertimeAssignments.Count}");
                Console.WriteLine($"Total overtime hours used: {result.ActualOvertimeHours:F2}");
                Console.WriteLine($"Required overtime hours: {result.RequiredOvertimeHours:F2}");
                _context.OvertimeProductionAssignments.AddRange(result.OvertimeAssignments);
            }

            var changes = await _context.SaveChangesAsync();
            Console.WriteLine($"Rows affected: {changes}");

            result.WorkerOptimization = await GetWorkerOptimizationSummaryAsync(month, year);

            return result;
        }

        private Dictionary<WorkType, double> CalculateShiftCapacities(WorkDayInfo workDays)
        {
            var (nonshiftRegular, nonshiftFriday) = ShiftWorkConfiguration.GetWorkMinutesPerShift(WorkType.NonShift);
            var (shift1Regular, shift1Friday) = ShiftWorkConfiguration.GetWorkMinutesPerShift(WorkType.Shift1);
            var (shift2Regular, shift2Friday) = ShiftWorkConfiguration.GetWorkMinutesPerShift(WorkType.Shift2);
            var (shift3Regular, shift3Friday) = ShiftWorkConfiguration.GetWorkMinutesPerShift(WorkType.Shift3);

            return new Dictionary<WorkType, double>
            {
                { WorkType.Shift1, ((workDays.RegularDays * shift1Regular) + (workDays.FridayDays * shift1Friday)) / 60.0 },
                { WorkType.Shift2, ((workDays.RegularDays * shift2Regular) + (workDays.FridayDays * shift2Friday)) / 60.0 },
                { WorkType.Shift3, ((workDays.RegularDays * shift3Regular) + (workDays.FridayDays * shift3Friday)) / 60.0 }
            };

        }

        private async Task AllocateModelsWithMultiShiftAsync(
            List<ModelData> models,
            List<LineConfiguration> lines,
            Dictionary<WorkType, double> shiftCapacities,
            ProductionPlanResult result,
            List<OptimizedLineCapacity> optimizedCapacities)
        {
            // Track utilization per line per shift
            var lineShiftUtilization = InitializeLineShiftUtilization(lines);
            var lineAssignedModels = lines.ToDictionary(l => l.Id, l => new Dictionary<WorkType, List<string>>
            {
                { WorkType.Shift1, new List<string>() },
                { WorkType.Shift2, new List<string>() },
                { WorkType.Shift3, new List<string>() }
            });

            foreach (var model in models)
            {
                var remainingQuantity = model.Quantity;
                var modelWorkHoursPerUnit = model.TotalWorkHours / model.Quantity;

                // Try to allocate in shift priority: Shift1 → Shift2 → Shift3
                var shiftPriority = new[] { WorkType.Shift1, WorkType.Shift2, WorkType.Shift3 };

                foreach (var shift in shiftPriority)
                {
                    if (remainingQuantity <= 0) break;

                    remainingQuantity = await AllocateToShiftAsync(
                        model, remainingQuantity, modelWorkHoursPerUnit, shift,
                        lines, lineShiftUtilization, lineAssignedModels,
                        shiftCapacities[shift], result, optimizedCapacities);
                }

                // If still have remaining quantity, add to unassigned
                if (remainingQuantity > 0)
                {
                    result.UnassignedModels.Add(new UnassignedModel
                    {
                        ModelName = model.ModelName,
                        UnassignedQuantity = remainingQuantity,
                        RequiredHours = remainingQuantity * modelWorkHoursPerUnit,
                        RequiredHeadCount = model.ModelReference.HeadCount,
                        Reason = "Insufficient capacity across all shifts"
                    });
                }
            }
        }

        private async Task<int> AllocateToShiftAsync(
            ModelData model, int remainingQuantity, double modelWorkHoursPerUnit, WorkType shift,
            List<LineConfiguration> lines, Dictionary<int, Dictionary<WorkType, double>> lineShiftUtilization,
            Dictionary<int, Dictionary<WorkType, List<string>>> lineAssignedModels,
            double maxHoursPerShift, ProductionPlanResult result,
            List<OptimizedLineCapacity> optimizedCapacities)
        {
            while (remainingQuantity > 0)
            {
                // Find suitable lines for this shift - UPDATE: pass shift parameter
                var suitableLines = lines
                    .Where(l => GetLineCapacity(l, shift, optimizedCapacities) >= model.ModelReference.HeadCount)
                    .OrderBy(l => GetLineCapacity(l, shift, optimizedCapacities))
                    .ThenBy(l => lineShiftUtilization[l.Id][shift])
                    .ToList();

                var assigned = false;
                foreach (var line in suitableLines)
                {
                    var lineCapacity = GetLineCapacity(line, shift, optimizedCapacities); // UPDATE: pass shift parameter
                    
                    // Calculate changeover time for this shift
                    var changeoverTime = CalculateChangeoverTimeForShift(line.Id, model.ModelName, shift, lineAssignedModels);
                    
                    // Available hours for this shift
                    var availableHours = maxHoursPerShift - lineShiftUtilization[line.Id][shift] - changeoverTime;
                    if (availableHours <= 0) continue;

                    // Calculate assignable quantity
                    var maxUnitsCanFit = (int)Math.Floor(availableHours / modelWorkHoursPerUnit);
                    var unitsToAssign = Math.Min(remainingQuantity, maxUnitsCanFit);
                    
                    if (unitsToAssign > 0)
                    {
                        var plannedHours = unitsToAssign * modelWorkHoursPerUnit;
                        var totalHoursWithChangeover = plannedHours + changeoverTime;
                        var surplusWorkers = lineCapacity - model.ModelReference.HeadCount;

                        var assignment = new ProductionAssignment
                        {
                            ModelDataId = model.Id,
                            LineId = line.Id,
                            AssignedQuantity = unitsToAssign,
                            PlannedHours = plannedHours,
                            ChangeoverHours = changeoverTime,
                            RequiredWorkers = model.ModelReference.HeadCount,
                            ActualAllocatedWorkers = lineCapacity,
                            SurplusWorkers = surplusWorkers,
                            DefaultCapacity = line.DefaultCapacity,
                            AssignedShift = shift,
                            CreatedDate = DateTime.UtcNow
                        };

                        result.Assignments.Add(assignment);
                        
                        // Update utilization for this shift
                        lineShiftUtilization[line.Id][shift] += totalHoursWithChangeover;
                        
                        // Track assigned models for changeover calculation
                        if (!lineAssignedModels[line.Id][shift].Contains(model.ModelName))
                        {
                            lineAssignedModels[line.Id][shift].Add(model.ModelName);
                        }

                        remainingQuantity -= unitsToAssign;
                        assigned = true;
                        break;
                    }
                }

                if (!assigned)
                {
                    // Cannot assign more to this shift, break and try next shift
                    break;
                }
            }

            return remainingQuantity;
        }

        private Dictionary<WorkType, double> CalculateNonShiftCapacities(WorkDayInfo workDays)
        {
            var (nonshiftRegular, nonshiftFriday) = ShiftWorkConfiguration.GetWorkMinutesPerShift(WorkType.NonShift);
            
            var regularHours = ((workDays.RegularDays * nonshiftRegular) + (workDays.FridayDays * nonshiftFriday)) / 60.0;
            // NOTE: 500 hours adalah total overtime budget untuk semua lines, akan didistribusikan saat alokasi
            
            return new Dictionary<WorkType, double>
            {
                { WorkType.NonShift, regularHours } // Only regular hours per line
            };
        }

        private async Task AllocateModelsNonShiftWithOvertimeAsync(
            List<ModelData> models,
            List<LineConfiguration> lines,
            Dictionary<WorkType, double> shiftCapacities,
            ProductionPlanResult result,
            List<OptimizedLineCapacity> optimizedCapacities)
        {
            var regularHoursPerLine = shiftCapacities[WorkType.NonShift];
            // var totalRegularCapacity = lines.Count * regularHoursPerLine;
            // var totalDemand = models.Sum(m => m.TotalWorkHours);
            
            // Calculate required overtime hours
            // var capacityShortage = Math.Max(0, totalDemand - totalRegularCapacity);
            
            var lineUtilization = lines.ToDictionary(l => l.Id, l => 0.0);
            var lineAssignedModels = lines.ToDictionary(l => l.Id, l => new List<string>());

            // PHASE 1: Allocate to regular hours first
            foreach (var model in models)
            {
                var remainingQuantity = model.Quantity;
                var modelWorkHoursPerUnit = model.TotalWorkHours / model.Quantity;

                while (remainingQuantity > 0)
                {
                    var suitableLines = lines
                        .Where(l => GetLineCapacity(l, WorkType.NonShift, optimizedCapacities) >= model.ModelReference.HeadCount)
                        .Where(l => lineUtilization[l.Id] < regularHoursPerLine) // Only lines with regular hours available
                        .OrderBy(l => GetLineCapacity(l, WorkType.NonShift, optimizedCapacities))
                        .ThenBy(l => lineUtilization[l.Id])
                        .ToList();

                    var assigned = false;
                    foreach (var line in suitableLines)
                    {
                        var lineCapacity = GetLineCapacity(line, WorkType.NonShift, optimizedCapacities);
                        var changeoverTime = lineAssignedModels[line.Id].Count == 0 ? 0.0 : CHANGEOVER_TIME_HOURS;
                        var availableRegularHours = regularHoursPerLine - lineUtilization[line.Id] - changeoverTime;
                        
                        if (availableRegularHours <= 0) continue;

                        var maxUnitsCanFit = (int)Math.Floor(availableRegularHours / modelWorkHoursPerUnit);
                        var unitsToAssign = Math.Min(remainingQuantity, maxUnitsCanFit);
                        
                        if (unitsToAssign > 0)
                        {
                            var plannedHours = unitsToAssign * modelWorkHoursPerUnit;
                            var totalHoursWithChangeover = plannedHours + changeoverTime;
                            var surplusWorkers = lineCapacity - model.ModelReference.HeadCount;

                            var assignment = new ProductionAssignment
                            {
                                ModelDataId = model.Id,
                                LineId = line.Id,
                                AssignedQuantity = unitsToAssign,
                                PlannedHours = plannedHours,
                                ChangeoverHours = changeoverTime,
                                RequiredWorkers = model.ModelReference.HeadCount,
                                ActualAllocatedWorkers = lineCapacity,
                                SurplusWorkers = surplusWorkers,
                                DefaultCapacity = line.DefaultCapacity,
                                AssignedShift = WorkType.NonShift,
                                CreatedDate = DateTime.UtcNow
                            };

                            result.Assignments.Add(assignment);
                            lineUtilization[line.Id] += totalHoursWithChangeover;
                            
                            if (!lineAssignedModels[line.Id].Contains(model.ModelName))
                            {
                                lineAssignedModels[line.Id].Add(model.ModelName);
                            }

                            remainingQuantity -= unitsToAssign;
                            assigned = true;
                            break;
                        }
                    }

                    if (!assigned) break;
                }

                // If there's remaining quantity, add to unassigned list to show overtime requirement
                if (remainingQuantity > 0)
                {
                    var remainingHours = remainingQuantity * (model.TotalWorkHours / model.Quantity);
                    result.UnassignedModels.Add(new UnassignedModel
                    {
                        ModelName = model.ModelName,
                        UnassignedQuantity = remainingQuantity,
                        RequiredHours = remainingHours,
                        RequiredHeadCount = model.ModelReference.HeadCount,
                        Reason = "Requires overtime allocation - Regular capacity exceeded"
                    });
                }
            }

            // Update result with overtime calculations
            var totalUnassignedHours = result.UnassignedModels.Sum(u => u.RequiredHours);
            result.RequiredOvertimeHours = totalUnassignedHours;
            result.ActualOvertimeHours = 0; // No actual overtime assignments, just showing requirement
            result.TotalOvertimeUsed = 0;
            result.RemainingOvertimeBudget = 0; // Not applicable
        }

        private Dictionary<int, Dictionary<WorkType, double>> InitializeLineShiftUtilization(List<LineConfiguration> lines)
        {
            return lines.ToDictionary(l => l.Id, l => new Dictionary<WorkType, double>
            {
                { WorkType.Shift1, 0.0 },
                { WorkType.Shift2, 0.0 },
                { WorkType.Shift3, 0.0 }
            });
        }

        private double CalculateChangeoverTimeForShift(int lineId, string modelName, WorkType shift, 
            Dictionary<int, Dictionary<WorkType, List<string>>> lineAssignedModels)
        {
            var assignedModels = lineAssignedModels[lineId][shift];
            return assignedModels.Count == 0 ? 0.0 : CHANGEOVER_TIME_HOURS;
        }
        
        public async Task<List<OptimizedLineCapacity>> GetOptimizedCapacitiesAsync(int month, int year)
        {
            return await _context.OptimizedLineCapacities
                .Include(olc => olc.LineConfiguration)
                .Where(olc => olc.Month == month.ToString("D2") && olc.Year == year)
                .OrderBy(olc => olc.LineConfiguration.LineName)
                .ThenBy(olc => olc.WorkType)
                .ToListAsync();
        }

        
        private int GetLineCapacity(LineConfiguration line, WorkType shift, List<OptimizedLineCapacity> optimizedCapacities)
        {
            var optimized = optimizedCapacities.FirstOrDefault(oc => 
                oc.LineConfigurationId == line.Id && oc.WorkType == shift);
            return optimized?.RequiredWorkers ?? line.DefaultCapacity;
        }

        private async Task<List<ModelData>> GetSortedModelsAsync(int month, int year)
        {
            return await _context.ModelDatas
                .Include(m => m.ModelReference)
                .Where(m => m.Month == month.ToString("D2") && m.Year == year)
                .OrderByDescending(m => m.ModelReference.HeadCount)
                .ToListAsync();
        }

        private async Task<List<LineConfiguration>> GetSortedLinesAsync()
        {
            
            return await _context.LineConfigurations
                .Where(l => l.IsActive)
                .OrderByDescending(l => l.DefaultCapacity)
                .ToListAsync();
        }

        private WorkDayInfo GetWorkDays(int month, int year)
        {
            var workDays = new WorkDayInfo();
            var daysInMonth = DateTime.DaysInMonth(year, month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(year, month, day);
                var dayOfWeek = date.DayOfWeek;

                if (dayOfWeek >= DayOfWeek.Monday && dayOfWeek <= DayOfWeek.Thursday)
                {
                    workDays.RegularDays++;
                }
                else if (dayOfWeek == DayOfWeek.Friday)
                {
                    workDays.FridayDays++;
                }
            }

            return workDays;
        }

        private async Task ClearExistingAssignmentsAsync(int month, int year)
        {
            string monthStr = month.ToString("D2");

            // Clear regular assignments
            var existingAssignments = await _context.ProductionAssignments
                .Where(pa => pa.ModelData.Month == monthStr && pa.ModelData.Year == year)
                .ToListAsync();

            if (existingAssignments.Any())
            {
                _context.ProductionAssignments.RemoveRange(existingAssignments);
            }

            // Clear overtime assignments
            var existingOvertimeAssignments = await _context.OvertimeProductionAssignments
                .Where(opa => opa.ModelData.Month == monthStr && opa.ModelData.Year == year)
                .ToListAsync();

            if (existingOvertimeAssignments.Any())
            {
                _context.OvertimeProductionAssignments.RemoveRange(existingOvertimeAssignments);
            }

            // Clear optimized capacities
            var existingCapacities = await _context.OptimizedLineCapacities
                .Where(oc => oc.Month == monthStr && oc.Year == year)
                .ToListAsync();

            if (existingCapacities.Any())
            {
                _context.OptimizedLineCapacities.RemoveRange(existingCapacities);
            }

            await _context.SaveChangesAsync();
        }


        private async Task OptimizeLineCapacitiesAsync(int month, int year, List<ProductionAssignment> assignments)
        {
            // Group assignments by line AND shift to analyze worker requirements per shift
            var lineShiftAnalysis = assignments
                .GroupBy(a => new { a.LineId, a.AssignedShift })
                .Select(g => new {
                    LineId = g.Key.LineId,
                    Shift = g.Key.AssignedShift,
                    MaxRequiredWorkers = g.Max(a => a.RequiredWorkers),
                    CurrentCapacity = g.First().ActualAllocatedWorkers,
                    Assignments = g.ToList()
                })
                .Where(l => l.CurrentCapacity > l.MaxRequiredWorkers) // Only line-shifts with surplus
                .ToList();

            foreach (var lineShiftInfo in lineShiftAnalysis)
            {
                var optimizedWorkers = lineShiftInfo.MaxRequiredWorkers;
                var workersSaved = lineShiftInfo.CurrentCapacity - optimizedWorkers;

                // Check if optimization record already exists for this line-shift combination
                var existingOptimization = await _context.OptimizedLineCapacities
                    .FirstOrDefaultAsync(olc => olc.LineConfigurationId == lineShiftInfo.LineId 
                                            && olc.Month == month.ToString("D2") 
                                            && olc.Year == year
                                            && olc.WorkType == lineShiftInfo.Shift); // NEW: Include shift check

                if (existingOptimization != null)
                {
                    // Update existing record
                    existingOptimization.RequiredWorkers = optimizedWorkers;
                    existingOptimization.ModifiedDate = DateTime.UtcNow;
                    existingOptimization.Notes = $"Shift {lineShiftInfo.Shift} optimized: {optimizedWorkers} workers (saved {workersSaved})";
                }
                else
                {
                    // Create new optimization record
                    var optimization = new OptimizedLineCapacity
                    {
                        LineConfigurationId = lineShiftInfo.LineId,
                        Month = month.ToString("D2"),
                        Year = year,
                        WorkType = lineShiftInfo.Shift, // NEW: Set the shift
                        RequiredWorkers = optimizedWorkers,
                        Notes = $"Shift {lineShiftInfo.Shift} optimized: {optimizedWorkers} workers (saved {workersSaved})",
                        CreatedDate = DateTime.UtcNow
                    };

                    _context.OptimizedLineCapacities.Add(optimization);
                }

                // Update assignments for this specific line-shift to reflect optimized capacity
                foreach (var assignment in lineShiftInfo.Assignments)
                {
                    assignment.ActualAllocatedWorkers = optimizedWorkers;
                    assignment.SurplusWorkers = optimizedWorkers - assignment.RequiredWorkers;
                }
            }

            await _context.SaveChangesAsync();
        }

        // Method untuk mendapatkan summary optimasi
        public async Task<WorkerOptimizationSummary> GetWorkerOptimizationSummaryAsync(int month, int year)
        {
            var optimizations = await _context.OptimizedLineCapacities
                .Include(olc => olc.LineConfiguration)
                .Where(olc => olc.Month == month.ToString("D2") && olc.Year == year)
                .ToListAsync();

            return new WorkerOptimizationSummary
            {
                Month = month,
                Year = year,
                OptimizedLines = optimizations.Select(o => new OptimizedLineInfo
                {
                    LineId = o.LineConfigurationId,
                    LineName = o.LineConfiguration.LineName,
                    Shift = o.WorkType,
                    DefaultCapacity = o.LineConfiguration.DefaultCapacity,
                    OptimizedCapacity = o.RequiredWorkers,
                    WorkersSaved = o.WorkersSaved,
                    Notes = o.Notes
                }).ToList(),
                TotalWorkersSaved = optimizations.Sum(o => o.WorkersSaved),
                TotalDefaultWorkers = optimizations.Sum(o => o.LineConfiguration.DefaultCapacity),
                TotalOptimizedWorkers = optimizations.Sum(o => o.RequiredWorkers)
            };
        }
    }
    // Supporting classes
    public class ProductionPlanResult
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public AllocationMethod AllocationMethod { get; set; }
        public double TotalWorkHoursPerLine { get; set; }
        public double TotalDemandHours { get; set; }
        public double TotalCapacityHours { get; set; }
        public double CapacityGap { get; set; }
        public int LineCount { get; set; }
        public List<ProductionAssignment> Assignments { get; set; } = new();
        public List<OvertimeProductionAssignment> OvertimeAssignments { get; set; } = new();
        public List<UnassignedModel> UnassignedModels { get; set; } = new();
        
        public Dictionary<WorkType, double> ShiftCapacities { get; set; } = new();
        
        public WorkerOptimizationSummary WorkerOptimization { get; set; }
        
        public ShiftUtilizationSummary ShiftUtilization => CalculateShiftUtilization();
        
        // Updated overtime properties for NonShift method
        public double TotalOvertimeUsed { get; set; } = 0.0;
        public double RequiredOvertimeHours { get; set; } = 0.0; 
        public double ActualOvertimeHours { get; set; } = 0.0;   
        public double RemainingOvertimeBudget { get; set; } = 0.0; 

        private ShiftUtilizationSummary CalculateShiftUtilization()
        {
            var summary = new ShiftUtilizationSummary();
            
            // Handle different allocation methods
            var shiftsToAnalyze = AllocationMethod == AllocationMethod.NonShiftWithOvertime
                ? new[] { WorkType.NonShift }
                : new[] { WorkType.Shift1, WorkType.Shift2, WorkType.Shift3 };
            
            foreach (WorkType shift in shiftsToAnalyze)
            {
                var shiftAssignments = Assignments.Where(a => a.AssignedShift == shift).ToList();
                var totalHours = shiftAssignments.Sum(a => a.PlannedHours + a.ChangeoverHours);
                var maxCapacity = ShiftCapacities.GetValueOrDefault(shift, 0) * LineCount;

                summary.ShiftUtilizations[shift] = new ShiftUtilizationInfo
                {
                    Shift = shift,
                    TotalCapacityHours = maxCapacity,
                    UsedHours = totalHours,
                    UtilizationPercentage = maxCapacity > 0 ? (totalHours / maxCapacity) * 100 : 0,
                    AssignmentCount = shiftAssignments.Count
                };
            }
            
            return summary;
        }
    }

    public class ShiftUtilizationSummary
    {
        public Dictionary<WorkType, ShiftUtilizationInfo> ShiftUtilizations { get; set; } = new();
        
        public double TotalUtilizedHours => ShiftUtilizations.Values.Sum(s => s.UsedHours);
        public double TotalCapacityHours => ShiftUtilizations.Values.Sum(s => s.TotalCapacityHours);
        public double OverallUtilizationPercentage => TotalCapacityHours > 0 ? 
            (TotalUtilizedHours / TotalCapacityHours) * 100 : 0;
    }

    public class ShiftUtilizationInfo
    {
        public WorkType Shift { get; set; }
        public double TotalCapacityHours { get; set; }
        public double UsedHours { get; set; }
        public double UtilizationPercentage { get; set; }
        public int AssignmentCount { get; set; }
    }

    // Update existing AssignedModelInfo to include shift
    public class AssignedModelInfo
    {
        public string ModelName { get; set; }
        public int Quantity { get; set; }
        public double Hours { get; set; }
        public double ChangeoverHours { get; set; }
        public int RequiredWorkers { get; set; }
        public int SurplusWorkers { get; set; }
        public WorkType AssignedShift { get; set; } // NEW
    }

    public class LineUtilization
    {
        public int LineId { get; set; }
        public string LineName { get; set; }
        public int Capacity { get; set; } // Current capacity (optimized or default)
        public int DefaultCapacity { get; set; }
        public double MaxHours { get; set; }
        public double UsedHours { get; set; }
        public double ChangeoverHours { get; set; }
        public double AvailableHours { get; set; }
        public double UtilizationPercentage { get; set; }
        public List<AssignedModelInfo> AssignedModels { get; set; } = new();
    }

    public class UnassignedModel
    {
        public string ModelName { get; set; }
        public int UnassignedQuantity { get; set; }
        public double RequiredHours { get; set; }
        public int RequiredHeadCount { get; set; }
        public string Reason { get; set; }
    }

    public class WorkDayInfo
    {
        public int RegularDays { get; set; }
        public int FridayDays { get; set; }
    }

    public class WorkerOptimizationSummary
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public List<OptimizedLineInfo> OptimizedLines { get; set; } = new();
        public int TotalWorkersSaved { get; set; }
        public int TotalDefaultWorkers { get; set; }
        public int TotalOptimizedWorkers { get; set; }
        public double OptimizationPercentage => TotalDefaultWorkers > 0 
            ? ((double)TotalWorkersSaved / TotalDefaultWorkers) * 100 
            : 0;
    }

    public class OptimizedLineInfo
    {
        public int LineId { get; set; }
        public string LineName { get; set; }
        public WorkType Shift { get; set; }
        public int DefaultCapacity { get; set; }
        public int OptimizedCapacity { get; set; }
        public int WorkersSaved { get; set; }
        public string Notes { get; set; }
    }
}