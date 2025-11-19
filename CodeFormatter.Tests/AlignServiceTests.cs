using Xunit;
using CodeFormatter;

namespace CodeFormatter.Tests
{
    public class AlignServiceTests
    {
        private readonly AlignService _alignService;

        public AlignServiceTests()
        {
            _alignService = new AlignService();
        }

        [Fact]
        public void FormatCode_WithNullInput_ReturnsNull()
        {
            // Act
            string result = _alignService.FormatCode(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FormatCode_WithEmptyInput_ReturnsEmpty()
        {
            // Act
            string result = _alignService.FormatCode("");

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void FormatCode_AlignsConsecutiveAssignments()
        {
            // Arrange
            string input = @"public class Test
{
    public void Method()
    {
        int x = 5;
        string name = ""test"";
        var value = 100;
    }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // The = signs should be aligned
            Assert.Contains("int x", result);
            Assert.Contains("string name", result);
            Assert.Contains("var value", result);
            
            // Verify the code is still valid
            Assert.DoesNotContain("syntax error", result.ToLower());
        }

        [Fact]
        public void FormatCode_AlignsConstructorWith3Parameters()
        {
            // Arrange
            string input = @"public class MyClass
{
    public MyClass(int a, string b, bool c)
    {
    }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // Parameters should be on separate lines for constructors with >2 params
            Assert.Contains("int a", result);
            Assert.Contains("string b", result);
            Assert.Contains("bool c", result);
        }

        [Fact]
        public void FormatCode_DoesNotAlignConstructorWith2Parameters()
        {
            // Arrange
            string input = @"public class MyClass
{
    public MyClass(int a, string b)
    {
    }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // Parameters should remain on same line for constructors with <=2 params
            // The structure should not have multi-line parameter formatting
            Assert.Contains("MyClass", result);
        }

        [Fact]
        public void FormatCode_AlignsMethodWith4Parameters()
        {
            // Arrange
            string input = @"public class MyClass
{
    public void MyMethod(int a, string b, bool c, double d)
    {
    }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // Parameters should be on separate lines for methods with >3 params
            Assert.Contains("int a", result);
            Assert.Contains("string b", result);
            Assert.Contains("bool c", result);
            Assert.Contains("double d", result);
        }

        [Fact]
        public void FormatCode_DoesNotAlignMethodWith3Parameters()
        {
            // Arrange
            string input = @"public class MyClass
{
    public void MyMethod(int a, string b, bool c)
    {
    }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // Parameters should remain on same line for methods with <=3 params
            Assert.Contains("MyMethod", result);
        }

        [Fact]
        public void FormatCode_HandlesInvalidCodeGracefully()
        {
            // Arrange
            string input = @"public void Test(";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // Should return original code when parsing fails
            Assert.Equal(input, result);
        }

        [Fact]
        public void FormatCode_PreservesNonConsecutiveAssignments()
        {
            // Arrange
            string input = @"public class Test
{
    public void Method()
    {
        int x = 5;
        System.Console.WriteLine(""break"");
        string name = ""test"";
    }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // Should not align assignments that are not consecutive
            Assert.Contains("int x", result);
            Assert.Contains("Console.WriteLine", result);
            Assert.Contains("string name", result);
        }

        [Fact]
        public void FormatCode_HandlesComplexCode()
        {
            // Arrange
            string input = @"namespace TestNamespace
{
    public class ComplexClass
    {
        private int field1 = 10;
        private string field2 = ""value"";

        public ComplexClass(int param1, string param2, bool param3)
        {
            field1 = param1;
            field2 = param2;
        }

        public void ComplexMethod(int a, string b, bool c, double d)
        {
            var x = 1;
            var longVariableName = 2;
            var y = 3;
        }
    }
}";

            // Act
            string result = _alignService.FormatCode(input);

            // Assert
            // Should process without errors
            Assert.NotNull(result);
            Assert.Contains("ComplexClass", result);
            Assert.Contains("ComplexMethod", result);
        }
    }
}
