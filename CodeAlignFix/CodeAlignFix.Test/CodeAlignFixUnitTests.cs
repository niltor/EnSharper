using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = CodeAlignFix.Test.CSharpCodeFixVerifier<
    CodeAlignFix.CodeAlignFixAnalyzer,
    CodeAlignFix.CodeAlignFixCodeFixProvider>;

namespace CodeAlignFix.Test
{
    [TestClass]
    public class CodeAlignFixUnitTest
    {
        [TestMethod]
        public async Task TestEmptyCode()
        {
            var test = @"";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestAssignmentAlignment_LocalVariables()
        {
            var test = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        void TestMethod()
        {
            {|#0:int x = 5;|}
            string name = ""test"";
            var value = 100;
        }
    }
}";

            var fixedTest = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        void TestMethod()
        {
            int    x     = 5;
            string name  = ""test"";
            var    value = 100;
        }
    }
}";

            var expected = VerifyCS.Diagnostic(CodeAlignFixAnalyzer.AssignmentAlignmentId).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task TestAssignmentAlignment_Fields()
        {
            var test = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        {|#0:private readonly string _name = ""test"";|}
        private readonly int _value = 100;
        private readonly double _price = 99.99;
    }
}";

            var fixedTest = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        private readonly string _name  = ""test"";
        private readonly int    _value = 100;
        private readonly double _price = 99.99;
    }
}";

            var expected = VerifyCS.Diagnostic(CodeAlignFixAnalyzer.AssignmentAlignmentId).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task TestObjectInitializerAlignment()
        {
            var test = @"
using System;

namespace TestNamespace
{
    class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Address { get; set; }
    }

    class TestClass
    {
        void TestMethod()
        {
            var person = new Person
            {|#0:{
                Name = ""John"",
                Age = 30,
                Address = ""123 Main St""
            }|};
        }
    }
}";

            var fixedTest = @"
using System;

namespace TestNamespace
{
    class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Address { get; set; }
    }

    class TestClass
    {
        void TestMethod()
        {
            var person = new Person
            {
                Name    = ""John"",
                Age     = 30,
                Address = ""123 Main St""
            };
        }
    }
}";

            var expected = VerifyCS.Diagnostic(CodeAlignFixAnalyzer.ObjectInitializerAlignmentId).WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task TestParameterAlignment_Method()
        {
            var test = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        void TestMethod{|#0:(int a, string b, bool c, double d)|}
        {
        }
    }
}";

            // Just verify diagnostic is reported
            var expected = VerifyCS.Diagnostic(CodeAlignFixAnalyzer.ParameterAlignmentId).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestParameterAlignment_Constructor()
        {
            var test = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        public TestClass{|#0:(int a, string b, bool c)|}
        {
        }
    }
}";

            // Just verify diagnostic is reported
            var expected = VerifyCS.Diagnostic(CodeAlignFixAnalyzer.ParameterAlignmentId).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestArgumentAlignment()
        {
            var test = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        void TestMethod()
        {
            Console.WriteLine{|#0:(""a"", ""b"", ""c"", ""d"")|};
        }
    }
}";

            // Just verify the diagnostic is reported
            var expected = VerifyCS.Diagnostic(CodeAlignFixAnalyzer.ArgumentAlignmentId).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestChainedMethodAlignment()
        {
            var test = @"
using System;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        void TestMethod()
        {
            var result = {|#0:new[] { 1, 2, 3 }.Where(x => x > 1).Select(x => x * 2).ToList()|};
        }
    }
}";

            // Chained method detection in method bodies - should trigger diagnostic
            var expected = VerifyCS.Diagnostic(CodeAlignFixAnalyzer.ChainedMethodAlignmentId).WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestNoAlignmentNeeded_SingleVariable()
        {
            var test = @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        void TestMethod()
        {
            int x = 5;
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
