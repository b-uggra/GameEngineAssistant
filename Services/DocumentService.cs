using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Threading.Tasks;

namespace GameEngineAssistant.Services
{
    public class DocumentItem
    {
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentName { get; set; } = string.Empty;
        public int ChunkCount { get; set; }
    }

    public class DocumentService
    {
        private readonly string _dbPath = "RagDatabase.db";

        public async Task InitializeDatabaseSchemaAsync()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            string updateSchemaQuery = @"
                PRAGMA foreign_keys=OFF;
                ALTER TABLE BookChunks ADD COLUMN document_id TEXT DEFAULT 'game_engine_book';
                ALTER TABLE BookChunks ADD COLUMN document_name TEXT DEFAULT 'Game Engine Architecture';
            ";

            try
            {
                using var cmd = new SqliteCommand(updateSchemaQuery, conn);
                await cmd.ExecuteNonQueryAsync();
                Console.WriteLine("✅ Veritabanı şeması başarıyla güncellendi.");
            }
            catch
            {
                // Kolonlar zaten ekliyse yok sayıyoruz
            }
        }

        public async Task<List<DocumentItem>> GetDocumentsAsync()
        {
            var documents = new List<DocumentItem>();
            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            string query = @"
                SELECT document_id, document_name, COUNT(*) as chunk_count 
                FROM BookChunks 
                GROUP BY document_id, document_name;";

            using var cmd = new SqliteCommand(query, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                documents.Add(new DocumentItem
                {
                    DocumentId = reader.GetString(0),
                    DocumentName = reader.GetString(1),
                    ChunkCount = reader.GetInt32(2)
                });
            }

            return documents;
        }

        public async Task<bool> DeleteDocumentAsync(string documentId)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            string deleteQuery = "DELETE FROM BookChunks WHERE document_id = @docId;";
            using var cmd = new SqliteCommand(deleteQuery, conn);
            cmd.Parameters.AddWithValue("@docId", documentId);

            int affectedRows = await cmd.ExecuteNonQueryAsync();
            return affectedRows > 0;
        }
    }
}
