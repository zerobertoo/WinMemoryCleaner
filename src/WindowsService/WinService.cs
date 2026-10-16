using System;
using System.Linq;
using System.ServiceProcess;
using System.Timers;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Windows Memory Cleaner Service
    /// </summary>
    public class WinService : ServiceBase
    {
        private readonly Computer _computer;
        private readonly IComputerService _computerService;
        private DateTimeOffset _lastAutoOptimizationByInterval = DateTimeOffset.Now;
        private DateTimeOffset _lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
        private readonly Timer _timer = new Timer(60000);
        private int _isTickRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="WinService" /> class.
        /// </summary>
        public WinService()
        {
            // App resources
            _computer = new Computer();
            _computerService = DependencyInjection.Container.Resolve<IComputerService>();

            // Service settings
            CanPauseAndContinue = false;
            ServiceName = Constants.App.Name;

            // Timer settings
            _timer.AutoReset = true;
            _timer.Elapsed += OnTimer;
        }

        /// <summary>
        /// Gets a value indicating whether the service is installed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the service is installed; otherwise, <c>false</c>.
        /// </value>
        public static bool IsInstalled
        {
            get
            {
                return ServiceController.GetServices().Any(sc => string.Equals(sc.ServiceName, Constants.App.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Gets the status.
        /// </summary>
        /// <value>
        /// The status.
        /// </value>
        public static Enums.ServiceStatus Status
        {
            get
            {
                try
                {
                    using (var service = ServiceController.GetServices().First(sc => string.Equals(sc.ServiceName, Constants.App.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        service.Refresh();

                        return (Enums.ServiceStatus)service.Status;
                    }
                } 
                catch
                {
                    return Enums.ServiceStatus.NotInstalled;
                }
            }
        }

        /// <summary>
        /// Called when debugging the service
        /// </summary>
        /// <param name="args"></param>
        public void OnDebug(string[] args)
        {
            OnStart(args);
            OnTimer(null, null);

            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Method specifies what code to execute each time a service is started. Here it configures parameters for the timer
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            _timer.Start();
        }

        /// <summary>
        /// Method specifies what code to execute each time a service is stopped. Here it configures parameters for the timer
        /// </summary>
        protected override void OnStop()
        {
            _timer.Stop();

            // Aguarda (até 30 s) a otimização em curso terminar antes de encerrar o serviço
            var deadline = DateTime.UtcNow.AddSeconds(30);

            while (System.Threading.Volatile.Read(ref _isTickRunning) != 0 && DateTime.UtcNow < deadline)
                System.Threading.Thread.Sleep(100);
        }

        /// <summary>
        /// Method specifies what code to execute each time a timer's interval is up
        /// /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            // Evita ticks sobrepostos (a otimização pode demorar mais que o intervalo do timer)
            if (System.Threading.Interlocked.CompareExchange(ref _isTickRunning, 1, 0) != 0)
                return;

            try
            {
                RunTimerTick();
            }
            catch (Exception exception)
            {
                // exceção não tratada em Elapsed derrubaria o serviço
                Logger.Error(exception);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isTickRunning, 0);
            }
        }

        private void RunTimerTick()
        {
            // App priority
            App.SetPriority(Settings.RunOnPriority);

            // Update memory info
            _computer.Memory = _computerService.Memory;

            var reason = AutoOptimizationTrigger.Decide(DateTimeOffset.Now, Settings.AutoOptimizationInterval, _lastAutoOptimizationByInterval, Settings.AutoOptimizationMemoryUsage, _computer.Memory.Physical.Free.Percentage, _lastAutoOptimizationByMemoryUsage, Constants.App.AutoOptimizationMemoryUsageInterval);

            if (reason is null)
                return;

            DependencyInjection.Container.Resolve<IComputerService>().Optimize(reason.Value, Settings.MemoryAreas);

            if (reason == Enums.Memory.Optimization.Reason.Schedule)
                _lastAutoOptimizationByInterval = DateTimeOffset.Now;
            else
                _lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
        }
    }
}
