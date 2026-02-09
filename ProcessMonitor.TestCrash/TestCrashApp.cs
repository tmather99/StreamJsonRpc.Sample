using System;

namespace TestCrashApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Test Crash Application");
        Console.WriteLine("======================");
        Console.WriteLine("This app will crash to test the Process Crash Monitor");
        Console.WriteLine();
        Console.WriteLine("Select crash type:");
        Console.WriteLine("1. Null Reference Exception");
        Console.WriteLine("2. Divide by Zero");
        Console.WriteLine("3. Stack Overflow");
        Console.WriteLine("4. Access Violation (unsafe)");
        Console.WriteLine("5. Unhandled Exception in Task");
        Console.Write("\nEnter choice (1-5): ");

        var choice = Console.ReadLine();

        Console.WriteLine("\nCrashing in 3 seconds...");
        System.Threading.Thread.Sleep(3000);

        switch (choice)
        {
            case "1":
                CrashNullReference();
                break;
            case "2":
                CrashDivideByZero();
                break;
            case "3":
                CrashStackOverflow();
                break;
            case "4":
                CrashAccessViolation();
                break;
            case "5":
                CrashUnhandledTask();
                break;
            default:
                Console.WriteLine("Invalid choice, crashing with null reference...");
                CrashNullReference();
                break;
        }
    }

    static void CrashNullReference()
    {
        Console.WriteLine("Triggering null reference exception...");
        string? text = null;
        Console.WriteLine(text!.Length);
    }

    static void CrashDivideByZero()
    {
        Console.WriteLine("Triggering divide by zero...");
        int x = 10;
        int y = 0;
        Console.WriteLine(x / y);
    }

    static void CrashStackOverflow()
    {
        Console.WriteLine("Triggering stack overflow...");
        CrashStackOverflow();
    }

    static void CrashAccessViolation()
    {
        Console.WriteLine("Triggering access violation...");
        unsafe
        {
            int* ptr = (int*)0;
            *ptr = 42;
        }
    }

    static void CrashUnhandledTask()
    {
        Console.WriteLine("Triggering unhandled task exception...");
        Task.Run(() =>
        {
            throw new Exception("Unhandled exception in task!");
        });
        System.Threading.Thread.Sleep(1000);
    }
}
