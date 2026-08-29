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

        public async Task<List<SearchResult>> SearchBookAsync(string query, List<string> targetDocumentIds = null, int topK = 10, float minSimilarityThreshold = 0.20f)
        {
            // Note: If targetDocumentIds is null or empty, search across ALL documents by default.
            float[] queryVector = await GetEmbeddingAsync(query);
            if (queryVector == null || queryVector.Length == 0) return new List<SearchResult>();

            var results = new List<SearchResult>();

            // Extract key search terms and stem prefixes from query for hybrid reranking
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "nedir", "nasıl", "olur", "veya", "ve", "bir", "bu", "için", "ile", "gibi", "mi", "mı", "mu", "mü" };
            var rawKeywords = query.Split(new[] { ' ', '?', '!', '.', ',', ':', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Where(w => w.Length >= 3 && !stopWords.Contains(w))
                                   .Select(w => w.ToLowerInvariant())
                                   .ToList();

            var keywords = new HashSet<string>(rawKeywords);
            foreach (var kw in rawKeywords)
            {
                if (kw.Length > 5)
                {
                    keywords.Add(kw.Substring(0, 5));
                }
            }

            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            string selectQuery = "SELECT chapter_title, page_number, content, embedding, document_name FROM BookChunks WHERE embedding IS NOT NULL";
            
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
                string docName = reader.IsDBNull(4) ? "" : reader.GetString(4);

                float[] chunkVector = ConvertToFloats(blob);
                float baseSimilarity = CosineSimilarity(queryVector, chunkVector);

                // Hybrid scoring: Vector Cosine Similarity + Keyword Match Bonus + Doc Match Bonus
                float hybridScore = baseSimilarity;
                string contentLower = content.ToLowerInvariant();
                string docLower = docName.ToLowerInvariant();

                int matchCount = 0;
                foreach (var kw in keywords)
                {
                    if (contentLower.Contains(kw))
                    {
                        matchCount++;
                    }
                    if (docLower.Contains(kw))
                    {
                        hybridScore += 0.08f;
                    }
                }
                hybridScore += matchCount * 0.06f;

                if (hybridScore > 0.15f)
                {
                    results.Add(new SearchResult
                    {
                        ChapterTitle = chapter,
                        PageNumber = page,
                        Content = content,
                        Similarity = hybridScore
                    });
                }
            }

            var sortedResults = results.OrderByDescending(r => r.Similarity).Take(topK).ToList();
            return sortedResults;
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