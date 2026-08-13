import re
import sqlite3
from PyPDF2 import PdfReader

PDF_PATH = "Game_Engine_Architecture_-_Jason_Gregory.pdf"
DB_PATH = "RagDatabase.db"

def init_db():
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    # Kitap parçalarını tutacak tabloyu oluşturuyoruz
    cursor.execute('''
        CREATE TABLE IF NOT EXISTS BookChunks (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            chapter_title TEXT,
            page_number INTEGER,
            content TEXT,
            embedding BLOB
        )
    ''')
    conn.commit()
    conn.close()

def extract_and_chunk_pdf():
    print("📖 PDF Okunuyor, bu işlem biraz sürebilir...")
    reader = PdfReader(PDF_PATH)
    total_pages = len(reader.pages)
    print(f"📄 Toplam Sayfa Sayısı: {total_pages}")

    chunks = []
    current_chapter = "Foundations / Intro"
    
    # Kitabın ana metin sayfalarını tarıyoruz (Örn: 15. sayfadan itibaren)
    for page_num in range(14, total_pages):
        page = reader.pages[page_num]
        text = page.extract_text()
        
        if not text or len(text.strip()) < 50:
            continue

        # Bölüm başlıklarını tespit etmeye çalışıyoruz (Örn: Chapter 1, 13 Collision vs.)
        chapter_match = re.search(r'(Chapter\s+\d+|PART\s+[I|V|X]+|[0-9]+\.[0-9]+\s+[A-Z].*)', text)
        if chapter_match:
            current_chapter = chapter_match.group(0).strip()

        # Sayfa metnini temizleyip parçalara bölüyoruz
        words = text.split()
        chunk_size = 600  # Kelime bazlı ideal parça boyutu
        
        for i in range(0, len(words), chunk_size):
            chunk_text = " ".join(words[i:i + chunk_size])
            chunks.append((current_chapter, page_num + 1, chunk_text))

    print(f"✅ Toplam {len(chunks)} adet anlamlı metin parçası (chunk) oluşturuldu.")
    return chunks

def save_chunks_to_sqlite(chunks):
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    
    print("💾 Parçalar SQLite veritabanına kaydediliyor...")
    cursor.executemany('''
        INSERT INTO BookChunks (chapter_title, page_number, content)
        VALUES (?, ?, ?)
    ''', chunks)
    
    conn.commit()
    conn.close()
    print("🎉 İşlem Tamamlandı! Kitap metinleri 'BookChunks' tablosuna aktarıldı.")

if __name__ == "__main__":
    init_db()
    data = extract_and_chunk_pdf()
    save_chunks_to_sqlite(data)
