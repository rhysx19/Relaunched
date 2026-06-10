using System.Collections.Generic;
using Xunit;
using ClassicLaunchpad.Core;

namespace ClassicLaunchpad.Tests
{
    public class SearchEngineTests
    {
        [Fact]
        public void TestSearchEngine_FiltersAppsByName()
        {
            // Arrange
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem>
            {
                new AppItem { Id = "1", Name = "Calculator" },
                new AppItem { Id = "2", Name = "Terminal" }
            };

            // Act
            var result = engine.Query("Term", pool);

            // Assert
            Assert.False(result.IsMathExpression);
            Assert.False(result.IsSystemAction);
            Assert.Single(result.FilteredApps);
            Assert.Equal("Terminal", result.FilteredApps[0].Name);
        }

        [Fact]
        public void TestSearchEngine_EvaluatesMathExpression()
        {
            // Arrange
            ISearchEngine engine = new SearchEngine();

            // Act
            var result = engine.Query("3 + 4 * 2", new List<AppItem>());

            // Assert
            Assert.True(result.IsMathExpression);
            Assert.Equal("11", result.MathResult);
        }

        [Fact]
        public void TestSearchEngine_IdentifiesSystemAction()
        {
            // Arrange
            ISearchEngine engine = new SearchEngine();

            // Act
            var result = engine.Query("sleep", new List<AppItem>());

            // Assert
            Assert.True(result.IsSystemAction);
            Assert.Equal(SystemActionType.Sleep, result.SystemAction);
        }


        [Fact]
        public void TestSearchEngine_EmptyTrashActionMapping()
        {
            ISearchEngine engine = new SearchEngine();

            var res1 = engine.Query("empty trash", new List<AppItem>());
            Assert.True(res1.IsSystemAction);
            Assert.Equal(SystemActionType.EmptyTrash, res1.SystemAction);

            var res2 = engine.Query("EmptyTrash", new List<AppItem>());
            Assert.True(res2.IsSystemAction);
            Assert.Equal(SystemActionType.EmptyTrash, res2.SystemAction);

            var res3 = engine.Query("emptytrash", new List<AppItem>());
            Assert.True(res3.IsSystemAction);
            Assert.Equal(SystemActionType.EmptyTrash, res3.SystemAction);
        }

        [Fact]
        public void TestSearchEngine_BalancedParenthesesFallback()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem> {
                new AppItem { Id = "test1", Name = "(5 + 5" },
                new AppItem { Id = "test2", Name = "5 + 5)" }
            };

            var res1 = engine.Query("(5 + 5", pool);
            Assert.False(res1.IsMathExpression);
            Assert.Single(res1.FilteredApps);
            Assert.Equal("(5 + 5", res1.FilteredApps[0].Name);

            var res2 = engine.Query("5 + 5)", pool);
            Assert.False(res2.IsMathExpression);
            Assert.Single(res2.FilteredApps);
            Assert.Equal("5 + 5)", res2.FilteredApps[0].Name);
        }

        [Fact]
        public void TestSearchEngine_EmptyParenthesesFallback()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem> { new AppItem { Id = "test", Name = "math" } };

