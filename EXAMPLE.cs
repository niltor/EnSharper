using System;

namespace SampleCode
{
    /// <summary>
    /// Sample class to demonstrate the code alignment features
    /// </summary>
    public class AlignmentDemo
    {
        // Example 1: Variable assignment alignment
        // Before formatting, these assignments might look like:
        // int x = 5;
        // string name = "test";
        // var value = 100;
        //
        // After formatting with alignment enabled, they will be aligned:
        // int x      = 5;
        // string name = "test";
        // var value  = 100;

        public void TestVariableAlignment()
        {
            int x = 5;
            string name = "test";
            var value = 100;
            bool isActive = true;
        }

        // Example 2: Constructor with more than 2 parameters
        // Will be formatted with each parameter on a new line
        public AlignmentDemo(int param1, string param2, bool param3)
        {
        }

        // Example 3: Method with more than 3 parameters
        // Will be formatted with each parameter on a new line
        public void MethodWithManyParameters(int a, string b, bool c, double d)
        {
        }

        // Example 4: Multiple consecutive assignments
        public void MultipleAssignments()
        {
            var firstName = "John";
            var lastName = "Doe";
            var age = 30;
            var email = "john.doe@example.com";
        }

        // Example 5: Constructor with many parameters (will be aligned)
        public AlignmentDemo(
            string firstName,
            string lastName,
            int age,
            string email,
            string phoneNumber)
        {
        }
    }
}
