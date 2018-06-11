﻿namespace Microsoft.VisualStudio.Threading.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class ReentrantSemaphoreJTFTests : ReentrantSemaphoreTestBase
    {
        private readonly JoinableTaskContext joinableTaskContext;

        public ReentrantSemaphoreJTFTests(ITestOutputHelper logger)
            : base(logger)
        {
            using (this.Dispatcher.Apply())
            {
                this.joinableTaskContext = new JoinableTaskContext();
            }

            this.semaphore = new ReentrantSemaphore(joinableTaskContext: this.joinableTaskContext);
        }

        [Fact]
        public void SemaphoreWaiterJoinsSemaphoreHolders()
        {
            var firstEntered = new AsyncManualResetEvent();
            bool firstOperationReachedMainThread = false;
            var firstOperation = Task.Run(async delegate
            {
                await this.semaphore.ExecuteAsync(
                    async delegate
                    {
                        firstEntered.Set();
                        await this.joinableTaskContext.Factory.SwitchToMainThreadAsync(this.TimeoutToken);
                        firstOperationReachedMainThread = true;
                    },
                    this.TimeoutToken);
            });

            bool secondEntryComplete = false;
            this.ExecuteOnDispatcher(async delegate
            {
                this.joinableTaskContext.Factory.Run(async delegate
                {
                    await firstEntered.WaitAsync().WithCancellation(this.TimeoutToken);
                    Assumes.False(firstOperationReachedMainThread);

                    // While blocking the main thread, request the semaphore.
                    // This should NOT deadlock if the semaphore properly Joins the existing semaphore holder(s),
                    // allowing them to get to the UI thread and then finally to exit the semaphore so we can enter it.
                    await this.semaphore.ExecuteAsync(
                        delegate
                        {
                            secondEntryComplete = true;
                            Assert.True(firstOperationReachedMainThread);
                            return TplExtensions.CompletedTask;
                        },
                        this.TimeoutToken);
                });
                await Task.WhenAll(firstOperation).WithCancellation(this.TimeoutToken);
                Assert.True(secondEntryComplete);
            });
        }
    }
}