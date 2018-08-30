﻿namespace Microsoft.VisualStudio.Threading.Analyzers.Tests
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.VisualStudio.Threading.Analyzers.Tests.Legacy;
    using Xunit;
    using Xunit.Abstractions;

    public class VSTHRD002UseJtfRunAnalyzerTests : CodeFixVerifier
    {
        private DiagnosticResult expect = new DiagnosticResult
        {
            Id = VSTHRD002UseJtfRunAnalyzer.Id,
            Severity = DiagnosticSeverity.Warning,
        };

        public VSTHRD002UseJtfRunAnalyzerTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new VSTHRD002UseJtfRunAnalyzer();

        protected override CodeFixProvider GetCSharpCodeFixProvider() => new VSTHRD002UseJtfRunCodeFixWithAwait();

        /// <devremarks>
        /// We set TestCategory=AnyCategory here so that *some* test in our assembly uses
        /// "TestCategory" as the name of a trait. This prevents VSTest.Console from failing
        /// when invoked with /TestCaseFilter:"TestCategory!=FailsInCloudTest" for assemblies
        /// such as this one that don't define any TestCategory tests.
        /// </devremarks>
        [Fact, Trait("TestCategory", "AnyCategory-SeeComment")]
        public void TaskWaitShouldReportWarning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    void F() {
        var task = Task.Run(() => {});
        task.Wait();
    }
}
";
            var withFix = @"
using System;
using System.Threading.Tasks;

class Test {
    async Task FAsync() {
        var task = Task.Run(() => {});
        await task;
    }
}
";
            this.expect.Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 14, 8, 18) };
            this.VerifyCSharpDiagnostic(test, this.expect);
            this.VerifyCSharpFix(test, withFix);
        }

        [Fact]
        public void TaskWaitShouldReportWarning_WithinAnonymousDelegate()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    void F() {
        var task = Task.Run(() => {});
        Action a = () => task.Wait();
    }
}
";
            this.expect.Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 31, 8, 35) };
            this.VerifyCSharpDiagnostic(test, this.expect);
            this.VerifyNoCSharpFixOffered(test);
        }

        [Fact]
        public void TaskResultShouldReportWarning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    void F() {
        var task = Task.Run(() => 1);
        var result = task.Result;
    }
}
";
            var withFix = @"
using System;
using System.Threading.Tasks;

