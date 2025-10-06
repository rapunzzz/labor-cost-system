using ProductionPlanning.Models;
using DataAccess.Data; 

namespace LaborSystemServer.DTOs
{
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

    public class UnassignedModel
    {
        public string ModelName { get; set; }
        public int UnassignedQuantity { get; set; }
        public double RequiredHours { get; set; }
        public int RequiredHeadCount { get; set; }
        public string Reason { get; set; }
    }
}