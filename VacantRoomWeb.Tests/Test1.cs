using Microsoft.VisualStudio.TestTools.UnitTesting;
using VacantRoomWeb;
using System.Reflection;

namespace VacantRoomWeb.Tests
{
    [TestClass]
    public class PeriodMatchTests
    {
        [TestMethod]
        public void Test_Period_Match_Batch()
        {
            RunPeriodMatchTests(
                ("周三 09-11节", "周三", "9-10节", true),
                ("周二 01-02节", "周二", "1-2节", true),
                ("周二 01-02节", "周二", "2-4节", false),
                ("周五 03-04节", "周一", "1-2节", false)
            );
        }



        private void RunPeriodMatchTests(params (string rowTime, string weekday, string period, bool expected)[] testCases)
        {
            var service = new VacantRoomService();
            var method = typeof(VacantRoomService).GetMethod("IsPeriodMatch",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method);

            foreach (var (rowTime, weekday, period, expected) in testCases)
            {
                object[] parameters = new object[] { rowTime, weekday, period };
                bool result = (bool)method.Invoke(service, parameters);

                if (expected)
                    Assert.IsTrue(result, $"应匹配：{rowTime} vs {weekday} {period}");
                else
                    Assert.IsFalse(result, $"不应匹配：{rowTime} vs {weekday} {period}");
            }
        }


    }
}
