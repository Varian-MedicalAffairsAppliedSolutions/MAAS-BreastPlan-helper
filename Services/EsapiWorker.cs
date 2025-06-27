using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using VMS.TPS.Common.Model.API;

namespace MAAS_BreastPlan_helper.Services
{
    public class EsapiWorker
    {
        private readonly ScriptContext _scriptContext;
        private readonly Dispatcher _dispatcher;

        public ScriptContext Context => _scriptContext;

        public EsapiWorker(ScriptContext scriptContext)
        {
            _scriptContext = scriptContext;
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public void Run(Action<ScriptContext> action)
        {
            _dispatcher.BeginInvoke(action, _scriptContext);
        }

        public void RunWithWait(Action<ScriptContext> action)
        {
            _dispatcher.BeginInvoke(action, _scriptContext).Wait();
        }

        public async Task RunWithWaitAsync(Action<ScriptContext> action)
        {
            await _dispatcher.BeginInvoke(action, _scriptContext);
        }

        // Helper methods for common ESAPI operations
        public T GetValue<T>(Func<ScriptContext, T> getter)
        {
            T result = default(T);
            RunWithWait(sc => result = getter(sc));
            return result;
        }

        public void ExecuteWithErrorHandling(Action<ScriptContext> action, Action<Exception> onError = null)
        {
            try
            {
                RunWithWait(action);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }

        public async Task ExecuteWithErrorHandlingAsync(Action<ScriptContext> action, Action<Exception> onError = null)
        {
            try
            {
                await RunWithWaitAsync(action);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }
    }
} 