class Test {
    async Task FAsync() {
        var task = Task.Run(() => 1);
        var result = await task;
    }
}
";
            this.expect.Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 27, 8, 33) };
            this.VerifyCSharpDiagnostic(test, this.expect);
            this.VerifyCSharpFix(test, withFix);
        }

        [Fact]
        public void TaskResultShouldReportWarning_WithinAnonymousDelegate()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    void F() {
        var task = Task.Run(() => 5);
        Func<int> a = () => task.Result;
    }
}
";
            this.expect.Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 34, 8, 40) };
            this.VerifyCSharpDiagnostic(test, this.expect);
            this.VerifyNoCSharpFixOffered(test);
        }

        [Fact]
        public void TaskResultShouldNotReportWarning_WithinItsOwnContinuationDelegate()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    void F() {
        var irrelevantTask = Task.Run(() => 1);
        var task = Task.Run(() => 5);
        task.ContinueWith(t => irrelevantTask.Result);
        ContinueWith(t => t.Result);
        task.ContinueWith(t => t.Result);
        task.ContinueWith((t) => t.Result);
        task.ContinueWith(delegate (Task<int> t) { return t.Result; });
        task.ContinueWith(t => t.Wait());
        ((Task)task).ContinueWith(t => t.Wait());
        task.ContinueWith((t, s) => t.Result, new object());
    }

    void ContinueWith(Func<Task<int>, int> del) { }
}
";
            var expect = new DiagnosticResult[]
            {
                new DiagnosticResult
                {
                    MessagePattern = this.expect.MessagePattern,
                    Id = this.expect.Id,
                    Severity = this.expect.Severity,
                    Locations = new[] { new DiagnosticResultLocation("Test0.cs", 9, 47, 9, 53) },
                },
                new DiagnosticResult
                {
                    MessagePattern = this.expect.MessagePattern,
                    Id = this.expect.Id,
                    Severity = this.expect.Severity,
                    Locations = new[] { new DiagnosticResultLocation("Test0.cs", 10, 29, 10, 35) },
                },
            };
            this.VerifyCSharpDiagnostic(test, expect);
            this.VerifyNoCSharpFixOffered(test);
        }

        [Fact]
        public void AwaiterGetResultShouldReportWarning()
        {
            var test = @"
using System;
using System.Threading.Tasks;

class Test {
    void F() {
        var task = Task.Run(() => 1);
        task.GetAwaiter().GetResult();
    }
}
";
            var withFix = @"
using System;
using System.Threading.Tasks;

class Test {
    async Task FAsync() {
        var task = Task.Run(() => 1);
        await task;
    }
}
";
            this.expect.Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 27, 8, 36) };
            this.VerifyCSharpDiagnostic(test, this.expect);
            this.VerifyCSharpFix(test, withFix);
        }

        [Fact]
        public void TaskResult_FixUpdatesCallers()
        {
            var test = new[] {
                @"
using System;
using System.Threading.Tasks;

class Test {
    internal static int GetNumber(int a) {
        var task = Task.Run(() => a);
        return task.Result;
    }

    int Add(int a, int b) {
        return GetNumber(a) + b;
    }

    int Subtract(int a, int b) {
        return GetNumber(a) - b;
    }

    static int Main(string[] args)
    {
        return new Test().Add(1, 2);
    }
}
",
                @"
class TestClient {
    int Multiply(int a, int b) {
        return Test.GetNumber(a) * b;
    }
}
", };
            var withFix = new[] {
                @"
using System;
using System.Threading.Tasks;

class Test {
    internal static async Task<int> GetNumberAsync(int a) {
        var task = Task.Run(() => a);
        return await task;
    }

    async Task<int> AddAsync(int a, int b) {
        return await GetNumberAsync(a) + b;
    }

    async Task<int> SubtractAsync(int a, int b) {
        return await GetNumberAsync(a) - b;
    }

    static async Task<int> Main(string[] args)
    {
        return await new Test().AddAsync(1, 2);
    }
}
",
                @"
class TestClient {
    async System.Threading.Tasks.Task<int> MultiplyAsync(int a, int b) {
        return await Test.GetNumberAsync(a) * b;
    }
}
", };
            this.expect.Locations = new[] { new DiagnosticResultLocation("Test0.cs", 8, 21, 8, 27) };
            this.VerifyCSharpDiagnostic(test, hasEntrypoint: true, allowErrors: false, expected: this.expect);
            this.VerifyCSharpFix(test, withFix, hasEntrypoint: true);
        }

        [Fact]
        public void DoNotReportWarningInTaskReturningMethods()
        {
            var test = @"
using System.Threading.Tasks;

class Test {
    Task F() {
        var task = Task.Run(() => 1);
        task.GetAwaiter().GetResult();
        return Task.CompletedTask;
    }
}
";
            this.VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void DoNotReportWarningOnCodeGeneratedByXaml2CS()
        {
            var test = @"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.0
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.VisualStudio.JavaScript.Project {
    using System;
    using System.Threading.Tasks;

    internal partial class ProjectProperties {
        void F() {
            var task = Task.Run(() => 1);
            task.GetAwaiter().GetResult();
            var result = task.Result;
        }
    }
}
";
            this.VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void DoNotReportWarningOnJTFRun()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class ProjectProperties {
    JoinableTaskFactory jtf;

    void F() {
        jtf.Run(async delegate {
            await Task.Yield();
        });
    }
}
";
            this.VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void DoNotReportWarningOnJoinableTaskJoin()
        {
            var test = @"
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class ProjectProperties {
    JoinableTaskFactory jtf;

    void F() {
        var jt = jtf.RunAsync(async delegate {
            await Task.Yield();
        });
        jt.Join();
    }
}
";
            this.VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void MethodsWithoutLeadingMember()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class ProjectProperties {
    public void Start(Task action, Action<Exception> exceptionHandler = null)
    {
        Task task = action.ContinueWith(
            t => exceptionHandler(t.Exception.InnerException),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
";
            this.VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void AnonymousDelegateWithExplicitCast()
        {
            var test = @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

class ProjectProperties {
    public void Start(JoinableTask joinableTask, object registration)
    {
        joinableTask.Task.ContinueWith(
            (_, state) => ((CancellationTokenRegistration)state).Dispose(),
            registration,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
";
            this.VerifyCSharpDiagnostic(test);
        }
    }
}