            var res = engine.Query("5 + ()", pool);
            Assert.False(res.IsMathExpression);
        }

        [Fact]
        public void TestSearchEngine_InvalidSyntaxFallback()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem>();

            var res1 = engine.Query("5 +", pool);
            Assert.False(res1.IsMathExpression);

            var res3 = engine.Query("5 /", pool);
            Assert.False(res3.IsMathExpression);

            var res4 = engine.Query("5 + * 2", pool);
            Assert.False(res4.IsMathExpression);
        }

        [Fact]
        public void TestSearchEngine_DivisionByZeroFallback()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem> { new AppItem { Id = "test", Name = "divbyzero" } };

            var res1 = engine.Query("5 / 0", pool);
            Assert.False(res1.IsMathExpression);

            var res2 = engine.Query("5 / (2 - 2)", pool);
            Assert.False(res2.IsMathExpression);
        }

        [Fact]
        public void TestSearchEngine_FormattingAndPrecision()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem>();

            var res1 = engine.Query("1 / 3", pool);
            Assert.True(res1.IsMathExpression);
            Assert.Equal("0.3333", res1.MathResult);

            var res2 = engine.Query("1 / 2", pool);
            Assert.True(res2.IsMathExpression);
            Assert.Equal("0.5", res2.MathResult);

            var res3 = engine.Query("10 / 5", pool);
            Assert.True(res3.IsMathExpression);
            Assert.Equal("2", res3.MathResult);

            var res4 = engine.Query("100000 * 100000", pool);
            Assert.True(res4.IsMathExpression);
            Assert.Equal("10000000000", res4.MathResult);
        }

        [Fact]
        public void TestSearchEngine_NumericInput_DoesNotTriggerSystemAction()
        {
            ISearchEngine engine = new SearchEngine();
            var result = engine.Query("1", new List<AppItem>());
            Assert.False(result.IsSystemAction);
        }

        [Fact]
        public void TestSearchEngine_NumericInput99_DoesNotTriggerSystemAction()
        {
            ISearchEngine engine = new SearchEngine();
            var result = engine.Query("99", new List<AppItem>());
            Assert.False(result.IsSystemAction);
        }

        [Fact]
        public void TestSearchEngine_NumericInput_DoesNotTriggerSystemAction_Undefined()
        {
            ISearchEngine engine = new SearchEngine();
            var result = engine.Query("6", new List<AppItem>());
            Assert.False(result.IsSystemAction);
            
            var resultNegative = engine.Query("-1", new List<AppItem>());
            Assert.False(resultNegative.IsSystemAction);
        }

        [Fact]
        public void TestSearchEngine_UnaryOperatorBeforeParenthesisFallback()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem> { new AppItem { Id = "test", Name = "-(5 + 5)" } };
            // Unary operator directly before parenthesis causes FormatException in factor parser, falling back to search
            var result = engine.Query("-(5 + 5)", pool);
            Assert.False(result.IsMathExpression);
            Assert.Single(result.FilteredApps);
            Assert.Equal("-(5 + 5)", result.FilteredApps[0].Name);
        }

        [Fact]
        public void TestSearchEngine_MathExpressionsWithMultipleDecimalsFallback()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem>();
            var result = engine.Query("1.2.3 + 4", pool);
            Assert.False(result.IsMathExpression);
        }

        [Fact]
        public void TestSearchEngine_MathExpressionsWithMultipleSigns()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem>();
            
            var resultPlusPlus = engine.Query("1 ++ 2", pool);
            Assert.True(resultPlusPlus.IsMathExpression);
            Assert.Equal("3", resultPlusPlus.MathResult);

            var resultPlusMinus = engine.Query("1 +- 2", pool);
            Assert.True(resultPlusMinus.IsMathExpression);
            Assert.Equal("-1", resultPlusMinus.MathResult);
        }

        [Fact]
        public void TestSearchEngine_DeeplyNestedParentheses_WithinLimitEvaluates()
        {
            ISearchEngine engine = new SearchEngine();
            // Nesting up to the documented cap (64) must evaluate without crashing.
            string input = new string('(', 50) + "1" + new string(')', 50);
            var result = engine.Query(input, new List<AppItem>());
            Assert.True(result.IsMathExpression);
            Assert.Equal("1", result.MathResult);
        }

        [Fact]
        public void TestSearchEngine_ParenthesesDepthExceedingLimit_DoesNotEvaluateAsMath()
        {
            ISearchEngine engine = new SearchEngine();
            // Depth is capped at 64 to keep the recursive evaluator far away from
            // stack limits; anything deeper falls back to a normal app search.
            string input = new string('(', 65) + "1" + new string(')', 65);
            var result = engine.Query(input, new List<AppItem>());
            Assert.False(result.IsMathExpression);
        }

        // --- Consolidated from the former SearchTests.cs ---

        [Fact]
        public void TestSearchEngine_EmptyOrWhitespaceInput_ReturnsAllApps()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem>
            {
                new AppItem { Id = "safari", Name = "Safari" },
                new AppItem { Id = "terminal", Name = "Terminal" }
            };

            var result1 = engine.Query(null!, pool);
            var result2 = engine.Query("   ", pool);

            Assert.False(result1.IsSystemAction);
            Assert.False(result1.IsMathExpression);
            Assert.Equal(pool.Count, result1.FilteredApps.Count);
            Assert.Equal(pool.Count, result2.FilteredApps.Count);
        }

        [Fact]
        public void TestSearchEngine_NormalSearch_FiltersCaseInsensitively()
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem>
            {
                new AppItem { Id = "safari", Name = "Safari" },
                new AppItem { Id = "app_store", Name = "App Store" },
                new AppItem { Id = "calculator", Name = "Calculator" },
                new AppItem { Id = "terminal", Name = "Terminal" }
            };

            var result1 = engine.Query("ar", pool);
            Assert.Single(result1.FilteredApps);
            Assert.Equal("safari", result1.FilteredApps[0].Id);

            var result2 = engine.Query("STORE", pool);
            Assert.Single(result2.FilteredApps);
            Assert.Equal("app_store", result2.FilteredApps[0].Id);

            var result3 = engine.Query("XYZ", pool);
            Assert.Empty(result3.FilteredApps);
        }

        [Theory]
        [InlineData("lock", SystemActionType.Lock)]
        [InlineData("Sleep", SystemActionType.Sleep)]
        [InlineData("restart", SystemActionType.Restart)]
        [InlineData("Shutdown", SystemActionType.Shutdown)]
        public void TestSearchEngine_SystemActionKeywords_IdentifiedCorrectly(string input, SystemActionType expectedAction)
        {
            ISearchEngine engine = new SearchEngine();
            var pool = new List<AppItem> { new AppItem { Id = "safari", Name = "Safari" } };

            var result = engine.Query(input, pool);

            Assert.True(result.IsSystemAction);
            Assert.Equal(expectedAction, result.SystemAction);
            Assert.Empty(result.FilteredApps);
            Assert.False(result.IsMathExpression);
        }
    }
}
