using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GameEngineAssistant.Services
{
    public class LlmService
    {
        private readonly string _modelName = "qwen2.5-coder-7b-instruct-generic-gpu";
        private readonly HttpClient _httpClient = new HttpClient();

        private string GetApiUrl()
        {
            string port = File.Exists("port.txt") ? File.ReadAllText("port.txt").Trim() : "55438";
            return $"http://127.0.0.1:{port}/v1/chat/completions";
        }

        public async IAsyncEnumerable<string> StreamAnswerWithContextAsync(string userQuery, List<SearchResult> searchResults)
        {
            var contextBuilder = new StringBuilder();
            if (searchResults != null && searchResults.Count > 0)
            {
                foreach (var result in searchResults)
                {
                    contextBuilder.AppendLine($"--- [Döküman Sayfası / Bölüm: {result.ChapterTitle} | Sayfa: {result.PageNumber}] ---");
                    contextBuilder.AppendLine(result.Content);
                    contextBuilder.AppendLine();
                }
            }

            string systemPrompt = @"Sen yetkin, profesyonel bir Akıllı Döküman ve Bilgi Asistanısın (RAG Assistant).

KATI VE ZORUNLU TALİMATLAR:

1. KESİN BAĞLAM BAĞLILIĞI (STRICT CONTEXT GROUNDING):
   - SADECE ve YALNIZCA sana 'BAĞLAM (REFERANS METİNLER)' başlığı altında sağlanan döküman parçalarındaki bilgileri kullanarak cevap ver.
   - Kendi genelleştirilmiş pre-training bilginle DÖKÜMANDA YER ALMAYAN rastgele/uydurma bilgiler ekleme (hallucination kesinlikle yasaktır).
   - Eğer verilen bağlam soruyu yanıtlamak için doğrudan yeterli bilgi içermiyorsa, dürüstçe 'Sağlanan dökümanlarda bu konuyla ilgili yeterli bilgi bulunmamaktadır.' şeklinde belirt.

2. DİL UYUMU (LANGUAGE MATCHING):
   - Kullanıcı sorusunu hangi dilde sorduysa (Türkçe, İngilizce vb.) YALNIZCA O DİLDE yanıt ver.
   - Bağlam metinleri farklı bir dilde (örneğin İngilizce) olsa dahi, kullanıcı Türkçe sorduysa yanıtı akıcı bir Türkçe ile bağlama sadık kalarak sun.

3. BAĞLAM FİLTRELEME VE TEMİZLEME (CONTEXT CLEANING):
   - Sana sağlanan metin parçalarından yalnızca öz bilgiyi süz, analiz et ve kendi anlaşılır profesyonel cümlelerinle derle.
   - Metin içi başlık veya ham şablonları doğrudan kopyalama.

4. KOD FORMATLAMA VE SUNUMU (CODE FORMATTING):
   - Kod örnekleri geçtiğinde markdown kod bloğu (```cpp, ```csharp vb.) formatında sun.";

            string userPrompt = $"BAĞLAM (REFERANS METİNLER):\n{contextBuilder}\n\nKULLANICI SORUSU: {userQuery}";

            var payload = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.2,
                repetition_penalty = 1.15,
                presence_penalty = 0.5,
                max_tokens = 1200,
                stream = true
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, GetApiUrl())
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            
            // Foundry API hata döndürdüyse, hata mesajını oku ve kullanıcıya göster
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"❌ LLM API Hatası ({response.StatusCode}): {errorBody}");
                yield return $"❌ Model yanıt veremedi (HTTP {(int)response.StatusCode}). Lütfen Python sunucusunun çalıştığından emin olun.";
                yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                string tokenToYield = null;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentElement))
                        {
                            var token = contentElement.GetString();
                            if (!string.IsNullOrEmpty(token))
                            {
                                tokenToYield = token; // 💡 Çözüm: Değeri dışarı çıkarıyoruz
                            }
                        }
                    }
                }
                catch
                {
                    // Bozuk veya uyumsuz JSON parçalarını atla
                }

                // 💡 yield return işlemini try-catch bloğunun DIŞINDA yapıyoruz
                if (tokenToYield != null)
                {
                    yield return tokenToYield;
                }
            }
        }
    }
}