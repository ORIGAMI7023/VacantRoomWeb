using ClosedXML.Excel;
using Microsoft.AspNetCore.Components.Server.Circuits;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VacantRoomWeb.Handlers;

namespace VacantRoomWeb.Services
{
    public class VacantRoomService
    {
        private readonly ConnectionCounterService _counter;
        private readonly ClientConnectionTracker _ipTracker;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly EnhancedLoggingService _logger;
        private readonly IConfiguration _configuration;
        private readonly NotificationService _notificationService;

        // 用于控制用户查询频率，避免日志膨胀
        private readonly Dictionary<string, DateTime> _lastQueryTime = new();
        private readonly Dictionary<string, int> _queryCount = new();
        private readonly object _queryLock = new();

        public VacantRoomService(
            ConnectionCounterService counter,
            ClientConnectionTracker ipTracker,
            IHttpContextAccessor httpContextAccessor,
            EnhancedLoggingService logger,
            IConfiguration configuration,
            NotificationService notificationService)
        {
            _counter = counter;
            _ipTracker = ipTracker;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _configuration = configuration;
            _notificationService = notificationService;
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

            // 智能日志记录：避免重复记录相同用户的频繁查询
            bool shouldLog = ShouldLogUserQuery(ip, "VACANT_ROOMS");

            if (shouldLog)
            {
                // 简化日志信息，去除敏感细节
                var logDetails = $"{campus.Substring(0, 2)} {weekday} {period} {building}楼 {week}";
                _logger.LogAccess(ip, "QUERY_VACANT_ROOMS", logDetails, TruncateUserAgent(userAgent));

                // 通知管理后台有新的日志
                _notificationService.NotifyLogsUpdated();
            }

            var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule.xlsx");
            if (!File.Exists(filePath))
            {
                // 错误情况必须记录
                _logger.LogAccess(ip, "ERROR_FILE_NOT_FOUND", filePath, userAgent);
                _notificationService.NotifyLogsUpdated();
                return new List<string>();
            }

            var occupiedRooms = new HashSet<string>();

            try
            {
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

                // 只在需要时记录查询结果
                if (shouldLog)
                {
                    _logger.LogAccess(ip, "QUERY_RESULT", $"Found {result.Count} vacant rooms", "");
                    _notificationService.NotifyLogsUpdated();
                }

                return result;
            }
            catch (Exception ex)
            {
                // 异常必须记录
                _logger.LogAccess(ip, "ERROR_QUERY_EXCEPTION", $"Exception: {ex.Message}", userAgent);
                _notificationService.NotifyLogsUpdated();
                return new List<string>();
            }
        }

        public List<RoomUsage> GetRoomUsage(string campus, string roomNumber, string weekday, string week)
        {
            var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
            var userAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() ?? "";

            bool shouldLog = ShouldLogUserQuery(ip, "ROOM_USAGE");

            if (shouldLog)
            {
                var logDetails = $"{campus.Substring(0, 2)} {roomNumber} {weekday} {week}";
                _logger.LogAccess(ip, "QUERY_ROOM_USAGE", logDetails, TruncateUserAgent(userAgent));
                _notificationService.NotifyLogsUpdated();
            }

            var filePath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule.xlsx");
            if (!File.Exists(filePath))
            {
                _logger.LogAccess(ip, "ERROR_FILE_NOT_FOUND", filePath, userAgent);
                _notificationService.NotifyLogsUpdated();
                return new List<RoomUsage>();
            }

            var roomUsages = new List<RoomUsage>();

            try
            {
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

                if (shouldLog)
                {
                    _logger.LogAccess(ip, "QUERY_RESULT", $"Found {result.Count} room usage records", "");
                    _notificationService.NotifyLogsUpdated();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogAccess(ip, "ERROR_QUERY_EXCEPTION", $"Exception: {ex.Message}", userAgent);
                _notificationService.NotifyLogsUpdated();
                return new List<RoomUsage>();
            }
        }

        // 智能查询日志记录策略
        private bool ShouldLogUserQuery(string ip, string queryType)
        {
            lock (_queryLock)
            {
                var now = DateTime.Now;
                var key = $"{ip}_{queryType}";

                // 清理过期记录
                CleanupOldQueryRecords();

                // 更新查询计数
                _queryCount[key] = _queryCount.GetValueOrDefault(key, 0) + 1;
                _lastQueryTime[key] = now;

                // 检查是否需要记录高频警告
                if (_queryCount[key] > 50) // 提高阈值，5分钟内超过50次才警告
                {
                    _logger.LogAccess(ip, "QUERY_HIGH_FREQUENCY", $"High frequency queries: {_queryCount[key]} in 5min", "");
                    _queryCount[key] = 0; // 重置计数避免重复警告
                }

                // 所有正常查询都记录，不过滤重复查询
                return true;
            }
        }

        private void CleanupOldQueryRecords()
        {
            var cutoff = DateTime.Now.AddMinutes(-5);
            var keysToRemove = _lastQueryTime
                .Where(kvp => kvp.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _lastQueryTime.Remove(key);
                _queryCount.Remove(key);
            }
        }

        private string TruncateUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "";
            return userAgent.Length > 30 ? userAgent.Substring(0, 30) + "..." : userAgent;
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
                {
                    return norm.Substring(idx + 1);
                }
                // 如果没找到中文数字，可能是纯数字格式，直接返回norm
                // 但需要确保它包含数字和连字符
                if (norm.Contains("-") && norm.Any(char.IsDigit))
                {
                    // 提取数字和连字符部分
                    var match = System.Text.RegularExpressions.Regex.Match(norm, @"\d+-\d+");
                    if (match.Success)
                        return match.Value;
                }
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
                // 原始逻辑：判断用户查询时间是否在课程时间范围内
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