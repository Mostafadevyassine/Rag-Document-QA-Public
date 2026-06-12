// Startup: DI wiring, middleware (CORS), and MVC controller routing.
// Keep this thin — real work lives in Services/, request handling in Controllers/.

var builder = WebApplication.CreateBuilder(args);

// OpenAI key from user-secrets (dev) / environment (prod) — never hard-coded.
var key = builder.Configuration["OPENAI_API_KEY"]!;

// 1. MVC controllers (discovers everything in Controllers/)
builder.Services.AddControllers();

// 2. OpenAI-calling services: typed HttpClient with base address + auth header.
builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>(c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/");
    c.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
});
builder.Services.AddHttpClient<IChatService, ChatService>(c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/");
    c.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
});

// 3. Singletons: VectorStore MUST be a singleton so indexed vectors persist
//    between the /upload and /ask requests. PdfService is stateless.
builder.Services.AddSingleton<IVectorStore, VectorStore>();
builder.Services.AddSingleton<PdfService>();

// 4. CORS — allow the Vite dev origin.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();          // must come before mapping controllers
app.MapControllers();   // wires up [Route]/[HttpPost] from Controllers/

app.Run();
