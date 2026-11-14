using System;
using System.Collections.Generic;

namespace DWDN_MechanicsTester
{
    public enum TestStatus
    {
        Passed,
        Failed,
        Skipped
    }

    public class TestCaseResult
    {
        public string Name { get; set; } = "";
        public string SuiteName { get; set; } = "";
        public TestStatus Status { get; set; } = TestStatus.Passed;
        public string Message { get; set; } = "OK";
        public double DurationMs { get; set; }
        public string? StackTrace { get; set; }
    }

    public class TestSuiteResult
    {
        public string SuiteName { get; set; } = "";
        public string Description { get; set; } = "";
        public int Total => Results.Count;
        public int Passed => Results.FindAll(r => r.Status == TestStatus.Passed).Count;
        public int Failed => Results.FindAll(r => r.Status == TestStatus.Failed).Count;
        public double DurationMs { get; set; }
        public List<TestCaseResult> Results { get; set; } = new();
    }

    public class TestRunReport
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int TotalSuites => Suites.Count;
        public int TotalTests { get; set; }
        public int TotalPassed { get; set; }
        public int TotalFailed { get; set; }
        public double TotalDurationMs { get; set; }
        public List<TestSuiteResult> Suites { get; set; } = new();
    }

    public class TestAssertionException : Exception
    {
        public TestAssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        public static void IsTrue(bool condition, string message)
        {
            if (!condition)
                throw new TestAssertionException($"[FAIL] Ожидалось True, получено False: {message}");
        }

        public static void IsFalse(bool condition, string message)
        {
            if (condition)
                throw new TestAssertionException($"[FAIL] Ожидалось False, получено True: {message}");
        }

        public static void AreEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TestAssertionException($"[FAIL] Несовпадение значений. Ожидалось: '{expected}', получено: '{actual}'. Причина: {message}");
        }

        public static void AreApproximatelyEqual(float expected, float actual, float tolerance, string message)
        {
            float diff = Math.Abs(expected - actual);
            if (diff > tolerance)
                throw new TestAssertionException($"[FAIL] Числовое расхождение ({diff:F4} > {tolerance:F4}). Ожидалось: {expected:F4}, получено: {actual:F4}. {message}");
        }

        public static void IsNotNull(object? obj, string message)
        {
            if (obj == null)
                throw new TestAssertionException($"[FAIL] Объект оказался null: {message}");
        }

        public static void Fail(string message)
        {
            throw new TestAssertionException($"[FAIL] {message}");
        }
    }
}
