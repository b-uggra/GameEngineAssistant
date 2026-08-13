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
    public class BookEmbeddingWorker
    {
        private readonly string _dbPath = "RagDatabase.db";
        private readonly string _modelName = "qwen3-embedding-0.6b-generic-gpu:1";
        private readonly HttpClient _httpClient = new HttpClient();

        private string GetApiUrl()
        {
            string port = File.Exists("port.txt") ? File.ReadAllText("port.txt").Trim() : "55438";
            return $"http://127.0.0.1:{port}/v1/embeddings";
        }

        public async Task ProcessBookEmbeddingsAsync()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            string selectQuery = "SELECT id, content FROM BookChunks WHERE embedding IS NULL;";
            using var cmd = new SqliteCommand(selectQuery, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var itemsToProcess = new List<(long id, string content)>();
            while (await reader.ReadAsync())
            {
                itemsToProcess.Add((reader.GetInt64(0), reader.GetString(1)));
            }

            Console.WriteLine($"📚 Vektörleştirilecek Parça Sayısı: {itemsToProcess.Count}");

            if (itemsToProcess.Count == 0)
            {
                Console.WriteLine("✅ Tüm parçaların vektörleri zaten veritabanında kayıtlı!");
                return;
            }

            int successCount = 0;
            int failCount = 0;

            foreach (var item in itemsToProcess)
            {
                float[] vector = await GetEmbeddingAsync(item.content);
                if (vector != null && vector.Length > 0)
                {
                    byte[] blob = ConvertToBytes(vector);
                    string updateQuery = "UPDATE BookChunks SET embedding = @emb WHERE id = @id;";
                    using var updateCmd = new SqliteCommand(updateQuery, conn);
                    updateCmd.Parameters.AddWithValue("@emb", blob);
                    updateCmd.Parameters.AddWithValue("@id", item.id);
                    await updateCmd.ExecuteNonQueryAsync();
                    successCount++;
                }
                else
                {
                    failCount++;
                }

                Console.Write($"\r⚙️ İlerleme: [{successCount + failCount}/{itemsToProcess.Count}] - Başarılı: {successCount} | Hatalı: {failCount}");
            }

            Console.WriteLine($"\n\n🎉 İŞLEM BİTTİ! Toplam {successCount} parça başarıyla veritabanına yazıldı.");
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
                    Console.WriteLine($"\n⚠️ API Hatası ({response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
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
                Console.WriteLine($"\n❌ Bağlantı Hatası: {ex.Message}");
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