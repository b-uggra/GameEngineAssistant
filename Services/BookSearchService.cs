using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameEngineAssistant.Services
{
    public class SearchResult
    {
        public string ChapterTitle { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string Content { get; set; } = string.Empty;
        public float Similarity { get; set; }
    }

    public class BookSearchService
    {
        private readonly string _dbPath = "RagDatabase.db";
        private readonly string _modelName = "qwen3-embedding-0.6b-generic-gpu:1";
        private readonly HttpClient _httpClient = new HttpClient();

        private string GetApiUrl()
        {
            string port = File.Exists("port.txt") ? File.ReadAllText("port.txt").Trim() : "55438";
            return $"http://127.0.0.1:{port}/v1/embeddings";
        }

        public async Task<List<SearchResult>> SearchBookAsync(string query, List<string> targetDocumentIds = null, int topK = 5, float minSimilarityThreshold = 0.35f)
        {
            // 1. Boş Liste Kontrolü: Kullanıcı arayüzden döküman seçmediyse döküman araması yapma
            if (targetDocumentIds != null && targetDocumentIds.Count == 0)
            {
                return new List<SearchResult>();
            }

            float[] queryVector = await GetEmbeddingAsync(query);
            if (queryVector == null || queryVector.Length == 0) return new List<SearchResult>();

            var results = new List<SearchResult>();

            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            string selectQuery = "SELECT chapter_title, page_number, content, embedding FROM BookChunks WHERE embedding IS NOT NULL";
            
            if (targetDocumentIds != null && targetDocumentIds.Count > 0)
            {
                string inClause = string.Join(",", targetDocumentIds.Select((_, i) => $"@doc{i}"));
                selectQuery += $" AND document_id IN ({inClause})";
            }

            using var cmd = new SqliteCommand(selectQuery, conn);

            if (targetDocumentIds != null && targetDocumentIds.Count > 0)
            {
                for (int i = 0; i < targetDocumentIds.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@doc{i}", targetDocumentIds[i]);
                }
            }

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                string chapter = reader.IsDBNull(0) ? "Genel" : reader.GetString(0);
                int page = reader.GetInt32(1);
                string content = reader.GetString(2);
                byte[] blob = (byte[])reader["embedding"];

                float[] chunkVector = ConvertToFloats(blob);
                float similarity = CosineSimilarity(queryVector, chunkVector);

                // 2. Benzerlik Eşiği Filtresi: Sadece %35 ve üzeri alakası olan parçaları ekliyoruz
                if (similarity >= minSimilarityThreshold)
                {
                    results.Add(new SearchResult
                    {
                        ChapterTitle = chapter,
                        PageNumber = page,
                        Content = content,
                        Similarity = similarity
                    });
                }
            }

            // 3. Sıralama ve Tavan Sınırı: En yüksek skora sahip ilk 5 parçayı döndürüyoruz
            return results.OrderByDescending(r => r.Similarity).Take(topK).ToList();
        }

        public async Task<List<SearchResult>> SearchBookAsync(string query, int topK)
        {
            return await SearchBookAsync(query, null, topK);
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
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"\n⚠️ API Hatası: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return Array.Empty<float>();
                }

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
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Bağlantı/Istek Hatası: {ex.Message}");
                return Array.Empty<float>();
            }
        }

        private float CosineSimilarity(float[] vecA, float[] vecB)
        {
            float dotProduct = 0.0f;
            float normA = 0.0f;
            float normB = 0.0f;

            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }

            if (normA == 0.0f || normB == 0.0f) return 0.0f;

            return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }

        private float[] ConvertToFloats(byte[] bytes)
        {
            float[] floats = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            return floats;
        }
    }
}