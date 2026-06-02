using Cargo.BuildingBlocks.Messaging;
using Cargo.BuildingBlocks.Notifications.Email;
using Cargo.BuildingBlocks.Notifications.WhatsApp;
using Cargo.NotificationService.Consumers;
using Cargo.NotificationService.Handlers;
using Cargo.NotificationService.Notifications.Email;
using Cargo.NotificationService.Notifications.WhatsApp;
using Cargo.Observability;

var builder = WebApplication.CreateBuilder(args);

// ── Observability ────────────────────────────────────────────────
builder.AddCargoObservability("cargo-notification-service");

// ── RabbitMQ settings ────────────────────────────────────────────
builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection(RabbitMqSettings.SectionName));

// ── Email channel ────────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();

// ── WhatsApp channel ─────────────────────────────────────────────
builder.Services.Configure<WhatsAppSettings>(
    builder.Configuration.GetSection(WhatsAppSettings.SectionName));
builder.Services.AddHttpClient<WhatsAppService>();
builder.Services.AddTransient<IWhatsAppService, WhatsAppService>();

// ── Notification handlers (scoped per message) ───────────────────
builder.Services.AddScoped<EmailNotificationHandler>();
builder.Services.AddScoped<WhatsAppNotificationHandler>();
builder.Services.AddScoped<PushNotificationHandler>();

// ── RabbitMQ consumer (hosted service) ───────────────────────────
builder.Services.AddHostedService<NotificationConsumer>();

// ── Health check ─────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Health endpoint — used by Docker Compose ─────────────────────
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
