using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameEngineAssistant.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        
        // Foundry Local'ın varsayılan API adresi. 
        // İnternete çıkmıyoruz, doğrudan senin Mac'indeki portla konuşuyoruz.
        private readonly string _apiUrl = "http://127.0.0.1:55438/v1/embeddings";

        public EmbeddingService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            // Foundry Local'a göndereceğimiz JSON veri paketi (Payload)
            var payload = new
            {
                model = "qwen3-embedding-0.6b-generic-gpu:1", // Kullanacağımız hafif ve hızlı embedding modeli
                input = text
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                // Yerel yapay zekamıza POST isteği atıyoruz
                var response = await _httpClient.PostAsync(_apiUrl, content);
                
                // YENİ EKLENEN KISIM: Eğer sunucu hata verirse, gizli mesajı oku ve ekrana bas!
                if (!response.IsSuccessStatusCode)
                {
                    string errorDetail = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"\n❌ API Hata Detayı: {errorDetail}");
                    return null; // Çökmesini engelleyip null dönüyoruz
                }

                // Gelen cevabı okuyup JSON içindeki float dizisini (vektörü) ayıklıyoruz
                string responseJson = await response.Content.ReadAsStringAsync();
                
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    var embeddingElement = doc.RootElement
                                              .GetProperty("data")[0]
                                              .GetProperty("embedding");
                                              
                    var embedding = JsonSerializer.Deserialize<float[]>(embeddingElement.GetRawText());
                    return embedding;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Beklenmeyen kod hatası: {ex.Message}");
                return null;
            }
        }
    }
}
