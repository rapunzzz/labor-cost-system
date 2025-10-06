using ProductionPlanning.Models;
using DataAccess.Data; 

namespace LaborSystemServer.DTOs
{
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
}