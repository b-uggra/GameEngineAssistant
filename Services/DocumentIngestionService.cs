using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameEngineAssistant.Services
{
    public class DocumentIngestionService
    {
        private readonly string _dbPath = "RagDatabase.db";
        private readonly string _modelName = "qwen3-embedding-0.6b-generic-gpu:1";
        private readonly HttpClient _httpClient = new HttpClient();

        private string GetApiUrl()
        {
            string port = File.Exists("port.txt") ? File.ReadAllText("port.txt").Trim() : "55438";
            return $"http://127.0.0.1:{port}/v1/embeddings";
        }

        public async Task ProcessAndSavePdfAsync(string documentId, string documentName, List<ParsedChunk> chunks)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            using var transaction = conn.BeginTransaction();

            string insertQuery = @"
                INSERT INTO BookChunks (document_id, document_name, chapter_title, page_number, content, embedding)
                VALUES (@docId, @docName, @chapter, @page, @content, @emb);";

            foreach (var chunk in chunks)
            {
                float[] vector = await GetEmbeddingAsync(chunk.Content);
                byte[] blob = (vector != null && vector.Length > 0) ? ConvertToBytes(vector) : null;

                using var cmd = new SqliteCommand(insertQuery, conn, transaction);
                cmd.Parameters.AddWithValue("@docId", documentId);
                cmd.Parameters.AddWithValue("@docName", documentName);
                cmd.Parameters.AddWithValue("@chapter", $"Sayfa {chunk.PageNumber}");
                cmd.Parameters.AddWithValue("@page", chunk.PageNumber);
                cmd.Parameters.AddWithValue("@content", chunk.Content);
                cmd.Parameters.AddWithValue("@emb", (object)blob ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        private async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                var payload = new { model = _modelName, input = text };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                string apiUrl = GetApiUrl();
                var response = await _httpClient.PostAsync(apiUrl, content);
                if (!response.IsSuccessStatusCode) return Array.Empty<float>();

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var dataArray = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");

                float[] embedding = new float[dataArray.GetArrayLength()];
                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] = dataArray[i].GetSingle();
                }
                return embedding;
            }
            catch
            {
                return Array.Empty<float>();
            }
        }

        private byte[] ConvertToBytes(float[] floats)
        {
            if (floats == null || floats.Length == 0) return Array.Empty<byte>();
            byte[] bytes = new byte[floats.Length * sizeof(float)];
            Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
