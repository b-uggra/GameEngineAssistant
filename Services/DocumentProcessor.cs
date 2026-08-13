using System;
using System.Collections.Generic;

namespace GameEngineAssistant.Services
{
    public class DocumentProcessor
    {
        /// <summary>
        /// Metni kelimelere böler ve belirlenen boyutta (chunkSize), 
        /// aralarında kesişim (overlap) bırakarak parçalar.
        /// </summary>
        public List<string> ChunkTextWithOverlap(string text, int chunkSize = 100, int overlap = 20)
        {
            // Metni boşluklara ve satır sonlarına göre kelime kelime ayırıyoruz
            var words = text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();

            // overlap (kesişim) kadar geri giderek döngüyü ilerletiyoruz
            for (int i = 0; i < words.Length; i += (chunkSize - overlap))
            {
                // Kalan kelime sayısını kontrol ediyoruz ki dizinin (array) dışına çıkıp hata almayalım
                int remainingWords = words.Length - i;
                int currentChunkSize = Math.Min(chunkSize, remainingWords);
                
                var chunkWords = new string[currentChunkSize];
                Array.Copy(words, i, chunkWords, 0, currentChunkSize);
                
                // Böldüğümüz kelimeleri tekrar boşluklarla birleştirip listemize ekliyoruz
                chunks.Add(string.Join(" ", chunkWords));

                // Eğer metnin sonuna geldiysek döngüyü bitiriyoruz
                if (i + chunkSize >= words.Length)
                    break;
            }

            return chunks;
        }
    }
}
