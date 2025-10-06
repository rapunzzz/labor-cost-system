using ProductionPlanning.Models;
using DataAccess.Data; 

namespace LaborSystemServer.DTOs
{
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
}