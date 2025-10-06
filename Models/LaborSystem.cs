using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductionPlanning.Models
{
    public enum WorkType
    {
        NonShift = 0,
        Shift1 = 1,
        Shift2 = 2,
        Shift3 = 3
    }

    public enum AllocationMethod
    {
        NonShiftWithOvertime = 0,
        MultiShift = 1
    }

    public class ModelData
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string ModelName { get; set; }
        public int Quantity { get; set; }
        public string Month { get; set; }
        public int Year { get; set; }
        public int ModelReferenceId { get; set; }
        
        [ForeignKey("ModelReferenceId")]
        public virtual ModelReference ModelReference { get; set; }
        
        [NotMapped]
        public double TotalWorkHours => ((ModelReference?.SUT ?? 0) / 3600.0) * Quantity;
        
        public virtual ICollection<ProductionAssignment> ProductionAssignments { get; set; } = new List<ProductionAssignment>();

        public virtual ICollection<OvertimeProductionAssignment> OvertimeProductionAssignments { get; set; } = new List<OvertimeProductionAssignment>();

    }

    public class ModelReference
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string ModelName { get; set; }
        [Required]
        public double SUT { get; set; }
        [Required]
        public int HeadCount { get; set; }
        
        public virtual ICollection<ModelData> ModelData { get; set; } = new List<ModelData>();
    }

    public class LineConfiguration
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string LineName { get; set; }
        [Required]
        public int DefaultCapacity { get; set; }
        public bool IsActive { get; set; } = true;
        
        public virtual ICollection<ProductionAssignment> ProductionAssignments { get; set; } = new List<ProductionAssignment>();
        public virtual ICollection<OptimizedLineCapacity> OptimizedCapacities { get; set; } = new List<OptimizedLineCapacity>();
        public virtual ICollection<OvertimeProductionAssignment> OvertimeProductionAssignments { get; set; } = new List<OvertimeProductionAssignment>();

    }

    public class ShiftWorkConfiguration
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public WorkType WorkType { get; set; }
        [Required]
        public TimeSpan StartTime { get; set; }
        [Required]
        public TimeSpan EndTime { get; set; }  

        public virtual ICollection<WorkTimeDeduction> TimeDeductions { get; set; } = new List<WorkTimeDeduction>();
    }

    public class OptimizedLineCapacity
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int LineConfigurationId { get; set; }
        [Required]
        public string Month { get; set; }
        [Required]
        public int Year { get; set; }
        [Required]
        public WorkType WorkType { get; set; }
        [Required]
        public int RequiredWorkers { get; set; }
        public string Notes { get; set; } 
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        
        [ForeignKey("LineConfigurationId")]
        public virtual LineConfiguration LineConfiguration { get; set; }
        
        [NotMapped]
        public int WorkersSaved => LineConfiguration?.DefaultCapacity - RequiredWorkers ?? 0;
    }

    public class ProductionAssignment
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int ModelDataId { get; set; }
        [Required]
        public int LineId { get; set; }
        public int AssignedQuantity { get; set; }
        public double PlannedHours { get; set; }
        public double ChangeoverHours { get; set; }
        public int RequiredWorkers { get; set; }
        public int ActualAllocatedWorkers { get; set; }
        public int DefaultCapacity { get; set; }
        public int SurplusWorkers { get; set; }
        public WorkType AssignedShift { get; set; } = WorkType.Shift1;
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        
        [ForeignKey("ModelDataId")]
        public virtual ModelData ModelData { get; set; }
        
        [ForeignKey("LineId")]
        public virtual LineConfiguration LineConfiguration { get; set; }
    }

    public class OvertimeProductionAssignment
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int ModelDataId { get; set; }
        [Required]
        public int LineId { get; set; }
        public int AssignedQuantity { get; set; }
        public double PlannedHours { get; set; }
        public double ChangeoverHours { get; set; }
        public int RequiredWorkers { get; set; }
        public int ActualAllocatedWorkers { get; set; }
        public int DefaultCapacity { get; set; }
        public int SurplusWorkers { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        
        [ForeignKey("ModelDataId")]
        public virtual ModelData ModelData { get; set; }
        
        [ForeignKey("LineId")]
        public virtual LineConfiguration LineConfiguration { get; set; }
    }

    public class WorkTimeDeduction
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public TimeSpan StartTime { get; set; }
        [Required]
        public TimeSpan EndTime { get; set; }
        [Required]
        public WorkType WorkType { get; set; }

        public bool IsActive { get; set; } = true;
        
        [Required]
        public int ShiftWorkConfigurationId { get; set; }
        [ForeignKey("ShiftWorkConfigurationId")]
        public virtual ShiftWorkConfiguration ShiftWorkConfiguration { get; set; }
    }
}