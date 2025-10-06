using ProductionPlanning.Models;
using DataAccess.Data; 

namespace LaborSystemServer.DTOs
{
    
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

}