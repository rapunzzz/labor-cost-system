using ProductionPlanning.Models;
using DataAccess.Data; 

namespace LaborSystemServer.DTOs
{
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