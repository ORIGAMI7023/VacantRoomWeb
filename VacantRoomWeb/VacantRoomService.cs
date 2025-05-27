using System.Collections.Generic;
using ClosedXML.Excel;
using System.IO;
using System.Linq;

namespace VacantRoomWeb
{
    public class VacantRoomService
    {
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

        public List<string> GetVacantRooms(string campus, string weekday, string period, string building, string week)
        {
            Console.WriteLine("读取文件…");

            var filePath = Path.Combine("wwwroot", "schedule.xlsx");
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

                if (room == "A206" && rowTime == "周二 01-02节"&&period=="1-2节")
                    ;

                // 时间段匹配（更强判断：节次区间是否覆盖）
                if (!IsPeriodMatch(rowTime, weekday, period)) continue;

                // 周次匹配（如“第1周”是否在“1-16周全”中）
                if (!IsWeekMatch(rowWeeks, week)) continue;


                if (room.StartsWith("A"))
                    ;

                occupiedRooms.Add(room);
            }

            // 可扩展为动态提取所有教室
            var allRooms = list;

            return allRooms
                .Where(r => !occupiedRooms.Contains(r) &&
                            (building == "所有" || r.StartsWith(building)))
                .ToList();
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

            // 提取“周几”和“节次范围”
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


}
