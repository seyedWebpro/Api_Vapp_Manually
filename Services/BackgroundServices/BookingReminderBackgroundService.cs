using Api_Vapp.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api_Vapp.Services.BackgroundServices
{
    /// <summary>
    /// ارسال SMS یادآوری نوبت — هر ۱ دقیقه
    /// </summary>
    public class BookingReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingReminderBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public BookingReminderBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<BookingReminderBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Booking Reminder Background Service started");
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var appointmentService = scope.ServiceProvider
                        .GetRequiredService<Interfaces.IBookingAppointmentService>();
                    await appointmentService.ProcessRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing booking reminders");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Booking Reminder Background Service stopped");
        }
    }
}
