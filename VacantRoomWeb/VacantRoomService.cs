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

        public VacantRoomService(
            ConnectionCounterService counter,
            ClientConnectionTracker ipTracker,
            IHttpContextAccessor httpContextAccessor)
        {
            _counter = counter;
            _ipTracker = ipTracker;
            _httpContextAccessor = httpContextAccessor;
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
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "未知IP";

            Console.Write($"{DateTime.Now:yyyy/M/d HH:mm:ss} IP: {ip,-16} ");

            Console.ForegroundColor = ConsoleColor.Yellow;//使用黄色字体
            Console.WriteLine("获取数据");
            Console.ResetColor(); // 恢复默认颜色
            Console.WriteLine($"查询条件：{campus.Substring(0, 2)} {weekday} {period} {building}教学楼 {week}");

            var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule.xlsx");
            if (!File.Exists(filePath))
            {
                Console.WriteLine("文件未找到：" + filePath);
                return new List<string>();
            }

            var occupiedRooms = new HashSet<string>();

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();

            foreach (var row in sheet.RowsUsed().Skip(1)) // 跳过表头
            {
                var rowCampus = row.Cell(10).GetString();       // J列：排课校区
                var rowTime = row.Cell(14).GetString();         // N列：上课时间（如 周一 1-2节）
                var rowWeeks = row.Cell(15).GetString();        // O列：起止周（如 1-16周全）
                var room = row.Cell(16).GetString();            // P列：上课地点（如 B303）

                if (!room.Any()) continue;

                // 校区匹配
                if (rowCampus != campus) continue;

                // 时间段匹配（更强判断：节次区间是否覆盖）
                if (!IsPeriodMatch(rowTime, weekday, period)) continue;

                // 周次匹配（如"第1周"是否在"1-16周全"中）
                if (!IsWeekMatch(rowWeeks, week)) continue;

                occupiedRooms.Add(room);
            }

            // 可扩展为动态提取所有教室
            var allRooms = list;

            return allRooms
                .Where(r => !occupiedRooms.Contains(r) &&
                            (building == "所有" || r.StartsWith(building)))
                .ToList();
        }

        // NEW METHOD: Get room usage for a specific room
        public List<RoomUsage> GetRoomUsage(string campus, string roomNumber, string weekday, string week)
        {
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "未知IP";

            Console.Write($"{DateTime.Now:yyyy/M/d HH:mm:ss} IP: {ip,-16} ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("查询教室使用情况");
            Console.ResetColor();
            Console.WriteLine($"查询条件：{campus.Substring(0, 2)} {roomNumber} {weekday} {week}");

            var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule.xlsx");
            if (!File.Exists(filePath))
            {
                Console.WriteLine("文件未找到：" + filePath);
                return new List<RoomUsage>();
            }

            var roomUsages = new List<RoomUsage>();

            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheets.First();

            foreach (var row in sheet.RowsUsed().Skip(1)) // 跳过表头
            {
                var rowCampus = row.Cell(10).GetString();       // J列：排课校区
                var rowTime = row.Cell(14).GetString();         // N列：上课时间（如 周一 1-2节）
                var rowWeeks = row.Cell(15).GetString();        // O列：起止周（如 1-16周全）
                var room = row.Cell(16).GetString();            // P列：上课地点（如 B303）
                var courseName = row.Cell(5).GetString();       // E列：课程名称
                var teacher = row.Cell(12).GetString();          // H列：任课教师

                if (string.IsNullOrEmpty(room)) continue;

                // 校区匹配
                if (rowCampus != campus) continue;

                // 教室匹配
                if (room != roomNumber) continue;

                // 星期匹配
                if (!IsWeekdayMatch(rowTime, weekday)) continue;

                // 周次匹配
                if (!IsWeekMatch(rowWeeks, week)) continue;

                // 提取节次信息
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

            // 按节次顺序排序
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

            return roomUsages.OrderBy(r => periodOrder.GetValueOrDefault(r.Period, 999)).ToList();
        }

        // NEW METHOD: Get available rooms for a building (for the dropdown)
        public List<string> GetRoomsForBuilding(string building)
        {
            if (string.IsNullOrEmpty(building)) return new List<string>();

            return list.Where(r => r.StartsWith(building))
                      .Select(r => r.Substring(1)) // Remove building letter (A101 -> 101)
                      .Distinct()
                      .OrderBy(r => r)
                      .ToList();
        }

        // Helper method to check weekday match
        private bool IsWeekdayMatch(string rowTime, string selectedWeekday)
        {
            string ExtractWeekday(string text)
            {
                if (text.StartsWith("周") && text.Length >= 2)
                    return text.Substring(0, 2); // 如 "周三"
                return "";
            }

            return ExtractWeekday(rowTime) == ExtractWeekday(selectedWeekday);
        }

        // Helper method to extract period from time string
        private string ExtractPeriodFromTime(string rowTime)
        {
            // Extract period range from time string like "周三 9-10节" -> "9-10节"
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

        // Helper method to get time range for a period
        private string GetTimeRangeForPeriod(string period)
        {
            // Normalize period format
            period = period.Replace("节", "");
            if (!period.Contains("-"))
            {
                // Handle single periods like "1" -> "1-1"
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

            // 去掉结尾描述（单/双/全）
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

            // 提取"周几"和"节次范围"
            string ExtractWeekday(string text)
            {
                if (text.StartsWith("周") && text.Length >= 2)
                    return text.Substring(0, 2); // 如 "周三"
                return "";
            }

            string ExtractRange(string text)
            {
                var norm = Normalize(text);
                var idx = norm.IndexOfAny("一二三四五六日".ToCharArray());
                if (idx >= 0 && idx + 1 < norm.Length)
                    return norm.Substring(idx + 1); // 从节次范围开始
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

            string rowWeekday = ExtractWeekday(rowTime);        // 周三
            string userWeekday = ExtractWeekday(selectedWeekday); // 周三

            if (rowWeekday != userWeekday) return false;

            string rowRange = ExtractRange(rowTime);             // 09-11
            string userRange = Normalize(selectedPeriod);        // 9-10

            if (TryParseRange(rowRange, out int rowStart, out int rowEnd) &&
                TryParseRange(userRange, out int userStart, out int userEnd))
            {
                return userStart >= rowStart && userEnd <= rowEnd;
            }

            return false;
        }
    }

    // NEW CLASS: Room usage data model
    public class RoomUsage
    {
        public string Period { get; set; } = "";
        public string TimeRange { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string Teacher { get; set; } = "";
    }
}