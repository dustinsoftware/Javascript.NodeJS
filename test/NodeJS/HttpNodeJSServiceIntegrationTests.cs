﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Jering.Javascript.NodeJS.Tests
{
    /// <summary>
    /// These tests are de facto tests for HttpServer.ts. They serve the additional role of verifying that IPC works properly.
    /// </summary>
    public class HttpNodeJSServiceIntegrationTests : IDisposable
    {
        // Set to true to break in NodeJS (see CreateHttpNodeJSService)
        private const bool DEBUG_NODEJS = false;
        // Set to -1 when debugging in NodeJS
        private const int TIMEOUT_MS = 60000;
        private const string DUMMY_RETURNS_ARG_MODULE_FILE = "dummyReturnsArgModule.js";
        private const string DUMMY_EXPORTS_MULTIPLE_FUNCTIONS_MODULE_FILE = "dummyExportsMultipleFunctionsModule.js";
        private const string DUMMY_CACHE_IDENTIFIER = "dummyCacheIdentifier";

        private static readonly string _projectPath = Path.Combine(Directory.GetCurrentDirectory(), "../../../Javascript"); // Current directory is <test project path>/bin/debug/<framework>
        private static readonly string _dummyReturnsArgModule = File.ReadAllText(Path.Combine(_projectPath, DUMMY_RETURNS_ARG_MODULE_FILE));
        private static readonly string _dummyExportsMultipleFunctionsModule = File.ReadAllText(Path.Combine(_projectPath, DUMMY_EXPORTS_MULTIPLE_FUNCTIONS_MODULE_FILE));

        private readonly ITestOutputHelper _testOutputHelper;
        private IServiceProvider _serviceProvider;

        public HttpNodeJSServiceIntegrationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromFileAsync_WithTypeParameter_InvokesFromFile()
        {
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            DummyResult result = await testSubject.
                InvokeFromFileAsync<DummyResult>(DUMMY_RETURNS_ARG_MODULE_FILE, args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public void InvokeFromFileAsync_WithTypeParameter_IsThreadSafe()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            var results = new ConcurrentQueue<DummyResult>();
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => results.Enqueue(testSubject.
                    InvokeFromFileAsync<DummyResult>(DUMMY_RETURNS_ARG_MODULE_FILE, args: new[] { dummyArg }).GetAwaiter().GetResult()));
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Equal(numThreads, results.Count);
            foreach (DummyResult result in results)
            {
                Assert.Equal(dummyArg, result.Result);
            }
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromFileAsync_WithoutTypeParameter_InvokesFromFile()
        {
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            await testSubject.InvokeFromFileAsync(DUMMY_EXPORTS_MULTIPLE_FUNCTIONS_MODULE_FILE, "setString", new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            DummyResult result = await testSubject.
                InvokeFromFileAsync<DummyResult>(DUMMY_EXPORTS_MULTIPLE_FUNCTIONS_MODULE_FILE, "getString").ConfigureAwait(false);
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromFileAsync_WithoutTypeParameter_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => testSubject.
                    InvokeFromFileAsync(DUMMY_EXPORTS_MULTIPLE_FUNCTIONS_MODULE_FILE, "incrementNumber").GetAwaiter().GetResult());
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            int result = await testSubject.InvokeFromFileAsync<int>(DUMMY_EXPORTS_MULTIPLE_FUNCTIONS_MODULE_FILE, "getNumber").ConfigureAwait(false);
            Assert.Equal(numThreads, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithTypeParameter_WithRawStringModule_InvokesFromString()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            DummyResult result = await testSubject.
                InvokeFromStringAsync<DummyResult>(_dummyReturnsArgModule, args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public void InvokeFromStringAsync_WithTypeParameter_WithRawStringModule_IsThreadSafe()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            var results = new ConcurrentQueue<DummyResult>();
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => results.Enqueue(testSubject.
                    InvokeFromStringAsync<DummyResult>(_dummyReturnsArgModule, args: new[] { dummyArg }).GetAwaiter().GetResult()));
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Equal(numThreads, results.Count);
            foreach (DummyResult result in results)
            {
                Assert.Equal(dummyArg, result.Result);
            }
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithoutTypeParameter_WithRawStringModule_InvokesFromString()
        {
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            await testSubject.InvokeFromStringAsync(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "setString", new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            DummyResult result = await testSubject.
                InvokeFromStringAsync<DummyResult>(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "getString").ConfigureAwait(false);
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithoutTypeParameter_WithRawStringModule_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => testSubject.
                    InvokeFromStringAsync(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "incrementNumber").GetAwaiter().GetResult());
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            int result = await testSubject.InvokeFromStringAsync<int>(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.Equal(numThreads, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithTypeParameter_WithModuleFactory_InvokesFromCacheIfModuleIsCached()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            await testSubject.InvokeFromStringAsync(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "setString", new[] { dummyArg }).ConfigureAwait(false);

            // Act
            DummyResult result = await testSubject.
                InvokeFromStringAsync<DummyResult>(() => null, DUMMY_CACHE_IDENTIFIER, "getString").ConfigureAwait(false);

            // Assert
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithTypeParameter_WithModuleFactory_InvokesFromStringAndCachesModuleIfModuleIsNotCached()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            // Module hasn't been cached, so if this returns the expected value, string was sent over
            DummyResult result1 = await testSubject.
                InvokeFromStringAsync<DummyResult>(() => _dummyReturnsArgModule, DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            // Ensure module was cached
            (bool success, DummyResult result2) = await testSubject.TryInvokeFromCacheAsync<DummyResult>(DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).ConfigureAwait(false);
            Assert.Equal(dummyArg, result1.Result);
            Assert.True(success);
            Assert.Equal(dummyArg, result2.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public void InvokeFromStringAsync_WithTypeParameter_WithModuleFactory_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            var results = new ConcurrentQueue<int>();
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => results.Enqueue(testSubject.
                    InvokeFromStringAsync<int>(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "incrementAndGetNumber").GetAwaiter().GetResult()));
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Equal(numThreads, results.Count);
            // Module shouldn't get cached more than once, we should get exactly [1,2,3,4,5]
            List<int> resultsList = results.ToList();
            resultsList.Sort();
            for (int i = 0; i < numThreads; i++)
            {
                Assert.Equal(resultsList[i], i + 1);
            }
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithoutTypeParameter_WithModuleFactory_InvokesFromCacheIfModuleIsCached()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            await testSubject.InvokeFromStringAsync(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Act
            await testSubject.
                InvokeFromStringAsync(() => null, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Assert
            int result = await testSubject.InvokeFromStringAsync<int>(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.Equal(2, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithoutTypeParameter_WithModuleFactory_InvokesFromStringAndCachesModuleIfModuleIsNotCached()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            await testSubject.
                InvokeFromStringAsync(() => _dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Assert
            // Ensure module was cached
            (bool success, int result) = await testSubject.TryInvokeFromCacheAsync<int>(DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.True(success);
            Assert.Equal(1, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStringAsync_WithoutTypeParameter_WithModuleFactory_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => testSubject.
                    InvokeFromStringAsync(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "incrementNumber").GetAwaiter().GetResult());
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            (bool success, int result) = await testSubject.TryInvokeFromCacheAsync<int>(DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.True(success);
            Assert.Equal(numThreads, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithTypeParameter_WithRawStreamModule_InvokesFromStream()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            MemoryStream memoryStream = CreateMemoryStream(_dummyReturnsArgModule);

            // Act
            DummyResult result = await testSubject.InvokeFromStreamAsync<DummyResult>(memoryStream, args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public void InvokeFromStreamAsync_WithTypeParameter_WithRawStreamModule_IsThreadSafe()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            var results = new ConcurrentQueue<DummyResult>();
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    MemoryStream memoryStream = CreateMemoryStream(_dummyReturnsArgModule);
                    results.Enqueue(testSubject.InvokeFromStreamAsync<DummyResult>(memoryStream, args: new[] { dummyArg }).GetAwaiter().GetResult());
                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Equal(numThreads, results.Count);
            foreach (DummyResult result in results)
            {
                Assert.Equal(dummyArg, result.Result);
            }
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithoutTypeParameter_WithRawStreamModule_InvokesFromStream()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);
            MemoryStream memoryStream = CreateMemoryStream(_dummyExportsMultipleFunctionsModule);

            // Act
            await testSubject.InvokeFromStreamAsync(memoryStream, DUMMY_CACHE_IDENTIFIER, "setString", new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            DummyResult result = await testSubject.
                InvokeFromStreamAsync<DummyResult>(memoryStream, DUMMY_CACHE_IDENTIFIER, "getString").ConfigureAwait(false);
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithoutTypeParameter_WithRawStreamModule_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    MemoryStream memoryStream = CreateMemoryStream(_dummyExportsMultipleFunctionsModule);
                    testSubject.InvokeFromStreamAsync(memoryStream, DUMMY_CACHE_IDENTIFIER, "incrementNumber").GetAwaiter().GetResult();
                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            int result = await testSubject.
                InvokeFromStringAsync<int>(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.Equal(numThreads, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithTypeParameter_WithModuleFactory_InvokesFromCacheIfModuleIsCached()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            MemoryStream memoryStream = CreateMemoryStream(_dummyExportsMultipleFunctionsModule);
            await testSubject.InvokeFromStreamAsync(memoryStream, DUMMY_CACHE_IDENTIFIER, "setString", new[] { dummyArg }).ConfigureAwait(false);

            // Act
            DummyResult result = await testSubject.
                InvokeFromStreamAsync<DummyResult>(() => null, DUMMY_CACHE_IDENTIFIER, "getString").ConfigureAwait(false);

            // Assert
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithTypeParameter_WithModuleFactory_InvokesFromStreamAndCachesModuleIfModuleIsNotCached()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            MemoryStream memoryStream = CreateMemoryStream(_dummyReturnsArgModule);

            // Act
            // Module hasn't been cached, so if this returns the expected value, stream was sent over
            DummyResult result1 = await testSubject.
                InvokeFromStreamAsync<DummyResult>(() => memoryStream, DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            // Ensure module was cached
            (bool success, DummyResult result2) = await testSubject.
                TryInvokeFromCacheAsync<DummyResult>(DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).ConfigureAwait(false);
            Assert.Equal(dummyArg, result1.Result);
            Assert.True(success);
            Assert.Equal(dummyArg, result2.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public void InvokeFromStreamAsync_WithTypeParameter_WithModuleFactory_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            var results = new ConcurrentQueue<int>();
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    MemoryStream memoryStream = CreateMemoryStream(_dummyExportsMultipleFunctionsModule);
                    results.Enqueue(testSubject.InvokeFromStreamAsync<int>(() => memoryStream, DUMMY_CACHE_IDENTIFIER, "incrementAndGetNumber").GetAwaiter().GetResult());
                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Equal(numThreads, results.Count);
            // Module shouldn't get cached more than once, we should get exactly [1,2,3,4,5]
            List<int> resultsList = results.ToList();
            resultsList.Sort();
            for (int i = 0; i < numThreads; i++)
            {
                Assert.Equal(resultsList[i], i + 1);
            }
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithoutTypeParameter_WithModuleFactory_InvokesFromCacheIfModuleIsCached()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            MemoryStream memoryStream = CreateMemoryStream(_dummyExportsMultipleFunctionsModule);
            await testSubject.InvokeFromStreamAsync(memoryStream, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Act
            await testSubject.
                InvokeFromStreamAsync(() => null, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Assert
            int result = await testSubject.InvokeFromStreamAsync<int>(memoryStream, DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.Equal(2, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithoutTypeParameter_WithModuleFactory_InvokesFromStreamAndCachesModuleIfModuleIsNotCached()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            MemoryStream memoryStream = CreateMemoryStream(_dummyExportsMultipleFunctionsModule);

            // Act
            await testSubject.
                InvokeFromStreamAsync(() => memoryStream, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Assert
            // Ensure module was cached
            (bool success, int result) = await testSubject.TryInvokeFromCacheAsync<int>(DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.True(success);
            Assert.Equal(1, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InvokeFromStreamAsync_WithoutTypeParameter_WithModuleFactory_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() =>
                {
                    MemoryStream memoryStream = CreateMemoryStream(_dummyExportsMultipleFunctionsModule);
                    testSubject.InvokeFromStreamAsync(memoryStream, DUMMY_CACHE_IDENTIFIER, "incrementNumber").GetAwaiter().GetResult();
                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            (bool success, int result) = await testSubject.TryInvokeFromCacheAsync<int>(DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.True(success);
            Assert.Equal(numThreads, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void TryInvokeFromCacheAsync_WithTypeParameter_InvokesFromCacheIfModuleIsCached()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            await testSubject.InvokeFromStringAsync<DummyResult>(_dummyReturnsArgModule, DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).ConfigureAwait(false);

            // Act
            (bool success, DummyResult value) = await testSubject.TryInvokeFromCacheAsync<DummyResult>(DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            Assert.True(success);
            Assert.Equal(dummyArg, value.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void TryInvokeFromCacheAsync_WithTypeParameter_ReturnsFalseIfModuleIsNotCached()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            (bool success, DummyResult value) = await testSubject.TryInvokeFromCacheAsync<DummyResult>(DUMMY_CACHE_IDENTIFIER, args: new[] { "success" }).ConfigureAwait(false);

            // Assert
            Assert.False(success);
            Assert.Null(value);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void TryInvokeFromCacheAsync_WithTypeParameter_IsThreadSafe()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            await testSubject.InvokeFromStringAsync<DummyResult>(_dummyReturnsArgModule, DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).ConfigureAwait(false);

            // Act
            var results = new ConcurrentQueue<(bool, DummyResult)>();
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => results.Enqueue(testSubject.
                    TryInvokeFromCacheAsync<DummyResult>(DUMMY_CACHE_IDENTIFIER, args: new[] { dummyArg }).GetAwaiter().GetResult()));
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Equal(numThreads, results.Count);
            foreach ((bool success, DummyResult value) in results)
            {
                Assert.True(success);
                Assert.Equal(dummyArg, value.Result);
            }
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void TryInvokeFromCacheAsync_WithoutTypeParameter_InvokesFromCacheIfModuleIsCached()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            await testSubject.InvokeFromStringAsync(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Act
            bool success = await testSubject.TryInvokeFromCacheAsync(DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Assert
            Assert.True(success);
            int result = await testSubject.InvokeFromStringAsync<int>(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.Equal(2, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void TryInvokeFromCacheAsync_WithoutTypeParameter_ReturnsFalseIfModuleIsNotCached()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            bool success = await testSubject.TryInvokeFromCacheAsync(DUMMY_CACHE_IDENTIFIER).ConfigureAwait(false);

            // Assert
            Assert.False(success);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void TryInvokeFromCacheAsync_WithoutTypeParameter_IsThreadSafe()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            await testSubject.InvokeFromStringAsync(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "incrementNumber").ConfigureAwait(false);

            // Act
            const int numThreads = 5;
            var threads = new List<Thread>();
            for (int i = 0; i < numThreads; i++)
            {
                var thread = new Thread(() => testSubject.TryInvokeFromCacheAsync(DUMMY_CACHE_IDENTIFIER, "incrementNumber").GetAwaiter().GetResult());
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            // Assert
            int result = await testSubject.InvokeFromStringAsync<int>(_dummyExportsMultipleFunctionsModule, DUMMY_CACHE_IDENTIFIER, "getNumber").ConfigureAwait(false);
            Assert.Equal(numThreads + 1, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InMemoryInvokeMethods_LoadRequiredModulesFromNodeModulesInProjectDirectory()
        {
            // Arrange
            const string dummyCode = @"public string ExampleFunction(string arg)
{
    // Example comment
    return arg + ""dummyString"";
}";
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath);

            // Act
            string result = await testSubject.InvokeFromStringAsync<string>(@"const prismjs = require('prismjs');
require('prismjs/components/prism-csharp');

module.exports = (callback, code) => {
    var result = prismjs.highlight(code, prismjs.languages.csharp, 'csharp');

    callback(null, result);
};", args: new[] { dummyCode }).ConfigureAwait(false);

            // Assert
            const string expectedResult = @"<span class=""token keyword"">public</span> <span class=""token keyword"">string</span> <span class=""token function"">ExampleFunction</span><span class=""token punctuation"">(</span><span class=""token keyword"">string</span> arg<span class=""token punctuation"">)</span>
<span class=""token punctuation"">{</span>
    <span class=""token comment"">// Example comment</span>
    <span class=""token keyword"">return</span> arg <span class=""token operator"">+</span> <span class=""token string"">""dummyString""</span><span class=""token punctuation"">;</span>
<span class=""token punctuation"">}</span>";
            Assert.Equal(expectedResult, result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void InMemoryInvokeMethods_LoadRequiredModulesFromFilesInProjectDirectory()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService(projectPath: _projectPath); // Current directory is <test project path>/bin/debug/<framework>

            // Act
            int result = await testSubject.InvokeFromStringAsync<int>(@"const value = require('./dummyReturnsValueModule.js');

module.exports = (callback) => {
    callback(null, value);
};").ConfigureAwait(false);

            // Assert
            Assert.Equal(10, result); // dummyReturnsValueModule.js just exports 10
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_ThrowInvocationExceptionIfModuleHasNoExports()
        {
            // Arrange
            const string dummyModule = "return null;";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            InvocationException result = await Assert.ThrowsAsync<InvocationException>(() =>
                testSubject.InvokeFromStringAsync<DummyResult>(dummyModule)).
                ConfigureAwait(false);

            // Assert
            Assert.StartsWith($"The module \"{dummyModule}...\" has no exports. Ensure that the module assigns a function or an object containing functions to module.exports.", result.Message);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_ThrowInvocationExceptionIfThereIsNoModuleExportWithSpecifiedExportName()
        {
            // Arrange
            const string dummyExportName = "dummyExportName";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            InvocationException result = await Assert.ThrowsAsync<InvocationException>(() =>
                testSubject.InvokeFromStringAsync<DummyResult>("module.exports = (callback) => callback(null, {result: 'success'});", DUMMY_CACHE_IDENTIFIER, dummyExportName)).
                ConfigureAwait(false);

            // Assert
            Assert.StartsWith($"The module {DUMMY_CACHE_IDENTIFIER} has no export named {dummyExportName}.", result.Message);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_ThrowInvocationExceptionIfModuleExportWithSpecifiedExportNameIsNotAFunction()
        {
            // Arrange
            const string dummyExportName = "dummyExportName";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            InvocationException result = await Assert.ThrowsAsync<InvocationException>(() =>
                testSubject.InvokeFromStringAsync<DummyResult>($"module.exports = {{{dummyExportName}: 'not a function'}};", exportName: dummyExportName)).
                ConfigureAwait(false);

            // Assert
            Assert.StartsWith($"The export named {dummyExportName} from module \"module.exports = {{dummyEx...\" is not a function.", result.Message);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_ThrowInvocationExceptionIfNoExportNameSpecifiedAndModuleExportsIsNotAFunction()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            InvocationException result = await Assert.ThrowsAsync<InvocationException>(() =>
                testSubject.InvokeFromStringAsync<DummyResult>("module.exports = {result: 'not a function'};")).
                ConfigureAwait(false);

            // Assert
            Assert.StartsWith("The module \"module.exports = {result:...\" does not export a function.", result.Message);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_ThrowInvocationExceptionIfInvokedMethodCallsCallbackWithError()
        {
            // Arrange
            const string dummyErrorString = "error";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            InvocationException result = await Assert.ThrowsAsync<InvocationException>(() =>
                testSubject.InvokeFromStringAsync<DummyResult>("module.exports = (callback, errorString) => callback(new Error(errorString));", args: new[] { dummyErrorString })).
                ConfigureAwait(false);

            // Assert
            Assert.StartsWith(dummyErrorString, result.Message); // Complete message includes the stack
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_ThrowInvocationExceptionIfInvokedAsyncMethodThrowsError()
        {
            // Arrange
            const string dummyErrorString = "error";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            InvocationException result = await Assert.ThrowsAsync<InvocationException>(() =>
                testSubject.InvokeFromStringAsync<DummyResult>("module.exports = async (errorString) => {throw new Error(errorString);}", args: new[] { dummyErrorString })).
                ConfigureAwait(false);

            // Assert
            Assert.StartsWith(dummyErrorString, result.Message); // Complete message includes the stack
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_InvokeAsyncJavascriptMethods()
        {
            // Arrange
            const string dummyArg = "success";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            DummyResult result = await testSubject.
                InvokeFromStringAsync<DummyResult>("module.exports = async (resultString) => {return {result: resultString};}", args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            Assert.Equal(dummyArg, result.Result);
        }

        [Fact(Timeout = TIMEOUT_MS)]
        public async void AllInvokeMethods_InvokeASpecificExportIfExportNameIsNotNull()
        {
            // Arrange
            const string dummyArg = "success";
            const string dummyExportName = "dummyExportName";
            HttpNodeJSService testSubject = CreateHttpNodeJSService();

            // Act
            DummyResult result = await testSubject.
                InvokeFromStringAsync<DummyResult>($"module.exports = {{ {dummyExportName}: (callback, resultString) => callback(null, {{result: resultString}}) }};", exportName: dummyExportName, args: new[] { dummyArg }).ConfigureAwait(false);

            // Assert
            Assert.Equal(dummyArg, result.Result);
        }

        // TODO doesn't pass reliably because Node.js randomly truncates stdout/stderr  - https://github.com/nodejs/node/issues/6456.
        // Still useful for diagnosing issues with output piping.
        // We're using Thread.Sleep(1000) to minimize issues, but it doesn't work all the time.
        [Theory(Timeout = TIMEOUT_MS, Skip = "Node.js randomly truncates stdout/stderr")]
        [MemberData(nameof(AllInvokeMethods_ReceiveAndLogStdoutOutput_Data))]
        public async void AllInvokeMethods_ReceiveAndLogStdoutOutput(string dummyLogArgument, string expectedResult)
        {
            // Arrange
            var resultStringBuilder = new StringBuilder();
            HttpNodeJSService testSubject = CreateHttpNodeJSService(resultStringBuilder);

            // Act
            await testSubject.
                InvokeFromStringAsync<string>($@"module.exports = (callback) => {{ 
            console.log({dummyLogArgument}); 
            callback();

            // Does not work
            // process.stdout.end();
            // process.on(finish, () => callback());

            // Does not work
            // process.stdout.write('', 'utf8', () => callback());
        }}").ConfigureAwait(false);
            Thread.Sleep(1000);
            ((IDisposable)_serviceProvider).Dispose();
            string result = resultStringBuilder.ToString();

            // Assert
            Assert.Equal($"{nameof(LogLevel.Information)}: {expectedResult}\n", result, ignoreLineEndingDifferences: true);
        }

        public static IEnumerable<object[]> AllInvokeMethods_ReceiveAndLogStdoutOutput_Data()
        {
            return new object[][]
            {
                        new object[] { "'dummySingleLineString'", "dummySingleLineString" },
                        new object[] { "`dummy\nMultiline\nString\n`", "dummy\nMultiline\nString\n" }, // backtick for multiline strings in js
                        new object[] { "''", "" },
                        new object[] { "undefined", "undefined" },
                        new object[] { "null", "null" },
                        new object[] { "", "" },
                        new object[] { "{}", "{}" },
                        new object[] { "'a\\n\\nb'", "a\n\nb" }
            };
        }

        // TODO doesn't pass reliably because Node.js randomly truncates stdout/stderr  - https://github.com/nodejs/node/issues/6456.
        // Still useful for diagnosing issues with output piping.
        // We're using Thread.Sleep(1000) to minimize issues, but it doesn't work all the time.
        [Theory(Timeout = TIMEOUT_MS, Skip = "Node.js randomly truncates stdout/stderr")]
        [MemberData(nameof(AllInvokeMethods_ReceiveAndLogStderrOutput_Data))]
        public async void AllInvokeMethods_ReceiveAndLogStderrOutput(string dummyLogArgument, string expectedResult)
        {
            // Arrange
            var resultStringBuilder = new StringBuilder();
            HttpNodeJSService testSubject = CreateHttpNodeJSService(resultStringBuilder);

            // Act
            await testSubject.
                InvokeFromStringAsync<string>($@"module.exports = (callback) => {{ 
            console.error({dummyLogArgument}); 
            callback();
        }}").ConfigureAwait(false);
            Thread.Sleep(1000);
            ((IDisposable)_serviceProvider).Dispose();
            string result = resultStringBuilder.ToString();

            // Assert
            Assert.Equal($"{nameof(LogLevel.Error)}: {expectedResult}\n", result, ignoreLineEndingDifferences: true);
        }

        public static IEnumerable<object[]> AllInvokeMethods_ReceiveAndLogStderrOutput_Data()
        {
            return new object[][]
            {
                        new object[] { "'dummySingleLineString'", "dummySingleLineString" },
                        new object[] { "`dummy\nMultiline\nString\n`", "dummy\nMultiline\nString\n" }, // backtick for multiline strings in js
                        new object[] { "''", "" },
                        new object[] { "undefined", "undefined" },
                        new object[] { "null", "null" },
                        new object[] { "", "" },
                        new object[] { "{}", "{}" },
                        new object[] { "'a\\n\\nb'", "a\n\nb" }
            };
        }

        [Fact]
        public async void AllInvokeMethods_HandleHttpClientErrorsSuchAsMalformedRequests()
        {
            // Arrange
            HttpNodeJSService testSubject = CreateHttpNodeJSService();
            await testSubject.InvokeFromStringAsync("module.exports = callback => callback();").ConfigureAwait(false); // Starts the Node.js process
            Uri dummyEndpoint = testSubject.Endpoint;
            var dummyJsonService = new JsonService();

            // Act
            using (var dummyHttpClient = new HttpClient())
            // Send a request with an invalid HTTP method. NodeJS drops the connection halfway through and fires the clientError event - https://nodejs.org/api/http.html#http_event_clienterror
            using (var dummyHttpRequestMessage = new HttpRequestMessage(new HttpMethod("INVALID"), dummyEndpoint))
            using (HttpResponseMessage dummyHttpResponseMessage = await dummyHttpClient.SendAsync(dummyHttpRequestMessage).ConfigureAwait(false))
            using (Stream dummyStream = await dummyHttpResponseMessage.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                InvocationError result = await dummyJsonService.DeserializeAsync<InvocationError>(dummyStream).ConfigureAwait(false);

                // Assert
                Assert.Equal(HttpStatusCode.InternalServerError, dummyHttpResponseMessage.StatusCode);
                Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
                Assert.False(string.IsNullOrWhiteSpace(result.ErrorStack));
            }
        }

        /// <summary>
        /// Specify <paramref name="loggerStringBuilder"/> for access to all logging output.
        /// </summary>
        private HttpNodeJSService CreateHttpNodeJSService(StringBuilder loggerStringBuilder = null,
            string projectPath = null)
        {
            var services = new ServiceCollection();
            services.AddNodeJS(); // Default INodeService is HttpNodeService
            if (projectPath != null)
            {
                services.Configure<NodeJSProcessOptions>(options => options.ProjectPath = projectPath);
            }
            services.AddLogging(lb =>
            {
                lb.
                    AddProvider(new TestOutputProvider(_testOutputHelper)).
                    AddFilter<TestOutputProvider>((LogLevel loglevel) => loglevel >= LogLevel.Debug);

                if (loggerStringBuilder != null)
                {
                    lb.
                        AddProvider(new StringBuilderProvider(loggerStringBuilder)).
                        AddFilter<StringBuilderProvider>((LogLevel LogLevel) => LogLevel >= LogLevel.Information);
                }
            });

            if (Debugger.IsAttached && DEBUG_NODEJS)
            {
                services.Configure<NodeJSProcessOptions>(options => options.NodeAndV8Options = "--inspect-brk"); // An easy way to step through NodeJS code is to use Chrome. Consider option 1 from this list https://nodejs.org/en/docs/guides/debugging-getting-started/#chrome-devtools-55.
                services.Configure<OutOfProcessNodeJSServiceOptions>(options => options.TimeoutMS = -1);
            }

            _serviceProvider = services.BuildServiceProvider();

            return _serviceProvider.GetRequiredService<INodeJSService>() as HttpNodeJSService;
        }

        private MemoryStream CreateMemoryStream(string value)
        {
#pragma warning disable IDE0067
            var memoryStream = new MemoryStream();
            var streamWriter = new StreamWriter(memoryStream);
#pragma warning disable IDE0067
            streamWriter.Write(value);
            streamWriter.Flush();
            memoryStream.Position = 0;

            return memoryStream;
        }

        private class DummyResult
        {
            public string Result { get; set; }
        }

        public void Dispose()
        {
            ((IDisposable)_serviceProvider).Dispose();
        }
    }
}
