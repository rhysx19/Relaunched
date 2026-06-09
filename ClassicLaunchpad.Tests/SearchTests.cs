using System;
using System.Collections.Generic;
using Xunit;

namespace ClassicLaunchpad.Tests
{
    public class SearchTests
    {
        private readonly List<ClassicLaunchpad.Core.AppItem> _testPool = new List<ClassicLaunchpad.Core.AppItem>
        {
            new ClassicLaunchpad.Core.AppItem { Id = "safari", Name = "Safari" },
            new ClassicLaunchpad.Core.AppItem { Id = "app_store", Name = "App Store" },
            new ClassicLaunchpad.Core.AppItem { Id = "calculator", Name = "Calculator" },
            new ClassicLaunchpad.Core.AppItem { Id = "terminal", Name = "Terminal" }
        };

        [Fact]
        public void Query_WithEmptyInput_ReturnsAllApps()
        {
            // Arrange
            var engine = new ClassicLaunchpad.Core.SearchEngine();

            // Act
            var result1 = engine.Query(null!, _testPool);
            var result2 = engine.Query("   ", _testPool);

            // Assert
            Assert.NotNull(result1);
            Assert.False(result1.IsSystemAction);
            Assert.False(result1.IsMathExpression);
            Assert.Equal(_testPool.Count, result1.FilteredApps.Count);

            Assert.NotNull(result2);
            Assert.Equal(_testPool.Count, result2.FilteredApps.Count);
        }

        [Fact]
        public void Query_WithNormalSearch_FiltersAppsCaseInsensitively()
        {
            // Arrange
            var engine = new ClassicLaunchpad.Core.SearchEngine();

            // Act
            var result1 = engine.Query("ar", _testPool);
            var result2 = engine.Query("STORE", _testPool);
            var result3 = engine.Query("XYZ", _testPool);

            // Assert
            Assert.Single(result1.FilteredApps);
            Assert.Equal("safari", result1.FilteredApps[0].Id);

            Assert.Single(result2.FilteredApps);
            Assert.Equal("app_store", result2.FilteredApps[0].Id);

            Assert.Empty(result3.FilteredApps);
        }

        [Theory]
        [InlineData("lock", ClassicLaunchpad.Core.SystemActionType.Lock)]
        [InlineData("Sleep", ClassicLaunchpad.Core.SystemActionType.Sleep)]
        [InlineData("restart", ClassicLaunchpad.Core.SystemActionType.Restart)]
        [InlineData("Shutdown", ClassicLaunchpad.Core.SystemActionType.Shutdown)]
        public void Query_WithSystemAction_IdentifiesActionCorrectly(string input, ClassicLaunchpad.Core.SystemActionType expectedAction)
        {
            // Arrange
            var engine = new ClassicLaunchpad.Core.SearchEngine();

            // Act
            var result = engine.Query(input, _testPool);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSystemAction);
            Assert.Equal(expectedAction, result.SystemAction);
            Assert.Empty(result.FilteredApps);
            Assert.False(result.IsMathExpression);
        }

        [Theory]
        [InlineData("2 + 3", "5")]
        [InlineData("10 - 2 * 3", "4")]
        [InlineData("(5 + 5) / 2", "5")]
        [InlineData(" 3.5 * 2 ", "7")]
        public void Query_WithValidMathExpression_EvaluatesResult(string input, string expectedResult)
        {
            // Arrange
            var engine = new ClassicLaunchpad.Core.SearchEngine();

            // Act
            var result = engine.Query(input, _testPool);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsMathExpression);
            Assert.Equal(expectedResult, result.MathResult);
            Assert.False(result.IsSystemAction);
            Assert.Empty(result.FilteredApps);
        }

        [Fact]
        public void Query_WithInvalidMathExpressionOrDivByZero_FallsBackToNormalSearch()
        {
            // Arrange
            var engine = new ClassicLaunchpad.Core.SearchEngine();

            // Act
            var resultDivByZero = engine.Query("10 / 0", _testPool);
            var resultBadSyntax = engine.Query("5 + +", _testPool);

            // Assert
            Assert.NotNull(resultDivByZero);
            Assert.False(resultDivByZero.IsMathExpression);
            Assert.Empty(resultDivByZero.FilteredApps);

            Assert.NotNull(resultBadSyntax);
            Assert.False(resultBadSyntax.IsMathExpression);
            Assert.Empty(resultBadSyntax.FilteredApps);
        }
    }
}
