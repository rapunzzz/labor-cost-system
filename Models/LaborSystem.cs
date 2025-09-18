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

    // Model untuk data produksi (demand)
    public class ModelData
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string ModelName { get; set; }
        
        public int Quantity { get; set; }
        
        public string Month { get; set; } // e.g., "09"
        
        public int Year { get; set; }
        
        public int ModelReferenceId { get; set; }
        
        [ForeignKey("ModelReferenceId")]
        public virtual ModelReference ModelReference { get; set; }
        
        // Calculated property for total work hours
        [NotMapped]
        public double TotalWorkHours => ((ModelReference?.SUT ?? 0) / 3600.0) * Quantity;
        
        public virtual ICollection<ProductionAssignment> ProductionAssignments { get; set; } = new List<ProductionAssignment>();

        public virtual ICollection<OvertimeProductionAssignment> OvertimeProductionAssignments { get; set; } = new List<OvertimeProductionAssignment>();

    }

    // Master data untuk model specifications
    public class ModelReference
    {
        [Key]
        public int Id { get; set; } // Primary Key baru
        
        [Required]
        public string ModelName { get; set; } // Index biasa, bukan Primary Key
        
        [Required]
        public double SUT { get; set; } // Standard Unit Time (seconds per unit)
        
        [Required]
        public int HeadCount { get; set; } // Required workers
        
        public virtual ICollection<ModelData> ModelData { get; set; } = new List<ModelData>();
    }

    public class LineConfiguration
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string LineName { get; set; } // e.g., "LINE_GP_1", "LINE_AUTO_1"
        
        [Required]
        public int DefaultCapacity { get; set; } // Default worker capacity sebagai referensi
        
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
        public int RegularDayMinutes { get; set; } // Senin-Kamis
        
        [Required]
        public int FridayMinutes { get; set; } // Jumat (biasanya sama kecuali Shift1)
                
        public bool IsActive { get; set; } = true;
        
        // Static method untuk mendapatkan minutes per shift
        public static (int regularMinutes, int fridayMinutes) GetWorkMinutesPerShift(WorkType shiftType)
        {
            return shiftType switch
            {
                WorkType.NonShift => (473, 433),
                WorkType.Shift1 => (458, 418), // Jumat berkurang 40 menit karena ishoma + jumatan
                WorkType.Shift2 => (398, 398), // Shift 2&3 tidak terpengaruh jumatan
                WorkType.Shift3 => (398, 398),
                _ => (0, 0)
            };
        }
    }

    public class OptimizedLineCapacity
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int LineConfigurationId { get; set; }
        
        [Required]
        public string Month { get; set; } // e.g., "09"
        
        [Required]
        public int Year { get; set; }

        [Required]
        public WorkType WorkType { get; set; }
        
        [Required]
        public int RequiredWorkers { get; set; } // Worker yang benar-benar dibutuhkan
        
        public string Notes { get; set; } 
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedDate { get; set; }
        
        [ForeignKey("LineConfigurationId")]
        public virtual LineConfiguration LineConfiguration { get; set; }
        
        // Calculated property
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
}