using ClosedXML.Excel;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VacantRoomWeb
{
    public class VacantRoomService
    {
        private readonly ConnectionCounterService _counter;
        private readonly ClientConnectionTracker _ipTracker;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly EnhancedLoggingService _logger;

        public VacantRoomService(
            ConnectionCounterService counter,
            ClientConnectionTracker ipTracker,
            IHttpContextAccessor httpContextAccessor,
            EnhancedLoggingService logger)
        {
            _counter = counter;
            _ipTracker = ipTracker;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        List<string> list = new List<string> { "A101", "A102", "A103", "A104", "A105", "A106",
                                               "A201", "A202", "A203", "A204", "A205", "A206",
                                               "A301", "A302", "A303", "A304", "A305",
                                               "A401", "A402", "A403", "A404", "A405", "A406",
                                               "B101", "B102", "B103", "B104", "B105", "B106", "B107", "B108",
                                               "B201", "B202", "B203", "B204", "B205", "B206", "B207", "B208",
                                               "B301", "B302", "B303", "B304", "B305", "B306", "B307", "B308",
                                               "B401", "B402", "B403", "B404", "B405", "B406", "B407", "B408",
                                               "B501", "B502", "B503", "B504", "B505", "B506", "B507", "B508",
                                               "C101", "C102", "C103", "C104", "C105", "C106", "C107", "C108",
                                               "C200", "C201", "C202", "C203", "C204", "C205", "C206", "C207", "C208",
                                               "C301", "C302", "C303", "C304", "C305", "C306", "C307", "C308",
                                               "C401", "C402", "C403", "C404", "C405", "C406", "C407", "C408",
                                               "C501", "C502", "C503", "C504", "C505", "C506", "C507", "C508",
                                               "D101", "D102", "D103", "D104", "D105", "D106", "D107", "D108",
                                               "D201", "D202", "D203", "D204", "D205", "D206", "D207", "D208",
                                               "D301", "D302", "D303", "D304", "D305", "D306", "D307", "D308",
                                               "D401", "D402", "D403", "D404", "D405", "D406", "D407", "D408",
                                               "D501", "D502", "D503", "D504", "D505", "D506", "D507", "D508",
                                               "E113", "E115", };

        // Dictionary for period time mapping
        private readonly Dictionary<string, string> PeriodTimeMap = new()
        {
            { "1-2节", "08:00-09:40" },
            { "01-02节", "08:00-09:40" },
            { "3-4节", "09:55-11:35" },
            { "03-04节", "09:55-11:35" },
            { "5-6节", "13:30-15:10" },
            { "05-06节", "13:30-15:10" },
            { "7-8节", "15:25-17:05" },
            { "07-08节", "15:25-17:05" },
            { "9-10节", "18:00-19:35" },
            { "09-10节", "18:00-19:35" },
            { "9-11节", "18:00-20:30" },
            { "09-11节", "18:00-20:30" }
        };

        public List<string> GetVacantRooms(string campus, string weekday, string period, string building, string week)
        {
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "";

            _logger.LogAccess(ip, "QUERY_VACANT_ROOMS", $"{campus.Substring(0, 2)} {weekday} {period} {building}楼 {week}", userAgent);

            var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule.xlsx");
            if (!File.Exists(filePath))
            {
                _logger.LogAccess(ip, "ERROR_FILE_NOT_FOUND", filePath, userAgent);
                return new List<string>();
            }

            var occupiedRooms = new HashSet<string>();

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();

            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                var rowCampus = row.Cell(10).GetString();
                var rowTime = row.Cell(14).GetString();
                var rowWeeks = row.Cell(15).GetString();
                var room = row.Cell(16).GetString();

                if (!room.Any()) continue;

                if (rowCampus != campus) continue;
                if (!IsPeriodMatch(rowTime, weekday, period)) continue;
                if (!IsWeekMatch(rowWeeks, week)) continue;

                occupiedRooms.Add(room);
            }

            var allRooms = list;
            var result = allRooms
                .Where(r => !occupiedRooms.Contains(r) &&
                            (building == "所有" || r.StartsWith(building)))
                .ToList();

            _logger.LogAccess(ip, "QUERY_RESULT", $"Found {result.Count} vacant rooms", userAgent);
            return result;
        }

        public List<RoomUsage> GetRoomUsage(string campus, string roomNumber, string weekday, string week)
        {
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "";

            _logger.LogAccess(ip, "QUERY_ROOM_USAGE", $"{campus.Substring(0, 2)} {roomNumber} {weekday} {week}", userAgent);

            var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule.xlsx");
            if (!File.Exists(filePath))
            {
                _logger.LogAccess(ip, "ERROR_FILE_NOT_FOUND", filePath, userAgent);
                return new List<RoomUsage>();
            }

            var roomUsages = new List<RoomUsage>();

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();

            foreach (var row in sheet.RowsUsed().Skip(1))
            {
                var rowCampus = row.Cell(10).GetString();
                var rowTime = row.Cell(14).GetString();
                var rowWeeks = row.Cell(15).GetString();
                var room = row.Cell(16).GetString();
                var courseName = row.Cell(5).GetString();
                var teacher = row.Cell(12).GetString();

                if (string.IsNullOrEmpty(room)) continue;
                if (rowCampus != campus) continue;
                if (room != roomNumber) continue;
                if (!IsWeekdayMatch(rowTime, weekday)) continue;
                if (!IsWeekMatch(rowWeeks, week)) continue;

                var period = ExtractPeriodFromTime(rowTime);
                var timeRange = GetTimeRangeForPeriod(period);

                roomUsages.Add(new RoomUsage
                {
                    Period = period,
                    TimeRange = timeRange,
                    CourseName = courseName,
                    Teacher = teacher,
                });
            }

            var periodOrder = new Dictionary<string, int>
            {
                { "1-2节", 1 }, { "01-02节", 1 },
                { "3-4节", 2 }, { "03-04节", 2 },
                { "5-6节", 3 }, { "05-06节", 3 },
                { "7-8节", 4 }, { "07-08节", 4 },
                { "9-10节", 5 }, { "09-10节", 5 },
                { "9-11节", 6 }, { "09-11节", 6 },
                { "10-11节", 7 }, { "10-12节", 8 }
            };

            var result = roomUsages.OrderBy(r => periodOrder.GetValueOrDefault(r.Period, 999)).ToList();
            _logger.LogAccess(ip, "QUERY_RESULT", $"Found {result.Count} room usage records", userAgent);

            return result;
        }

        public List<string> GetRoomsForBuilding(string building)
        {
            if (string.IsNullOrEmpty(building)) return new List<string>();

            return list.Where(r => r.StartsWith(building))
                      .Select(r => r.Substring(1))
                      .Distinct()
                      .OrderBy(r => r)
                      .ToList();
        }

        // Keep all existing helper methods unchanged
        private bool IsWeekdayMatch(string rowTime, string selectedWeekday)
        {
            string ExtractWeekday(string text)
            {
                if (text.StartsWith("周") && text.Length >= 2)
                    return text.Substring(0, 2);
                return "";
            }

            return ExtractWeekday(rowTime) == ExtractWeekday(selectedWeekday);
        }

        private string ExtractPeriodFromTime(string rowTime)
        {
            var parts = rowTime.Split(' ');
            if (parts.Length > 1)
            {
                var periodPart = parts[1];
                if (periodPart.EndsWith("节"))
                {
                    return periodPart;
                }
                else
                {
                    return periodPart + "节";
                }
            }
            return "";
        }

        private string GetTimeRangeForPeriod(string period)
        {
            period = period.Replace("节", "");
            if (!period.Contains("-"))
            {
                if (int.TryParse(period, out int num))
                {
                    period = $"{num}-{num}";
                }
            }
            period += "节";

            return PeriodTimeMap.GetValueOrDefault(period, "时间未知");
        }

        private bool IsWeekMatch(string rowWeeks, string selectedWeek)
        {
            if (!int.TryParse(selectedWeek.Replace("第", "").Replace("周", ""), out int weekNumber))
                return false;

            rowWeeks = rowWeeks.Replace("周", "").Replace(" ", "");
            bool isSingle = rowWeeks.EndsWith("单");
            bool isDouble = rowWeeks.EndsWith("双");
            bool isAll = rowWeeks.EndsWith("全");

            if (isSingle || isDouble || isAll)
                rowWeeks = rowWeeks.Substring(0, rowWeeks.Length - 1);

            var matched = false;

            foreach (var part in rowWeeks.Split(','))
            {
                if (part.Contains('-'))
                {
                    var bounds = part.Split('-');
                    if (bounds.Length == 2 &&
                        int.TryParse(bounds[0], out int start) &&
                        int.TryParse(bounds[1], out int end))
                    {
                        if (weekNumber >= start && weekNumber <= end)
                        {
                            matched = true;
                            break;
                        }
                    }
                }
                else if (int.TryParse(part, out int val))
                {
                    if (val == weekNumber)
                    {
                        matched = true;
                        break;
                    }
                }
            }

            if (!matched) return false;

            if (isSingle && weekNumber % 2 == 0) return false;
            if (isDouble && weekNumber % 2 != 0) return false;

            return true;
        }

        private bool IsPeriodMatch(string rowTime, string selectedWeekday, string selectedPeriod)
        {
            string Normalize(string text) => text.Replace("节", "").Replace(" ", "");

            string ExtractWeekday(string text)
            {
                if (text.StartsWith("周") && text.Length >= 2)
                    return text.Substring(0, 2);
                return "";
            }

            string ExtractRange(string text)
            {
                var norm = Normalize(text);
                var idx = norm.IndexOfAny("一二三四五六日".ToCharArray());
                if (idx >= 0 && idx + 1 < norm.Length)
                    return norm.Substring(idx + 1);
                return "";
            }

            bool TryParseRange(string range, out int start, out int end)
            {
                start = end = 0;
                var nums = range.Split('-');
                if (nums.Length == 2 &&
                    int.TryParse(nums[0], out start) &&
                    int.TryParse(nums[1], out end)) return true;
                return false;
            }

            string rowWeekday = ExtractWeekday(rowTime);
            string userWeekday = ExtractWeekday(selectedWeekday);

            if (rowWeekday != userWeekday) return false;

            string rowRange = ExtractRange(rowTime);
            string userRange = Normalize(selectedPeriod);

            if (TryParseRange(rowRange, out int rowStart, out int rowEnd) &&
                TryParseRange(userRange, out int userStart, out int userEnd))
            {
                return userStart >= rowStart && userEnd <= rowEnd;
            }

            return false;
        }
    }

    public class RoomUsage
    {
        public string Period { get; set; } = "";
        public string TimeRange { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Teacher { get; set; } = "";
    }
}