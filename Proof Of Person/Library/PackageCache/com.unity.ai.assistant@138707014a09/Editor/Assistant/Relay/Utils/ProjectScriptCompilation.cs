using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;

namespace Unity.AI.Assistant.Editor.Utils
{
    struct CompilationResult
    {
        public bool Success;
        public string ErrorMessage;
    }

    static class ProjectScriptCompilation
    {
        public static Action OnRequestReload;
        public static Action OnBeforeReload;

        static TaskCompletionSource<CompilationResult> s_CompilationTcs;
        static readonly object k_CompilationLock = new ();
        static readonly List<string> s_CompilationErrors = new ();

        static ProjectScriptCompilation()
        {
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;

            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        static void HandleBeforeAssemblyReload()
        {
            OnBeforeReload?.Invoke();
        }

        static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            lock (k_CompilationLock)
            {
                foreach (var message in messages)
                {
                    if (message.type == CompilerMessageType.Error)
                    {
                        var errorMessage = $"{message.file}({message.line},{message.column}): error {message.message}";
                        s_CompilationErrors.Add(errorMessage);
                    }
                }
            }
        }

        static void OnCompilationFinished(object sender)
        {
            lock (k_CompilationLock)
            {
                if (s_CompilationTcs != null)
                {

                    var hasErrors = s_CompilationErrors.Count > 0;
                    var errorMessage = hasErrors ? string.Join("\n", s_CompilationErrors) : string.Empty;

                    s_CompilationTcs.TrySetResult(new CompilationResult
                    {
                        Success = !hasErrors,
                        ErrorMessage = errorMessage
                    });

                    s_CompilationTcs = null;
                    s_CompilationErrors.Clear();
                }
            }
        }

        internal static void ForceDomainReload()
        {
            OnRequestReload?.Invoke();

            EditorUtility.RequestScriptReload();
        }

        public static async Task<CompilationResult> RequestProjectCompilation(int timeoutMs = 60000)
        {
            Task<CompilationResult> existingTask = null;
            lock (k_CompilationLock)
            {
                // If there's already a compilation in progress, capture the task
                if (s_CompilationTcs != null)
                {
                    existingTask = s_CompilationTcs.Task;
                }
                else
                {
                    // Clear any previous compilation errors
                    s_CompilationErrors.Clear();
                    s_CompilationTcs = new TaskCompletionSource<CompilationResult>();
                }
            }

            if (existingTask != null)
                return await existingTask;

            // Request compilation
            CompilationPipeline.RequestScriptCompilation();

            using var cts = new CancellationTokenSource(timeoutMs);
            var timeoutTask = Task.Delay(timeoutMs, cts.Token);

            var completedTask = await Task.WhenAny(s_CompilationTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                CompilationResult timeoutResult;
                lock (k_CompilationLock) {
                    timeoutResult = new CompilationResult { Success = false, ErrorMessage = "Compilation timeout: compilation did not complete within the specified time limit." };
                    s_CompilationTcs?.TrySetResult(timeoutResult);
                    s_CompilationTcs = null;
                }
                return timeoutResult;
            }

            return await s_CompilationTcs.Task;
        }
    }
}
