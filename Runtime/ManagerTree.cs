using System.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine;

namespace Five.Architecture
{
    public abstract class ManagerTree : MonoBehaviour, IAsyncDisposable
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private bool isDone;
        private bool isRunning;
        private bool isDisposing;

        protected CancellationToken LifeTimeToken => cts.Token;
        protected bool IsDone => isDone;

        protected abstract Task OnInitializeAsync();
        protected abstract Task OnRunAsync();
        protected abstract ValueTask OnDisposeAsync();

        protected virtual void OnDestroy()
        {
            cts.Cancel();
        }

        protected virtual void EndRun()
        {
            isDone = true;
        }

#if UNITY_EDITOR
        [ContextMenu("Dispose")]
        private async Task EditorDispose()
        {
            await DisposeAsync();
        }
#endif

        private static void SafeDestroyObject(GameObject obj)
        {
            if (obj != null)
            {
                if (!obj.Equals(null))
                {
                    Destroy(obj);
                }
            }
        }

        /// <summary>
        /// Destroy component's GameObject if Component & GameObject is not already destroyed
        /// </summary>
        /// <param name="comp">Component which GameObject is to destroy</param>
        public static void SafeDestroy(Component comp)
        {
            if (comp != null)
            {
                if (!comp.Equals(null))
                {
                    SafeDestroyObject(comp.gameObject);
                }
            }
        }

        /// <summary>
        /// Synchronously create instance, call to Initialize. Run & Dispose is handled manually
        /// </summary>
        /// <param name="manager"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>instance of type T</returns>
        protected async Task<T> Load<T>(ManagerTree manager)
            where T : ManagerTree
        {
            var managerInstance = Instantiate(manager);
            await managerInstance.InitializeAsync();
            return managerInstance.GetComponent<T>();
        }

        /// <summary>
        /// Asynchronously create instance, call to Initialize. Run & Dispose is handled manually
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>instance of type T</returns>
        protected async Task<T> LoadAsync<T>(ManagerTree manager, CancellationToken cancellationToken)
            where T : ManagerTree
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var handle = InstantiateAsync(manager, 1);

            while (!handle.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    handle.Cancel();
                    cts.Cancel();
                    return null;
                }
                await Task.Yield();
            }

            var managerInstance = handle.Result[0];
            await managerInstance.InitializeAsync();
            return managerInstance.GetComponent<T>();
        }

        /// <summary>
        /// Synchronously create instance, call to Initialize & Run. Dispose is handled manually
        /// </summary>
        /// <param name="manager"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>instance of type T</returns>
        protected async Task<T> Launch<T>(ManagerTree manager)
            where T : ManagerTree
        {
            var managerInstance = Instantiate(manager);
            await managerInstance.InitializeAsync();
            await managerInstance.RunAsync();
            return managerInstance.GetComponent<T>();
        }

        /// <summary>
        /// Asynchronously create instance, call to Initialize & Run. Dispose is handled manually
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>instance of type T</returns>
        protected async Task<T> LaunchAsync<T>(ManagerTree manager, CancellationToken cancellationToken)
            where T : ManagerTree
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var handle = InstantiateAsync(manager, 1);

            while (!handle.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    handle.Cancel();
                    cts.Cancel();
                    return null;
                }
                await Task.Yield();
            }

            var managerInstance = handle.Result[0];
            await managerInstance.InitializeAsync();
            await managerInstance.RunAsync();
            return managerInstance.GetComponent<T>();
        }

        /// <summary>
        /// Asynchronously create instance, call to Initialize & Run.
        /// Dispose is handled automatically when isDone = true or cancellation requested
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>instance of type T</returns>
        protected async Task LaunchThenDisposeAsync<T>(ManagerTree manager, CancellationToken cancellationToken) where T : ManagerTree
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var managerInstance = await LaunchAsync<T>(manager, cancellationToken);

            while (!cancellationToken.IsCancellationRequested && !managerInstance.isDone && !managerInstance.LifeTimeToken.IsCancellationRequested)
            {
                await Task.Yield();
            }

            await managerInstance.DisposeAsync();
        }

        /// <summary>
        /// Asynchronously execute the manager and wait for a result.
        /// Returns a result based on the execution.
        /// Dispose is handled automatically when isDone = true or cancellation requested
        /// </summary>
        /// <param name="manager"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Result of type TResult</returns>
        protected async Task<TResult> LaunchThenDisposeWithResultAsync<T, TResult>(ManagerTree manager, CancellationToken cancellationToken) where T : ManagerTree
        {
            TResult result = default;

            if (cancellationToken.IsCancellationRequested)
            {
                return result;
            }

            var managerInstance = await LaunchAsync<T>(manager, cancellationToken);

            while (!cancellationToken.IsCancellationRequested && !managerInstance.isDone && !managerInstance.LifeTimeToken.IsCancellationRequested)
            {
                await Task.Yield();
            }

            if (managerInstance is IResultProvider<TResult> resultProvider)
            {
                result = resultProvider.GetResult();
            }

            await managerInstance.DisposeAsync();

            return result;
        }

        protected async Task InitializeAsync()
        {
            await OnInitializeAsync();
        }

        public async Task RunAsync()
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;

            await OnRunAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (isDisposing) return;
            isDisposing = true;
            await OnDisposeAsync();
        }
    }
}