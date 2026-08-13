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
        private readonly string _modelName = "qwen2.5-coder-7b-instruct-generic-gpu:4";
        private readonly HttpClient _httpClient = new HttpClient();

        private string GetApiUrl()
        {
            string port = File.Exists("port.txt") ? File.ReadAllText("port.txt").Trim() : "52081";
            return $"http://127.0.0.1:{port}/v1/chat/completions";
        }

        public async IAsyncEnumerable<string> StreamAnswerWithContextAsync(string userQuery, List<SearchResult> searchResults)
        {
            var contextBuilder = new StringBuilder();
            foreach (var result in searchResults)
            {
                contextBuilder.AppendLine($"--- [Döküman Sayfası / Bölüm: {result.ChapterTitle} | Sayfa: {result.PageNumber}] ---");
                contextBuilder.AppendLine(result.Content);
                contextBuilder.AppendLine();
            }

            string systemPrompt = @"Sen yetkin, profesyonel bir Akıllı Döküman ve Bilgi Asistanısın.

KATI VE ZORUNLU TALİMATLAR:

1. DİL UYUMU (LANGUAGE MATCHING):
   - Kullanıcı sorusunu hangi dilde sorduysa (Türkçe, İngilizce vb.) YALNIZCA O DİLDE yanıt ver. 
   - Bağlam (Context) metinleri farklı bir dilde olsa dahi, kullanıcı Türkçe sorduysa yanıt tamamen Türkçe olmalıdır. Kullanıcı İngilizce sorduysa yanıt tamamen İngilizce olmalıdır.

2. BAĞLAM FİLTRELEME VE TEMİZLEME (CONTEXT CLEANING):
   - Sana sağlanan bağlam metinleri veritabanından alınmış ham parçalardır. 
   - Metin içinde geçen 'Kullanıcı Sorusu:', 'Assistant:', '--- [Bölüm:...]', sayfa indeksleri, yayıncı notları veya tekrarlayan şablon başlıklarını ASLA yanıtına kopyalama veya taklit etme.
   - Yalnızca dökümandaki bilgiyi süz, analiz et ve kendi özgün cümlelerinle derle.

3. KOD FORMATLAMA VE SUNUMU (CODE FORMATTING):
   - Kullanıcı kod örneği istediğinde: Bağlam metinlerinde geçen kod parçalarını tam ve eksiksiz bir şekilde ilgili dilin markdown bloğu (örn: ```cpp, ```csharp, ```python, ```javascript) içerisine alarak temizce formatla.
   - Eğer sağlanan bağlamda doğrudan çalışabilir bir kod örneği yoksa, durumu açıkça belirt ('Referans dökümanda doğrudan kod örneği yer almamaktadır') ve konunun teorik altyapısını açıkla.

4. BİLGİ DÜZEYİ VE TON (TONE & CONTEXT ACCURACY):
   - Yanıtların doğrudan sorulan soruya odaklansın.
   - Dökümanda yer almayan bilgileri uydurma (hallucination yapma).
   - Kaynak sayfa numaralarını veya bölüm adlarını yanıt metninin içinde tekrar etme; mevcuttakiler sistem tarafından otomatik eklenecektir.";

            string userPrompt = $"BAĞLAM (REFERANS METİNLER):\n{contextBuilder}\n\nKULLANICI SORUSU: {userQuery}";

            var payload = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                repetition_penalty = 1.15,
                max_tokens = 1200,
                stream = true
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, GetApiUrl())
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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