using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;

namespace GameEngineAssistant.Services
{
    // Eğer ParsedChunk sınıfı DocumentIngestionService.cs dosyasında veya başka bir 
    // yerde zaten tanımlıysa aşağıdaki 'public class ParsedChunk' bloğunu silebilirsin.
    // Tanımlı değilse kalabilir.
    public class ParsedChunk
    {
        public string Content { get; set; }
        public int PageNumber { get; set; }
        public string ChapterTitle { get; set; }
    }

    public class PdfParsingService
    {
        public List<ParsedChunk> ParsePdfToChunks(Stream pdfStream, int maxWordsPerChunk = 250, int overlapWords = 50)
        {
            var chunks = new List<ParsedChunk>();
            
            // Kelimeleri ve bulundukları sayfa numaralarını tutmak için bir yapı
            var allWords = new List<(string Text, int Page)>();

            // 1. PDF'in içindeki tüm kelimeleri sayfa numaralarıyla birlikte çıkarıyoruz
            using (var document = PdfDocument.Open(pdfStream))
            {
                foreach (var page in document.GetPages())
                {
                    var words = page.GetWords().Select(w => (Text: w.Text, Page: page.Number));
                    allWords.AddRange(words);
                }
            }

            // 2. KESİŞİMLİ (OVERLAPPING) PARÇALAMA
            for (int i = 0; i < allWords.Count; i += (maxWordsPerChunk - overlapWords))
            {
                var chunkWords = allWords.Skip(i).Take(maxWordsPerChunk).ToList();
                if (!chunkWords.Any()) break;

                string chunkText = string.Join(" ", chunkWords.Select(w => w.Text));
                chunkText = chunkText.Replace("\r", " ").Replace("\n", " ").Trim();
                
                // Parçanın ağırlıklı olarak hangi sayfada başladığını referans alıyoruz
                int chunkPage = chunkWords.First().Page;

                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(new ParsedChunk
                    {
                        Content = chunkText,
                        PageNumber = chunkPage,
                        ChapterTitle = "Genel" // Dinamik başlık bulma algoritması eklenebilir
                    });
                }
            }

            return chunks;
        }
    }
}