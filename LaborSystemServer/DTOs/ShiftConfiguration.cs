using ProductionPlanning.Models;
using System.ComponentModel.DataAnnotations;

namespace LaborSystemServer.DTOs
{
    public class ShiftConfigurationDto
    {
        [Required]
        public WorkType WorkType { get; set; }
        
        [Required]
        public TimeSpan StartTime { get; set; }
        
        [Required]
        public TimeSpan EndTime { get; set; }
    }

    public class WorkTimeDeductionDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        
        [Required]
        public TimeSpan StartTime { get; set; }
        
        [Required]
        public TimeSpan EndTime { get; set; }

        public bool IsActive { get; set; }

        [Required]
        public WorkType WorkType { get; set; }

        public int? ShiftWorkConfigurationId { get; set; }
    }

    public class ShiftCapacityCalculationResult
    {
        public WorkType WorkType { get; set; }
        public int GrossMinutesPerDay { get; set; }
        public int DeductionMinutesPerDay { get; set; }
        public int NetRegularMinutes { get; set; }
        public int NetFridayMinutes { get; set; }
        public int RegularDays { get; set; }
        public int FridayDays { get; set; }
        public int TotalMinutes { get; set; }
        public double TotalHours { get; set; }
    }

    public class ShiftConfigurationViewModel
    {
        public int Id { get; set; }
        public WorkType WorkType { get; set; }
        public string WorkTypeName => WorkType.ToString();
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int GrossMinutes
        {
            get
            {
                var duration = EndTime - StartTime;
                if (duration.TotalMinutes < 0)
                {
                    duration += TimeSpan.FromDays(1);
                }
                return (int)duration.TotalMinutes;
            }
        }
        public List<WorkTimeDeductionViewModel> TimeDeductions { get; set; } = new();
        public int TotalDeductionMinutes => TimeDeductions.Where(d => d.IsActive).Sum(d => d.DurationMinutes);
        public int NetMinutes => GrossMinutes - TotalDeductionMinutes;
    }

    public class WorkTimeDeductionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int DurationMinutes
        {
            get
            {
                var duration = EndTime - StartTime;
                if (duration.TotalMinutes < 0)
                {
                    duration += TimeSpan.FromDays(1);
                }
                return (int)duration.TotalMinutes;
            }
        }
        public bool IsActive { get; set; }
        public int ShiftWorkConfigurationId { get; set; }
    }
}