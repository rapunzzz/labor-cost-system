using ProductionPlanning.Models;
using DataAccess.Data;
using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Globalization;

namespace LaborSystemServer.Service
{
    public class HariKerjaResult
    {
        public List<DateTime> SeninKamis { get; set; } = new List<DateTime>();
        public List<DateTime> Jumat { get; set; } = new List<DateTime>();
        public int TotalSeninKamis { get; set; }
        public int TotalJumat { get; set; }
        public int TotalHariKerja { get; set; }
    }

    public class ExcelService
    {
        private readonly ApplicationDBContext _context;
        private const string WORKSHEET_NAME = "PSI";
        private const int DATA_START_ROW = 8;
        private const int MODEL_NAME_COLUMN = 6; // Column F
        private const int QUANTITY_TYPE_COLUMN = 7; // Column G
        private const int HEADER_ROW = 6;
        private const string QUANTITY_TYPE = "P";
        private static readonly string[] MONTH_NAMES = { "", "Jan", "Feb", "Mar", "Apr", "May", "Jun", 
                                                        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        
        public ExcelService(ApplicationDBContext context)
        {
            _context = context;
        }
        
        public List<ModelData> ReadExcel(Stream fileStream, int month, int year)
        {
            var result = new List<ModelData>();
            ExcelPackage.License.SetNonCommercialPersonal("PMI");

            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets[WORKSHEET_NAME];
            
            if (worksheet.Dimension == null)
            {
                // Console.WriteLine("Worksheet kosong atau tidak ada data");
                return result;
            }

            int endRow = worksheet.Dimension.End.Row;
            
            // Cari kolom header yang sesuai dengan bulan/tahun yang dicari
            List<int> targetColumns = FindTargetColumns(worksheet, month, year);
            
            if (targetColumns.Count == 0)
            {
                // Console.WriteLine($"Tidak ditemukan kolom untuk bulan {month}/{year}");
                return result;
            }

            // Loop through rows untuk mengambil data
            for (int row = DATA_START_ROW; row <= endRow; row++)
            {
                // Ambil model name dari kolom F
                var modelCell = worksheet.Cells[row, MODEL_NAME_COLUMN].Value;
                if (modelCell == null) continue;
                
                string modelName = modelCell.ToString().Trim();
                if (string.IsNullOrEmpty(modelName)) continue;

                // Cek apakah modelName ada di database (ModelReferences) dan ambil SUT
                var modelReference = _context.ModelReferences
                          .FirstOrDefault(m => m.ModelName == modelName);

                if (modelReference == null)
                {
                    Console.WriteLine($"Model {modelName} tidak ditemukan di ModelReferences");
                    continue;
                }

                // Cek apakah ini row dengan tipe "P" (berdasarkan kolom G)
                var typeCell = worksheet.Cells[row, QUANTITY_TYPE_COLUMN].Value;
                string qtyType = typeCell?.ToString()?.Trim() ?? "";
                
                if (qtyType == QUANTITY_TYPE)
                {
                    // Ambil data dari kolom target
                    foreach (int col in targetColumns)
                    {
                        var qtyCell = worksheet.Cells[row, col].Value;
                        if (qtyCell != null && int.TryParse(qtyCell.ToString().Replace(",", ""), out int qty))
                        {
                            if (qty == 0)
                            {
                                continue; 
                            }

                            // Check if this exact combination already exists
                            var existingRecord = _context.ModelDatas
                                .FirstOrDefault(md => md.ModelReferenceId == modelReference.Id && 
                                                    md.Month == month.ToString("D2") && 
                                                    md.Year == year);

                            if (existingRecord != null)
                            {
                                // Update existing record
                                existingRecord.Quantity = qty;
                                existingRecord.ModelName = modelName; // Update ModelName juga
                                _context.ModelDatas.Update(existingRecord);
                                result.Add(existingRecord);
                                // Console.WriteLine($"Updated existing record: {modelName}");
                            }
                            else
                            {
                                // Create new record
                                var modelData = new ModelData
                                {
                                    ModelName = modelName,
                                    ModelReferenceId = modelReference.Id,
                                    Quantity = qty,
                                    Month = month.ToString("D2"),
                                    Year = year
                                };

                                _context.ModelDatas.Add(modelData);
                                result.Add(modelData);
                                // Console.WriteLine($"Added new record: {modelName}");
                            }
                        }
                    }
                }
            }

            // Save all changes at once (more efficient)
            if (result.Count > 0)
            {
                _context.SaveChanges();
                // Console.WriteLine($"Successfully saved {result.Count} records to database");
            }
            
            return result;
        }

        public HariKerjaResult GetHariKerja(int month, int year)
        {
            var result = new HariKerjaResult();
            
            // Tentukan hari pertama dan terakhir dalam bulan
            var firstDayOfMonth = new DateTime(year, month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            
            // Loop semua hari dalam bulan
            for (var currentDate = firstDayOfMonth; currentDate <= lastDayOfMonth; currentDate = currentDate.AddDays(1))
            {
                var dayOfWeek = currentDate.DayOfWeek;
                
                // Cek apakah hari kerja (Senin-Jumat, tidak termasuk Sabtu-Minggu)
                if (dayOfWeek != DayOfWeek.Saturday && dayOfWeek != DayOfWeek.Sunday)
                {
                    // Pisahkan Senin-Kamis dan Jumat
                    if (dayOfWeek == DayOfWeek.Friday)
                    {
                        result.Jumat.Add(currentDate);
                    }
                    else // Senin, Selasa, Rabu, Kamis
                    {
                        result.SeninKamis.Add(currentDate);
                    }
                }
            }
            
            // Hitung total
            result.TotalSeninKamis = result.SeninKamis.Count;
            result.TotalJumat = result.Jumat.Count;
            result.TotalHariKerja = result.TotalSeninKamis + result.TotalJumat;
            
            return result;
        }

        private static List<int> FindTargetColumns(ExcelWorksheet worksheet, int month, int year)
        {
            var targetColumns = new List<int>();
            
            // Cek row 6 untuk header
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var headerCellValue = worksheet.Cells[HEADER_ROW, col].Value;
                string headerFormatted = ConvertDateToMonthYear(headerCellValue);
                
                if (IsTargetMonth(headerFormatted, month, year))
                {
                    targetColumns.Add(col);
                    // Console.WriteLine($"Target column ditemukan: {col} untuk {headerFormatted}");
                    break;
                }
            }
            
            return targetColumns;
        }

        private static string ConvertDateToMonthYear(object cellValue)
        {
            if (cellValue == null) return "";
            
            try
            {
                DateTime date;
                
                // Jika cellValue adalah DateTime
                if (cellValue is DateTime dateTime)
                {
                    date = dateTime;
                }
                // Jika cellValue adalah double (Excel date serial number)
                else if (cellValue is double doubleValue)
                {
                    date = DateTime.FromOADate(doubleValue);
                }
                // Jika cellValue adalah string, coba parse
                else if (cellValue is string stringValue)
                {
                    if (!DateTime.TryParse(stringValue, out date))
                    {
                        return stringValue; // Return as is jika tidak bisa di-parse
                    }
                }
                else
                {
                    return cellValue.ToString();
                }
                
                string yearTwoDigit = (date.Year % 100).ToString("D2");
                return $"{MONTH_NAMES[date.Month]}-{yearTwoDigit}";
            }
            catch
            {
                return cellValue?.ToString() ?? "";
            }
        }

        private static bool IsTargetMonth(string header, int month, int year)
        {
            if (string.IsNullOrEmpty(header)) return false;
            
            try
            {
                string yearTwoDigit = (year % 100).ToString("D2");
                string targetFormat = $"{MONTH_NAMES[month]}-{yearTwoDigit}";
                
                return header.Equals(targetFormat, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}