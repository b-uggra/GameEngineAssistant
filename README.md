# 🎮 Game Engine Assistant — Akıllı Döküman Asistanı (RAG)

> **Yerel yapay zeka destekli, PDF tabanlı Retrieval-Augmented Generation (RAG) sistemi.**
> Yüklediğin PDF dökümanlarından sorduğun sorulara, yalnızca döküman içeriğine sadık kalarak canlı streaming yanıtlar üretir.

![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Python](https://img.shields.io/badge/Python_3-3776AB?style=flat-square&logo=python&logoColor=white)
![SQLite](https://img.shields.io/badge/SQLite-003B57?style=flat-square&logo=sqlite&logoColor=white)
![Foundry Local](https://img.shields.io/badge/Foundry_Local-FF6F00?style=flat-square&logo=microsoft&logoColor=white)
![License](https://img.shields.io/badge/License-Apache_2.0-green?style=flat-square)

---

## 📌 Proje Hakkında

**Game Engine Assistant**, Jason Gregory'nin *"Game Engine Architecture (3rd Edition)"* kitabı başta olmak üzere, herhangi bir PDF dökümanını yükleyerek içeriğinden soru-cevap yapmanı sağlayan **tam yerel (local) bir RAG asistanıdır**.

Tüm işlemler (embedding, arama, yanıt üretme) **internet bağlantısı olmadan**, kendi bilgisayarında çalışır. Veriler hiçbir zaman dışarı gönderilmez.

### ✨ Öne Çıkan Özellikler

- 🔒 **%100 Yerel & Gizli** — Tüm modeller ve veriler makinende çalışır
- 📄 **Sürükle-Bırak PDF Yükleme** — Herhangi bir PDF'i anında vektörleştirip kütüphaneye ekle
- 🔍 **Hibrit Arama** — Vektör benzerliği + anahtar kelime kök eşleme (stem-aware hybrid search)
- ⚡ **Canlı SSE Streaming** — Yanıtlar kelime kelime anlık olarak akar
- 📚 **Çoklu Döküman Desteği** — Birden fazla PDF yükle, istediğini seç veya hepsinde ara
- 🎯 **Halüsinasyon Koruması** — Model yalnızca döküman içeriğiyle yanıt verir, uydurma yapmaz
- 🗑️ **Döküman Yönetimi** — Yüklenen dökümanları listele ve sil

---

## 🏗️ Mimari

```
┌─────────────────────────────────────────────────────────┐
│                    KULLANICI (Tarayıcı)                  │
│              http://localhost:5000                        │
│  ┌──────────────────────────────────────────────────┐    │
│  │  index.html (Tailwind CSS + Marked.js)           │    │
│  │  • Sürükle-Bırak PDF Upload                      │    │
│  │  • Döküman Seçimi (Checkbox)                      │    │
│  │  • SSE Markdown Streaming Chat                    │    │
│  └──────────────────┬───────────────────────────────┘    │
└─────────────────────┼───────────────────────────────────┘
                      │ HTTP REST API
┌─────────────────────┼───────────────────────────────────┐
│  ASP.NET Core Web Server (C#)          Program.cs        │
│                     │                                     │
│  ┌──────────────────┴───────────────────────────────┐    │
│  │  /api/documents/upload  →  PdfParsingService      │    │
│  │                            DocumentIngestionService│    │
│  │                            EmbeddingService        │    │
│  │                                                    │    │
│  │  /api/chat              →  BookSearchService       │    │
│  │                            (Hibrit Arama)          │    │
│  │                            LlmService              │    │
│  │                            (SSE Streaming)         │    │
│  │                                                    │    │
│  │  /api/documents         →  DocumentService         │    │
│  └──────────────────┬───────────────────────────────┘    │
└─────────────────────┼───────────────────────────────────┘
                      │ OpenAI-Compatible API (127.0.0.1)
┌─────────────────────┼───────────────────────────────────┐
│  Python Foundry Local Server       start_foundry.py      │
│                     │                                     │
│  ┌──────────────────┴───────────────────────────────┐    │
│  │  Embedding Model:                                 │    │
│  │    qwen3-embedding-0.6b-generic-gpu               │    │
│  │                                                    │    │
│  │  Generation Model:                                │    │
│  │    qwen2.5-coder-7b-instruct-generic-gpu          │    │
│  └──────────────────────────────────────────────────┘    │
│                                                           │
│              ┌─────────────┐                              │
│              │  port.txt   │  ← Dinamik port senkronize   │
│              └─────────────┘                              │
└───────────────────────────────────────────────────────────┘
                      │
              ┌───────┴───────┐
              │ RagDatabase.db │  ← SQLite (Chunks + Embeddings)
              └───────────────┘
```

---

## 🛠️ Teknoloji Yığını

| Katman | Teknoloji | Açıklama |
|--------|-----------|----------|
| **Backend** | ASP.NET Core (.NET 10) | Web sunucusu, REST API, SSE streaming |
| **Frontend** | HTML + Tailwind CSS + Marked.js | Sürükle-bırak upload, markdown chat arayüzü |
| **Veritabanı** | SQLite (Microsoft.Data.Sqlite) | Chunk ve vektör embedding saklama |
| **PDF İşleme** | UglyToad.PdfPig | PDF'den metin çıkarma ve chunklama |
| **AI Runtime** | Microsoft Foundry Local SDK | Yerel model yönetimi ve OpenAI-compatible API |
| **Embedding Model** | Qwen3-Embedding-0.6B | Metin → vektör dönüşümü |
| **LLM (Generation)** | Qwen2.5-Coder-7B-Instruct | Bağlam tabanlı yanıt üretimi |

---

## 📁 Proje Yapısı

```
GameEngineAssistant/
├── Program.cs                          # Ana web sunucusu ve API endpoint'leri
├── start_foundry.py                    # Python: Yerel AI model sunucusunu başlatır
├── chunk_book.py                       # Python: PDF'leri chunk'lara ayırma yardımcı scripti
├── port.txt                            # Foundry sunucusunun dinamik port bilgisi
├── RagDatabase.db                      # SQLite veritabanı (chunk'lar + embedding'ler)
├── GameEngineAssistant.csproj          # .NET proje dosyası
├── GameEngineAssistant.sln             # Solution dosyası
├── LICENSE                             # Apache 2.0 Lisansı
│
├── Services/
│   ├── PdfParsingService.cs            # PDF → metin chunk'larına ayrıştırma
│   ├── DocumentIngestionService.cs     # Chunk'ları vektörleştirip DB'ye kaydetme
│   ├── EmbeddingService.cs             # Foundry API ile embedding vektörü üretme
│   ├── BookSearchService.cs            # Hibrit arama (vektör + anahtar kelime)
│   ├── BookEmbeddingWorker.cs          # Arka plan embedding işlemi
│   ├── LlmService.cs                   # LLM ile SSE streaming yanıt üretme
│   ├── DocumentService.cs              # Döküman CRUD işlemleri
│   ├── DocumentProcessor.cs            # Döküman işleme koordinasyonu
│   └── DatabaseService.cs              # SQLite bağlantı yönetimi
│
└── wwwroot/
    └── index.html                      # Tek sayfa web arayüzü (SPA)
```

---

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler

| Araç | Minimum Versiyon |
|------|------------------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ |
| [Python](https://www.python.org/downloads/) | 3.10+ |
| [Foundry Local SDK](https://pypi.org/project/foundry-local-sdk/) | En güncel |

### 1️⃣ Python Bağımlılıklarını Kur

```bash
pip install foundry-local-sdk
```

### 2️⃣ .NET Bağımlılıklarını Kur

```bash
dotnet restore
```

### 3️⃣ Projeyi Çalıştır

> ⚠️ **Önemli:** İki terminal penceresi gereklidir. Terminal 1 tamamlanmadan Terminal 2'yi başlatmayın!

**Terminal 1** — Yerel AI Sunucusunu Başlat:
```bash
python start_foundry.py
```
> ✅ `"SUNUCU AKTİF!"` mesajını gördüğünde modeller yüklenmiş demektir.

**Terminal 2** — Web Sunucusunu Başlat:
```bash
dotnet run
```
> ✅ `"Now listening on: http://localhost:5000"` mesajını gördüğünde hazır.

### 4️⃣ Tarayıcıda Aç

```
http://localhost:5000
```

> 💡 **IDE İçinde Açmak İçin:** `Cmd + Shift + P` → `Simple Browser: Show` → `http://localhost:5000`

---

## 📖 Kullanım

1. **PDF Yükle** — Sol panelden PDF dosyanı sürükle-bırak ile yükle
2. **Döküman Seç** *(isteğe bağlı)* — Checkbox ile hangi dökümanlarda arama yapılacağını seç (hiçbiri seçilmezse tümünde arar)
3. **Soru Sor** — Alt kısımdaki chat kutusuna sorunuzu yazıp gönderin
4. **Yanıtı İzle** — Model, yalnızca döküman içeriğine dayalı olarak anlık streaming yanıt üretir

---

## ⚙️ Yapılandırma

| Parametre | Dosya | Varsayılan | Açıklama |
|-----------|-------|------------|----------|
| `topK` | `Program.cs` | `8` | Arama sonucu döndürülecek en alakalı chunk sayısı |
| `temperature` | `LlmService.cs` | `0.2` | Yanıt çeşitliliği (düşük = daha tutarlı) |
| `repetition_penalty` | `LlmService.cs` | `1.15` | Token tekrar engelleme katsayısı |
| `max_tokens` | `LlmService.cs` | `1200` | Maksimum yanıt uzunluğu |
| Web sunucu portu | `Program.cs` | `5000` | C# web sunucu portu |

---

## 🧑‍💻 Geliştirici

**Osman Buğra Örten**
- 🎓 Yazılım Mühendisliği — 4. Sınıf


---

## 📄 Lisans

Bu proje [Apache License 2.0](LICENSE) altında lisanslanmıştır.
