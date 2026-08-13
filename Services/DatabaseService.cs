using System;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading.Tasks;

namespace GameEngineAssistant.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath = "RagDatabase.db";

        public async Task InitializeDatabaseAsync()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};");
            await conn.OpenAsync();

            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS BookChunks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    document_id TEXT DEFAULT 'game_engine_book',
                    document_name TEXT DEFAULT 'Game Engine Architecture',
                    chapter_title TEXT,
                    page_number INTEGER,
                    content TEXT NOT NULL,
                    embedding BLOB
                );";

            using var cmd = new SqliteCommand(createTableQuery, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}