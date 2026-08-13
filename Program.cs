using GameEngineAssistant.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Servisleri kaydediyoruz
builder.Services.AddSingleton<DocumentService>();
builder.Services.AddSingleton<PdfParsingService>();
builder.Services.AddSingleton<DocumentIngestionService>();
builder.Services.AddSingleton<BookSearchService>();
builder.Services.AddSingleton<LlmService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// 💡 1. FIX: Veritabanı tablolarını (BookChunks dahil) manuel ve kesin olarak oluşturuyoruz
using (var conn = new SqliteConnection("Data Source=RagDatabase.db;"))
{
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Documents (
            documentId TEXT PRIMARY KEY,
            documentName TEXT,
            uploadDate DATETIME DEFAULT CURRENT_TIMESTAMP
        );
        CREATE TABLE IF NOT EXISTS BookChunks (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            document_id TEXT,
            document_name TEXT,     -- 💡 EKSİK OLAN SATIR BUYDU, EKLENDİ
            chapter_title TEXT,
            page_number INTEGER,
            content TEXT,
            embedding BLOB
        );";
    cmd.ExecuteNonQuery();
}

// Eski DocumentService initini de çağıralım (ne olur ne olmaz)
using (var scope = app.Services.CreateScope())
{
    var docService = scope.ServiceProvider.GetRequiredService<DocumentService>();
    await docService.InitializeDatabaseSchemaAsync();
}

// 1. Dökümanları Listeleme
app.MapGet("/api/documents", async (DocumentService docService) =>
{
    var docs = await docService.GetDocumentsAsync();
    return Results.Ok(docs);
});

// 2. Döküman Silme
app.MapDelete("/api/documents/{id}", async (string id, DocumentService docService) =>
{
    bool success = await docService.DeleteDocumentAsync(id);
    return success ? Results.Ok(new { message = "Döküman silindi." }) : Results.NotFound();
});

// 3. Sürükle-Bırak PDF Yükleme ve Vektörleştirme
// 💡 2. FIX: DisableAntiforgery() ekleyerek dosya yükleme güvenlik hatasını aşıyoruz
app.MapPost("/api/documents/upload", async (IFormFile file, PdfParsingService parser, DocumentIngestionService ingestion) =>
{
    if (file == null || file.Length == 0) return Results.BadRequest("Geçersiz dosya.");

    string docId = Guid.NewGuid().ToString("N");
    string docName = file.FileName;

    using var stream = file.OpenReadStream();
    var chunks = parser.ParsePdfToChunks(stream);

    if (chunks.Count == 0) return Results.BadRequest("PDF metni okunamadı.");

    await ingestion.ProcessAndSavePdfAsync(docId, docName, chunks);

    return Results.Ok(new { documentId = docId, documentName = docName, chunkCount = chunks.Count });
}).DisableAntiforgery(); 

// 4. Chat / RAG Live SSE Streaming (Canlı Akış)
app.MapPost("/api/chat", async (HttpContext http, ChatRequest req, BookSearchService searchService, LlmService llmService) =>
{
    http.Response.Headers.Append("Content-Type", "text/event-stream");
    http.Response.Headers.Append("Cache-Control", "no-cache");

    var searchResults = await searchService.SearchBookAsync(req.Query, req.DocumentIds, topK: 5);

    // 1. Önce Referansları JSON Olarak İletiyoruz
    var refJson = JsonSerializer.Serialize(new { references = searchResults });
    await http.Response.WriteAsync($"data: {refJson}\n\n");
    await http.Response.Body.FlushAsync();

    // 2. Ardından Kelime Kelime Cevabı Akıtıyoruz
    await foreach (var token in llmService.StreamAnswerWithContextAsync(req.Query, searchResults))
    {
        var tokenJson = JsonSerializer.Serialize(new { token = token });
        await http.Response.WriteAsync($"data: {tokenJson}\n\n");
        await http.Response.Body.FlushAsync();
    }

    await http.Response.WriteAsync("data: [DONE]\n\n");
    await http.Response.Body.FlushAsync();
});

app.Run();

public class ChatRequest
{
    public string Query { get; set; } = string.Empty;
    public List<string> DocumentIds { get; set; } = new();
}